using System.Text;
using User0332.Backup.Client.Configuration;

namespace User0332.Backup.Client.JobExecution;

public class Client(BackupClientOptions options)
{
	readonly string _clientId = options.ClientId;
	readonly string _serverAddress = options.ServerAddress;
	readonly string _encryptionKey = options.EncryptionKey;

	public void EncryptTransmitSnapshotManifest(SnapshotManifest manifest, ExpiryInfo expiryInfo, List<uint> aggregatedChunkList)
	{
		byte[] manifestSerialized = Encoding.UTF8.GetBytes(manifest.ToJson());
		byte[] expiryInfoSerialized = expiryInfo.Serialize();
		byte[] aggregatedChunkListSerialized = [..BitConverter.GetBytes(aggregatedChunkList.Count).Concat(
			aggregatedChunkList.SelectMany(BitConverter.GetBytes)
		)];

		// send to server...
	}

	public bool ServerAlreadyHasChunk(uint identityHash)
	{
		// check with server...
		return false;
	}

	public void EncryptAndTransmitChunk(Span<byte> compressedChunk, uint identityHash)
	{
		// send to server...
	}
}