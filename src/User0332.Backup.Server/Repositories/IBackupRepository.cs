using User0332.Backup.Server.Models;

namespace User0332.Backup.Server.Repositories;

public interface IBackupRepository
{
	Task<bool> ChunkExistsAsync(uint identityHash, CancellationToken cancellationToken = default);
	public Task AddChunkAsync(ChunkRecord chunk, CancellationToken cancellationToken = default);
	public Task<ChunkRecord?> GetChunkAsync(uint identityHash, CancellationToken cancellationToken = default);
	public Task StoreSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken = default);
	public Task<SnapshotRecord?> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default);
	public Task<List<SnapshotRecord>> ListSnapshotsAsync(string? clientIdFilter, string? jobIdFilter, CancellationToken cancellationToken = default);
	public Task<DateTimeOffset?> GetNextSnapshotExpiryTime(CancellationToken cancellationToken = default);
	public Task<List<string>> DeleteSnapshotAsync(SnapshotRecord snapshotId, CancellationToken cancellationToken = default);
}