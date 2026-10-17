using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using User0332.Backup.Client.Configuration;
using User0332.Backup.Shared;
using ZstdSharp;

namespace User0332.Backup.Client.JobExecution;

public class Job
{
	const int MaxRetries = 4;

	readonly string _jobId;
	readonly string _basePath;
	readonly string[] _filePatterns;
	readonly ExpiryPolicy _expiryPolicy;
	readonly ChunkSizeConstraint _chunkSize;
	readonly RequestHelper _client;
	readonly Compressor _compressor = new();
	readonly Matcher _matcher = new();
	readonly ILogger<Worker> _logger;

	public Job(JobOptions options, RequestHelper client, ILogger<Worker> logger)
	{
		_jobId = options.JobId;
		_basePath = options.BasePath;
		_filePatterns = [..options.FilePatterns];
		_expiryPolicy = options.ExpiryPolicy;
		_chunkSize = options.ChunkSize;
		_client = client;
		_logger = logger;

		_matcher.AddIncludePatterns(_filePatterns);
	}

	public async Task Execute()
	{
		List<ManifestFileEntry> manifestFiles = [];
		HashSet<uint> aggregatedChunkHashes = [];

		foreach (var pattern in _filePatterns)
		{
			var files = _matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(_basePath)))
				.Files
				.Select(file => Path.Combine(_basePath, file.Path))
				.ToList();

			foreach (var file in files)
			{
				string relativePath = Path.GetRelativePath(_basePath, file);

				using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);

				var chunkHashes = await TransmitFile(fileStream, relativePath);

				manifestFiles.Add(new ManifestFileEntry
				{
					RelativePath = relativePath,
					Chunks = [..chunkHashes],
					FileSize = fileStream.Length
				});

				aggregatedChunkHashes.UnionWith(chunkHashes);
			}
		}

		var snapshotCreationTime = DateTimeOffset.UtcNow;

		var manifest = new SnapshotManifest
		{
			SnapshotTimestamp = snapshotCreationTime.ToUnixTimeSeconds(),
			JobId = _jobId,
			BasePath = _basePath,
			Files = [..manifestFiles]
		};

		DateTimeOffset expiresAt = snapshotCreationTime + _expiryPolicy.MaxAge.ToTimeSpan();

		for (int attempt = 0; attempt < MaxRetries; attempt++)
		{
			try
			{
				var (success, error) = await _client.EncryptTransmitSnapshotManifest(manifest, expiresAt, aggregatedChunkHashes);

				if (success) return;

				_logger.LogError("Failed to transmit snapshot manifest: {Error}", error);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Attempt {Attempt} to transmit snapshot manifest failed.", attempt+1);

				if (attempt == MaxRetries)
				{
					throw;
				}

				await Task.Delay(1000 * attempt); // backoff
			}
		}

	}

	async Task<uint> HashAndTransmitChunk(byte[] buffer, int chunkLength)
	{
		byte[] compressedBytes = [.._compressor.Wrap(buffer.AsSpan(0, chunkLength))];

		uint identityHash = CDCUtils.CalculateIdentityHash(buffer);

		if (await _client.ServerAlreadyHasChunk(identityHash))
		{
			return identityHash;
		}

		for (int attempt = 0; attempt < MaxRetries; attempt++)
		{
			try
			{
				await _client.EncryptAndTransmitChunk(compressedBytes, identityHash);
				return identityHash;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Attempt {Attempt} to transmit chunk {ChunkHash} failed.", attempt+1, identityHash);

				if (attempt == MaxRetries)
				{
					throw;
				}

				await Task.Delay(1000 * attempt); // backoff
			}
		}

		return identityHash;
	}

	async Task<List<uint>> TransmitFile(FileStream fileStream, string relativePath)
	{
		var chunkSizes = CDCUtils.CalculateChunkSizes(fileStream, _chunkSize.MinKB*1024, _chunkSize.MaxKB*1024);

		byte[] buffer = new byte[_chunkSize.MaxKB * 1024];

		List<uint> chunkHashes = [];

		foreach (var chunkSize in chunkSizes)
		{
			int bytesRead = fileStream.Read(buffer, 0, chunkSize);

			if (bytesRead <= 0)
			{
				throw new EndOfStreamException($"Unexpected end of stream while reading file {relativePath}");
			}

			chunkHashes.Add(await HashAndTransmitChunk(buffer, bytesRead));
		}

		return chunkHashes;
	}
}