using Microsoft.Extensions.FileSystemGlobbing;
using User0332.Backup.Client.Configuration;
using ZstdSharp;

namespace User0332.Backup.Client.JobExecution;

public class Job
{
	readonly string _jobId;
	readonly string _basePath;
	readonly string[] _filePatterns;
	readonly ChunkSizeConstraint _chunkSize;
	readonly Client _client;
	readonly Compressor _compressor = new();
	readonly Matcher _matcher = new();

	public Job(JobOptions options, Client client)
	{
		_jobId = options.JobId;
		_basePath = options.BasePath;
		_filePatterns = [..options.FilePatterns];
		_chunkSize = options.ChunkSize;
		_client = client;

		_matcher.AddIncludePatterns(_filePatterns);
	}

	public void Execute()
	{
		TransmitAllFiles();
	}

	void TransmitAllFiles()
	{
		foreach (var pattern in _filePatterns)
		{
			var files = Directory.GetFiles(_basePath, pattern, SearchOption.TopDirectoryOnly);

			foreach (var file in files)
			{
				string relativePath = Path.GetRelativePath(_basePath, file);

				using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);

				TransmitFile(fileStream, relativePath);
			}
		}
	}

	void HashAndTransmitChunk(byte[] buffer, int chunkLength)
	{
		var compressedBytes = _compressor.Wrap(buffer.AsSpan(0, chunkLength));

		uint identityHash = CDCUtils.CalculateIdentityHash(buffer);

		if (_client.ServerAlreadyHasChunk(identityHash))
		{
			return;
		}

		_client.EncryptAndTransmitChunk(compressedBytes, identityHash);
	}

	void TransmitFile(FileStream fileStream, string relativePath)
	{
		var chunkSizes = CDCUtils.CalculateChunkSizes(fileStream, _chunkSize.MinKB*1024, _chunkSize.MaxKB*1024);

		byte[] buffer = new byte[_chunkSize.MaxKB * 1024];

		foreach (var chunkSize in chunkSizes)
		{
			int bytesRead = fileStream.Read(buffer, 0, chunkSize);

			if (bytesRead <= 0)
			{
				throw new EndOfStreamException($"Unexpected end of stream while reading file {relativePath}");
			}
		}
	}
}