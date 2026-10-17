using Microsoft.EntityFrameworkCore;

namespace User0332.Backup.Shared.ClientOnly.Models;

public class BackupClientDbContext(DbContextOptions<BackupClientDbContext> options) : DbContext(options)
{
	public DbSet<SnapshotRecord> Snapshots => Set<SnapshotRecord>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SnapshotRecord>().HasKey(s => s.SnapshotId);
	}
}

public class FileInfoAtSnapshotTime
{
	public required string FullPath { get; set; }
	public required DateTime LastModified { get; set; }
	public required List<(int Size, uint IdentityHash)> Chunks { get; set; }
}

public class SnapshotRecord
{
	public required string SnapshotId { get; set; }
	public ICollection<FileInfoAtSnapshotTime> Files { get; set; } = [];
}