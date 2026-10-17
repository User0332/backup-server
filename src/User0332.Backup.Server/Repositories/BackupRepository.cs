using Microsoft.EntityFrameworkCore;
using User0332.Backup.Server.Models;

namespace User0332.Backup.Server.Repositories;

public class BackupRepository(BackupServerDbContext db) : IBackupRepository
{
	public async Task<bool> ChunkExistsAsync(uint identityHash, CancellationToken cancellationToken = default)
	{
		return await db.Chunks.AnyAsync(c => c.IdentityHash == identityHash, cancellationToken);
	}

	public async Task AddChunkAsync(ChunkRecord chunk, CancellationToken cancellationToken = default)
	{
		db.Chunks.Add(chunk);
		await db.SaveChangesAsync(cancellationToken);
	}

	public async Task<ChunkRecord> GetChunkAsync(uint identityHash, CancellationToken cancellationToken = default)
	{
		return await db.Chunks.FirstAsync(c => c.IdentityHash == identityHash, cancellationToken);
	}

	public async Task StoreSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken = default)
	{
		db.Snapshots.Add(snapshot);

		foreach (var chunk in snapshot.Chunks)
		{
			chunk.RefCount++;
		}

		await db.SaveChangesAsync(cancellationToken);
	}
}