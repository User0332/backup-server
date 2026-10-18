using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using User0332.Backup.Shared.ClientOnly.Configuration;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly;
using User0332.Backup.Shared.ClientOnly.Models;

namespace User0332.Backup.Client.JobExecution;

public class Job
{
	const int MaxRetries = 4;

	readonly string _clientId;
	public readonly string JobId;
	public readonly TimeSpan Interval;
	readonly string _basePath;
	readonly string[] _filePatterns;
	readonly ExpiryPolicy _expiryPolicy;
	readonly ChunkSizeConstraint _chunkSize;
	readonly RequestHelper _client;
	readonly Matcher _matcher = new();
	readonly ILogger<Worker> _logger;

	public Job(string clientId, JobOptions options, RequestHelper client, ILogger<Worker> logger)
	{
		_clientId = clientId;
		JobId = options.JobId;
		Interval = options.Interval.ToTimeSpan();
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
		Dictionary<ManifestFileEntry, DateTime> modifiedTimes = [];
		Dictionary<ManifestFileEntry, List<ChunkInfo>> chunkInfoMap = [];
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

				var writeTime = File.GetLastWriteTimeUtc(file);

				using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);

				var chunkInfo = await TransmitFile(fileStream, relativePath);

				var entry = new ManifestFileEntry
				{
					RelativePath = relativePath,
					Chunks = [..chunkInfo.Select(c => c.IdentityHash)],
					FileSize = fileStream.Length
				};

				manifestFiles.Add(entry);

				modifiedTimes[entry] = writeTime;
				chunkInfoMap[entry] = chunkInfo;

				aggregatedChunkHashes.UnionWith(entry.Chunks);
			}
		}

		var snapshotCreationTime = DateTime.UtcNow;

		var manifest = new SnapshotManifest
		{
			SnapshotTimestamp = (snapshotCreationTime - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond,
			JobId = JobId,
			BasePath = _basePath,
			Files = [..manifestFiles],
		};

		DateTime expiresAt = snapshotCreationTime + _expiryPolicy.MaxAge.ToTimeSpan();

		for (int attempt = 0; attempt < MaxRetries; attempt++)
		{
			try
			{
				var (success, error) = await _client.EncryptAndTransmitSnapshotManifest(manifest, expiresAt, aggregatedChunkHashes);

				if (success)
				{
					await StoreSnapshotDataInDatabase(manifest, modifiedTimes, chunkInfoMap);
					return;
				}

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

	async Task StoreSnapshotDataInDatabase(SnapshotManifest manifest, Dictionary<ManifestFileEntry, DateTime> modifiedTimes, Dictionary<ManifestFileEntry, List<ChunkInfo>> chunkInfoMap)
	{
		var db = new BackupClientDbContextFactory().CreateDbContext([]);

		var snapshot = new SnapshotRecord
		{
			SnapshotId = manifest.GetSnapshotId(_clientId),
			Files = [..manifest.Files.Select(f => new FileInfoAtSnapshotTime
			{
				FullPath = Path.Combine(manifest.BasePath, f.RelativePath),
				LastModified = modifiedTimes[f],
				Chunks = chunkInfoMap[f]
			})],
		};

		db.Snapshots.Add(snapshot);

		await db.SaveChangesAsync();
	}

	async Task<ChunkInfo> HashAndTransmitChunk(byte[] buffer, int chunkLength)
	{
		byte[] compressedBytes = CompressionUtils.CompressData(buffer.AsSpan()[..chunkLength]);

		uint identityHash = CDCUtils.CalculateIdentityHash(buffer);

		if (await _client.ServerAlreadyHasChunk(identityHash))
		{
			return new ChunkInfo { Size = chunkLength, IdentityHash = identityHash };
		}

		for (int attempt = 0; attempt < MaxRetries; attempt++)
		{
			try
			{
				await _client.EncryptAndTransmitChunk(compressedBytes, identityHash);
				return new ChunkInfo { Size = chunkLength, IdentityHash = identityHash };
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

		return new ChunkInfo { Size = chunkLength, IdentityHash = identityHash };
	}

	async Task<List<ChunkInfo>> TransmitFile(FileStream fileStream, string relativePath)
	{
		var chunkSizes = CDCUtils.CalculateChunkSizes(fileStream, _chunkSize.MinKB*1024, _chunkSize.MaxKB*1024);

		fileStream.Seek(0, SeekOrigin.Begin);

		byte[] buffer = new byte[_chunkSize.MaxKB * 1024];

		List<ChunkInfo> chunkInfo = [];

		foreach (var chunkSize in chunkSizes)
		{
			int bytesRead = fileStream.Read(buffer, 0, chunkSize);

			if (bytesRead <= 0)
			{
				throw new EndOfStreamException($"Unexpected end of stream while reading file {relativePath}");
			}

			chunkInfo.Add(await HashAndTransmitChunk(buffer, bytesRead));
		}

		return chunkInfo;
	}
}