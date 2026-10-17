using User0332.Backup.Server.Models;

namespace User0332.Backup.Server.Repositories;

public interface IBackupRepository
{
	Task<bool> ChunkExistsAsync(uint identityHash, CancellationToken cancellationToken = default);
	public Task AddChunkAsync(ChunkRecord chunk, CancellationToken cancellationToken = default);
	public Task<ChunkRecord> GetChunkAsync(uint identityHash, CancellationToken cancellationToken = default);
	public Task StoreSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken = default);
}