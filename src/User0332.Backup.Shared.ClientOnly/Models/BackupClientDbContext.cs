using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace User0332.Backup.Shared.ClientOnly.Models;

public class BackupClientDbContext(DbContextOptions<BackupClientDbContext> options) : DbContext(options)
{
	public DbSet<SnapshotRecord> Snapshots => Set<SnapshotRecord>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
        var filesConverter = new ValueConverter<List<FileInfoAtSnapshotTime>, string>(
            files => JsonSerializer.Serialize(files, (JsonSerializerOptions?) null),
            json => JsonSerializer.Deserialize<List<FileInfoAtSnapshotTime>>(json, (JsonSerializerOptions?) null)
                ?? new List<FileInfoAtSnapshotTime>());

        modelBuilder.Entity<SnapshotRecord>(entity =>
        {
            entity.HasKey(s => s.SnapshotId);

            entity.Property(s => s.Files)
                .HasConversion(filesConverter)
                .HasColumnType("TEXT");
        });
	}
}

public class ChunkInfo
{
	public required int Size { get; set; }
	public required uint IdentityHash { get; set; }
}

public class FileInfoAtSnapshotTime
{
	public required string FullPath { get; set; }
	public required DateTime LastModified { get; set; }
	public required List<ChunkInfo> Chunks { get; set; }
}

public class SnapshotRecord
{
	public required string SnapshotId { get; set; }
	public List<FileInfoAtSnapshotTime> Files { get; set; } = [];
}