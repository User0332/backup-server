using Grpc.Core;
using User0332.Backup.Server.Repositories;

namespace User0332.Backup.Server.Services;

public class BackupService(ILogger<BackupService> logger, IBackupRepository repository) : Backup.BackupBase
{
	public override async Task<ChunkExistsReply> ChunkExists(ChunkExistsRequest request, ServerCallContext context)
	{
		return new ChunkExistsReply
		{
			Exists = await repository.ChunkExistsAsync(request.Hash, context.CancellationToken)
		};
	}

	public override async Task<StoreChunkReply> StoreChunk(IAsyncStreamReader<StoreChunkRequest> requestStream, ServerCallContext context)
	{
		FileStream? fileStream = null;
		string targetDirectory = null!;

		try
		{
			while (await requestStream.MoveNext(context.CancellationToken))
			{
				var request = requestStream.Current;

				switch (request.PayloadCase)
				{
					case StoreChunkRequest.PayloadOneofCase.ChunkHash:
						logger.LogInformation("Storing chunk {hash}.", request.ChunkHash);

						string fileName = $"{request.ChunkHash}.chunk";

						fileStream = new FileStream(Path.Combine(targetDirectory, fileName), FileMode.Create, FileAccess.Write);

						break;
					case StoreChunkRequest.PayloadOneofCase.ChunkOfChunk:
						if (fileStream == null)
						{
							throw new InvalidOperationException("Received chunk data before receiving chunk hash.");
						}

						await fileStream.WriteAsync(request.ChunkOfChunk.Memory, context.CancellationToken);

						break;
				}
			}

			await repository.AddChunkAsync(new Models.ChunkRecord
			{
				IdentityHash = requestStream.Current.ChunkHash,
				FileName = $"{requestStream.Current.ChunkHash}.chunk",
				RefCount = 0
			}, context.CancellationToken);

			return new StoreChunkReply
			{
				Success = true
			};
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error storing chunk.");
			return new StoreChunkReply
			{
				Success = false,
				Error = ex.Message
			};
		}
		finally
		{
			if (fileStream != null)
			{
				await fileStream.DisposeAsync();
			}
		}
	}

	public override async Task<StoreSnapshotReply> StoreSnapshot(StoreSnapshotRequest request, ServerCallContext context)
	{
		var snapshotId = $"{request.ClientId}-{request.JobId}-{request.CreatedAt.ToDateTime():yyyyMMddHHmmss}";

		logger.LogInformation("Storing snapshot {snapshotId}.", snapshotId);

		var snapshotRecord = new Models.SnapshotRecord
		{
			SnapshotId = snapshotId,
			ClientId = request.ClientId,
			JobId = request.JobId,
			Chunks = await Task.WhenAll(
				request.Chunks.Select(
					async (hash) => await repository.GetChunkAsync(hash, context.CancellationToken)
				)
			),
			EncryptedManifest = request.EncryptedManifest.ToByteArray(),
			CreatedAt = request.CreatedAt.ToDateTimeOffset(),
			ExpiresAt = request.ExpiresAt.ToDateTimeOffset(),
		};

		await repository.StoreSnapshotAsync(snapshotRecord, context.CancellationToken);

		return new StoreSnapshotReply
		{
			Success = true
		};
	}
}
