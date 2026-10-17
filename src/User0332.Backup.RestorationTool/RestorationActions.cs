using Grpc.Net.Client;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly.Models;

namespace User0332.Backup.RestorationTool;

public class RestorationActions(BackupClientDbContext db, GrpcChannel channel, MinimalConfig config)
{
	public async Task<List<string>> ListSnapshots(string? clientIdFilter, string? jobIdFilter)
	{
		return [];
	}

	public async Task RestoreSnapshot(string snapshotId)
	{
		string stagingDirectory = Path.Combine(Path.GetTempPath(), "User0332.Backup.RestorationTool", snapshotId);

		SnapshotManifest manifest = null!; // download from server

		var snapshotRecord = await db.Snapshots.FindAsync(snapshotId);

		List<string> preservedFiles = [];
		Dictionary<uint, (string FileName, int Offset, int Size)> chunkFileMap = [];

		if (snapshotRecord == null)
		{
			Console.WriteLine($"Snapshot {snapshotId} not found in local database.");
		}
		else
		{
			foreach (var file in snapshotRecord.Files)
			{
				if (File.Exists(file.FullPath) && File.GetLastWriteTimeUtc(file.FullPath) == file.LastModified)
				{
					preservedFiles.Add(file.FullPath);

					int offset = 0;

					foreach (var (size, identityHash) in file.Chunks)
					{
						if (!chunkFileMap.ContainsKey(identityHash))
						{
							chunkFileMap[identityHash] = (file.FullPath, offset, size);
							offset += size;
						}
					}
				}
			}
		}

		foreach (var file in manifest.Files)
		{
			if (preservedFiles.Contains(Path.Combine(manifest.BasePath, file.RelativePath)))
			{
				Console.WriteLine($"Preserving file: {file.RelativePath}");
				continue;
			}

			using FileStream destStream = new(Path.Combine(stagingDirectory, file.RelativePath), FileMode.Create, FileAccess.Write);

			foreach (var chunkHash in file.Chunks)
			{
				if (chunkFileMap.TryGetValue(chunkHash, out var chunkInfo))
				{
					Console.WriteLine($"Reusing chunk {chunkHash} from {chunkInfo.FileName} for file {file.RelativePath}");
					// Logic to copy the chunk from the existing file to the new file

					using FileStream sourceStream = new(chunkInfo.FileName, FileMode.Open, FileAccess.Read);
					sourceStream.Seek(chunkInfo.Offset, SeekOrigin.Begin);

					sourceStream.CopyTo(destStream, chunkInfo.Size);
					sourceStream.Close();
				}
				else
				{
					Console.WriteLine($"Downloading chunk {chunkHash} for file {file.RelativePath}");


				}
			}

			destStream.Close();

			Console.WriteLine($"Restored file: {file.RelativePath}");
		}

		if (Directory.Exists(manifest.BasePath))
		{
			Console.WriteLine($"Copying existing directory {manifest.BasePath} to {manifest.BasePath}.old");

			if (Directory.Exists(manifest.BasePath + ".old"))
			{
				Console.WriteLine($"Deleting existing backup directory {manifest.BasePath}.old");
				Directory.Delete(manifest.BasePath + ".old", true);
			}

			Directory.Move(manifest.BasePath, manifest.BasePath + ".old");
		}

		Directory.Move(stagingDirectory, manifest.BasePath);

		Console.WriteLine($"Restoration of snapshot {snapshotId} completed successfully.");
		Console.WriteLine($"The original directory has been moved to {manifest.BasePath}.old.");
	}
}