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

	public async Task<ChunkRecord?> GetChunkAsync(uint identityHash, CancellationToken cancellationToken = default)
	{
		return await db.Chunks.FirstOrDefaultAsync(c => c.IdentityHash == identityHash, cancellationToken);
	}

	public async Task StoreSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken = default)
	{
		foreach (var chunk in snapshot.Chunks)
		{
			// recalcuate the reference count for each of these chunks to prevent some weird edge cases/race conditions

			chunk.RefCount = await db.Snapshots.CountAsync(s => s.Chunks.Any(c => c.IdentityHash == chunk.IdentityHash), cancellationToken);
			chunk.RefCount++; // increment the reference count for the new snapshot
		}

		db.Snapshots.Add(snapshot);

		await db.SaveChangesAsync(cancellationToken);
	}

	public async Task<List<SnapshotRecord>> ListSnapshotsAsync(string? clientIdFilter, string? jobIdFilter, CancellationToken cancellationToken = default)
	{
		var query = db.Snapshots
			.Include(s => s.Chunks)
			.AsQueryable();

		if (!string.IsNullOrEmpty(clientIdFilter))
		{
			query = query.Where(s => s.ClientId == clientIdFilter);
		}

		if (!string.IsNullOrEmpty(jobIdFilter))
		{
			query = query.Where(s => s.JobId == jobIdFilter);
		}

		return await query.ToListAsync(cancellationToken);
	}

	public async Task<SnapshotRecord?> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default)
	{
		return await db.Snapshots
			.Include(s => s.Chunks)
			.FirstOrDefaultAsync(s => s.SnapshotId == snapshotId, cancellationToken);
	}

	public async Task<DateTime?> GetNextSnapshotExpiryTime(CancellationToken cancellationToken = default)
	{
		return await db.Snapshots
			.OrderBy(s => s.ExpiresAt)
			.Select(s => (DateTime?)s.ExpiresAt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<List<string>> DeleteSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken = default)
	{
		List<string> filesToCleanup = [];

		foreach (var chunk in snapshot.Chunks)
		{
			var referenceCount = await db.Snapshots
				.Where(s => s.SnapshotId != snapshot.SnapshotId)
				.CountAsync(s => s.Chunks.Any(c => c.IdentityHash == chunk.IdentityHash), cancellationToken);

			chunk.RefCount = referenceCount;
			if (referenceCount == 0)
			{
				filesToCleanup.Add(chunk.FilePath);
				db.Chunks.Remove(chunk);
			}
		}

		db.Snapshots.Remove(snapshot);
		await db.SaveChangesAsync(cancellationToken);

		return filesToCleanup;
	}
}