using Grpc.Core;
using User0332.Backup.Server.Repositories;
using User0332.Backup.Shared;

namespace User0332.Backup.Server.Services;

public class BackupService(ILogger<BackupService> logger, ServerLockManager lockManager, IConfiguration config, IServiceProvider serviceProvider) : Shared.Backup.BackupBase
{
	readonly string TargetDirectory = config["BackupStoragePath"] ?? throw new InvalidOperationException("BackupStoragePath is not configured.");
	IBackupRepository Repository => serviceProvider.GetRequiredService<IBackupRepository>();

	public override async Task<ChunkExistsReply> ChunkExists(ChunkExistsRequest request, ServerCallContext context)
	{
		return new ChunkExistsReply
		{
			Exists = await Repository.ChunkExistsAsync(request.Hash, context.CancellationToken)
		};
	}

	public override async Task<StoreChunkReply> StoreChunk(IAsyncStreamReader<StoreChunkRequest> requestStream, ServerCallContext context)
	{
		FileStream? fileStream = null;
		uint chunkHash = 0;

		try
		{
			while (await requestStream.MoveNext(context.CancellationToken))
			{
				var request = requestStream.Current;

				switch (request.PayloadCase)
				{
					case StoreChunkRequest.PayloadOneofCase.ChunkHash:
						logger.LogInformation("Storing chunk {hash}.", request.ChunkHash);

						string fileName = $"{chunkHash}.chunk";

						fileStream = new FileStream(Path.Combine(TargetDirectory, fileName), FileMode.Create, FileAccess.Write);

						break;
					case StoreChunkRequest.PayloadOneofCase.ChunkOfChunk:
						if (fileStream == null)
						{
							return new StoreChunkReply
							{
								Success = false,
								Error = "ChunkHash must be sent before ChunkOfChunk."
							};
						}

						var hash = TransitIntegrityUtils.CalculateTransitHash(request.ChunkOfChunk.Data.Span);

						if (hash != request.ChunkOfChunk.TransitHash)
						{
							return new StoreChunkReply
							{
								Success = false,
								Error = "Transit hash mismatch."
							};
						}

						await fileStream.WriteAsync(request.ChunkOfChunk.Data.Memory, context.CancellationToken);

						break;
				}
			}

			using (await lockManager.AcquireDatabaseLockAsync(context.CancellationToken))
			{
				await Repository.AddChunkAsync(new Models.ChunkRecord
				{
					IdentityHash = chunkHash,
					FilePath = fileStream!.Name,
					RefCount = -1 // placeholder, when a snapshot is stored, the refcount will be updated to the correct value
				}, context.CancellationToken);
			}

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
		logger.LogInformation("Storing snapshot {snapshotId}.", request.SnapshotId);

		var transitHash = TransitIntegrityUtils.CalculateTransitHash(request.EncryptedManifest.Span);

		if (transitHash != request.ManifestTransitHash)
		{
			return new StoreSnapshotReply
			{
				Success = false,
				Error = "Manifest transit hash mismatch."
			};
		}

		using (await lockManager.AcquireDatabaseLockAsync(context.CancellationToken))
		{
			var snapshotRecord = new Models.SnapshotRecord
			{
				SnapshotId = request.SnapshotId,
				ClientId = request.ClientId,
				JobId = request.JobId,
				Chunks = await Task.WhenAll(
					request.Chunks.Select(
						async (hash) => {
							var res = await Repository.GetChunkAsync(hash, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, $"Chunk {hash} not found."));
							return res;
						}
					)
				),
				EncryptedManifest = request.EncryptedManifest.ToByteArray(),
				CreatedAt = request.CreatedAt.ToDateTimeOffset(),
				ExpiresAt = request.ExpiresAt.ToDateTimeOffset(),
			};

			await Repository.StoreSnapshotAsync(snapshotRecord, context.CancellationToken);
		}

		return new StoreSnapshotReply
		{
			Success = true
		};
	}
}
