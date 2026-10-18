using System.Security.Cryptography;
using Grpc.Net.Client;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly;
using User0332.Backup.Shared.ClientOnly.Models;

namespace User0332.Backup.RestorationTool;

public class RestorationActions(BackupClientDbContext db, GrpcChannel channel, MinimalConfig config)
{
	const int MaxFetchRetries = 4;
	readonly Restore.RestoreClient _client = new(channel);
	readonly AesGcm	_aes = new(File.ReadAllBytes(Path.Combine(Constants.DataDirectory, config.EncryptionKey)), 16);

	public async Task<List<string>> ListSnapshots(string? clientIdFilter, string? jobIdFilter)
	{
		var req = new ListSnapshotsRequest
		{
			ClientId = clientIdFilter ?? string.Empty,
			JobId = jobIdFilter ?? string.Empty
		};

		var resp = await _client.ListSnapshotsAsync(req);

		return [.. resp.SnapshotIds];
	}

	public async Task RestoreSnapshot(string snapshotId)
	{
		string stagingDirectory = Path.Combine(Path.GetTempPath(), "User0332.Backup.RestorationTool", snapshotId);
		Directory.CreateDirectory(stagingDirectory);

		SnapshotManifest manifest = null!;

		for (int attempt = 0; attempt < MaxFetchRetries; attempt++)
		{
			try
			{
				var req = new FetchManifestRequest { SnapshotId = snapshotId };
				var resp = await _client.FetchManifestAsync(req);

				if (!resp.Success)
				{
					Console.WriteLine($"Failed to fetch manifest for snapshot {snapshotId}.");
					continue;
				}

				byte[] decrypted = EncryptionUtils.DecryptChunk(resp.Manifest.ToByteArray(), _aes);

				string manifestJson = System.Text.Encoding.UTF8.GetString(decrypted);
				manifest = SnapshotManifest.FromJson(manifestJson);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Attempt {attempt + 1} to fetch manifest failed: {ex.Message}");
				await Task.Delay(1000 * attempt); // backoff
			}
		}

		if (manifest == null)
		{
			Console.WriteLine($"Failed to fetch manifest for snapshot {snapshotId} after {MaxFetchRetries} attempts.");
			return;
		}

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

					foreach (var chunkInfo in file.Chunks)
					{
						var size = chunkInfo.Size;
						var identityHash = chunkInfo.IdentityHash;

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
			int filePos = 0;

			if (preservedFiles.Contains(Path.Combine(manifest.BasePath, file.RelativePath)))
			{
				Console.WriteLine($"Preserving file: {file.RelativePath}");
				continue;
			}

			string destPath = Path.Combine(stagingDirectory, file.RelativePath);

			using FileStream destStream = new(destPath, FileMode.Create, FileAccess.Write);

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

					filePos += chunkInfo.Size;
				}
				else
				{
					Console.WriteLine($"Downloading chunk {chunkHash} for file {file.RelativePath}");

					int chunkStartOffset = filePos;

					for (int i = 0; i < MaxFetchRetries; i++)
					{
						try
						{
							var request = new DownloadChunkRequest { ChunkHash = chunkHash };
							var response = _client.DownloadChunk(request);

							while (await response.ResponseStream.MoveNext(CancellationToken.None))
							{
								var chunkData = response.ResponseStream.Current.ChunkOfChunk.ToByteArray();
								byte[] decryptedChunk = EncryptionUtils.DecryptChunk(chunkData, _aes);
								destStream.Write(decryptedChunk, 0, decryptedChunk.Length);

								filePos += decryptedChunk.Length;
							}
						}
						catch (Exception ex)
						{
							Console.WriteLine($"Attempt {i + 1} to download chunk {chunkHash} failed: {ex.Message}");
							await Task.Delay(1000 * i); // backoff
							continue;
						}
					}

					int chunkSize = filePos - chunkStartOffset;

					chunkFileMap[chunkHash] = (destPath, chunkStartOffset, chunkSize);
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