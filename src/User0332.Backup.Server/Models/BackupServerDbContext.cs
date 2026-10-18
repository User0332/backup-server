using Microsoft.EntityFrameworkCore;

namespace User0332.Backup.Server.Models;

public class BackupServerDbContext(DbContextOptions<BackupServerDbContext> options) : DbContext(options)
{
	public DbSet<ChunkRecord> Chunks => Set<ChunkRecord>();
	public DbSet<SnapshotRecord> Snapshots => Set<SnapshotRecord>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ChunkRecord>().HasKey(c => c.IdentityHash);
		modelBuilder.Entity<SnapshotRecord>().HasKey(s => s.SnapshotId);
	}
}

public class ChunkRecord
{
	public required uint IdentityHash { get; set; } // the identity hash
	public required string FilePath { get; set; } = string.Empty;
	public required int RefCount { get; set; } // how many snapshots reference this chunk
	public ICollection<SnapshotRecord> Snapshots { get; set; } = [];
}

public class SnapshotRecord
{
	public required string SnapshotId { get; set; }
	public required string ClientId { get; set; }
	public required string JobId { get; set; }
	public ICollection<ChunkRecord> Chunks { get; set; } = [];
	public required byte[] EncryptedManifest { get; set; }
	public required DateTime CreatedAt { get; set; }
	public required DateTime ExpiresAt { get; set; }
}