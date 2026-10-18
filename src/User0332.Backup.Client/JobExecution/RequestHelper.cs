using System.Security.Cryptography;
using System.Text;
using Grpc.Net.Client;
using User0332.Backup.Client.Configuration;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly;

namespace User0332.Backup.Client.JobExecution;

public class RequestHelper(BackupClientOptions options, GrpcChannel channel)
{
	readonly string _clientId = options.ClientId;
	readonly AesGcm _aes = new(File.ReadAllBytes(Path.Combine(Constants.DataDirectory, options.EncryptionKey)), 16);
	readonly Shared.Backup.BackupClient _client = new(channel);

	const int MaxTransmitChunkSize = 64 * 1024; // 64 KB

	public async Task<(bool Success, string Error)> EncryptAndTransmitSnapshotManifest(SnapshotManifest manifest, DateTime expiresAt, HashSet<uint> aggregatedChunkList)
	{
		byte[] manifestSerialized = Encoding.UTF8.GetBytes(manifest.ToJson());

		byte[] manifestCompressed = CompressionUtils.CompressData(manifestSerialized);

		var encryptedManifest = EncryptionUtils.EncryptData(manifestCompressed, _aes);
		var manifestTransitHash = TransitIntegrityUtils.CalculateTransitHash(encryptedManifest);

		// send to server...
		var req = new StoreSnapshotRequest
		{
			SnapshotId = manifest.GetSnapshotId(_clientId),
			ClientId = _clientId,
			JobId = manifest.JobId,
			ManifestTransitHash = manifestTransitHash,
			EncryptedManifest = Google.Protobuf.ByteString.CopyFrom(encryptedManifest),
			ExpiresAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
			CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UnixEpoch.AddSeconds(manifest.SnapshotTimestamp)),
		};

		req.Chunks.AddRange(aggregatedChunkList);

		var response = await _client.StoreSnapshotAsync(req);

		return (response.Success, response.Error);
	}

	public async Task<bool> ServerAlreadyHasChunk(uint identityHash)
	{
		var req = new ChunkExistsRequest
		{
			Hash = identityHash
		};

		var response = await _client.ChunkExistsAsync(req);

		return response.Exists;
	}

	public async Task<(bool Success, string Error)> EncryptAndTransmitChunk(byte[] compressedChunk, uint identityHash)
	{
		byte[] encryptedChunk = EncryptionUtils.EncryptData(compressedChunk, _aes);

		using var req = _client.StoreChunk();

		await req.RequestStream.WriteAsync(new StoreChunkRequest
		{
			ChunkHash = identityHash
		});

		for (int offset = 0; offset < encryptedChunk.Length; offset += MaxTransmitChunkSize)
		{
			int chunkSize = Math.Min(MaxTransmitChunkSize, encryptedChunk.Length - offset);
			var chunkSlice = encryptedChunk.AsSpan(offset, chunkSize);
			var transitHash = TransitIntegrityUtils.CalculateTransitHash(chunkSlice);

			await req.RequestStream.WriteAsync(new StoreChunkRequest
			{
				ChunkOfChunk = new ChunkOfChunk
				{
					Data = Google.Protobuf.ByteString.CopyFrom(chunkSlice.ToArray()),
					TransitHash = transitHash
				},

			});
		}

		await req.RequestStream.CompleteAsync();

		var resp = await req.ResponseAsync;

		return (resp.Success, resp.Error);
	}
}