using Google.Protobuf;
using Grpc.Core;
using User0332.Backup.Server.Repositories;
using User0332.Backup.Shared;

namespace User0332.Backup.Server.Services;

public class RestoreService(ILogger<RestoreService> logger, IBackupRepository repository) : Restore.RestoreBase
{
	const int StreamingBufferSize = 64*1024; // 64 KB

	public override async Task<ListSnapshotsReply> ListSnapshots(ListSnapshotsRequest request, ServerCallContext context)
	{
		var snapshots = await repository.ListSnapshotsAsync(request.ClientId, request.JobId, context.CancellationToken);

		return new ListSnapshotsReply
		{
			SnapshotIds = { snapshots.Select(s => s.SnapshotId) }
		};
	}

	public override async Task<FetchManifestReply> FetchManifest(FetchManifestRequest request, ServerCallContext context)
	{
		var snapshot = await repository.GetSnapshotAsync(request.SnapshotId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, $"Snapshot {request.SnapshotId} not found."));

		return new FetchManifestReply
		{
			Manifest = ByteString.CopyFrom(snapshot.EncryptedManifest)
		};
	}

	public override async Task DownloadChunk(DownloadChunkRequest request, IServerStreamWriter<DownloadChunkReply> responseStream, ServerCallContext context)
	{
		var chunk = await repository.GetChunkAsync(request.ChunkHash, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, $"Chunk {request.ChunkHash} not found."));

		FileStream chunkStream = new(chunk.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

		logger.LogInformation("Streaming chunk {ChunkHash} to client {client}", request.ChunkHash, context.Peer);

		while (true)
		{
			byte[] buffer = new byte[StreamingBufferSize];
			int bytesRead = await chunkStream.ReadAsync(buffer, context.CancellationToken);

			if (bytesRead == 0)
				break;

			await responseStream.WriteAsync(new DownloadChunkReply
			{
				ChunkOfChunk = ByteString.CopyFrom(buffer, 0, bytesRead)
			});
		}
	}
}