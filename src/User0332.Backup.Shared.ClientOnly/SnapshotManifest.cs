using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace User0332.Backup.Shared;

public class SnapshotManifest
{
	public required string JobId { get; init; }
	public required long SnapshotTimestamp { get; init; }
	public required string BasePath { get; init; }
	public required ManifestFileEntry[] Files { get; init; }

	public string ToJson()
	{
		return JsonSerializer.Serialize(this);
	}

	public static SnapshotManifest FromJson(string json)
	{
		return JsonSerializer.Deserialize<SnapshotManifest>(json) ?? throw new InvalidOperationException("Failed to deserialize SnapshotManifest from JSON.");
	}

	public string GetSnapshotId(string clientId)
	{
		var manifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJson())));
		return $"{clientId}-{JobId}-{DateTime.UnixEpoch.AddSeconds(SnapshotTimestamp):O}-{manifestHash}";
	}
}

public class ManifestFileEntry
{
	public required string RelativePath { get; init; }
	public required long FileSize { get; init; }
	public required uint[] Chunks { get; init; }
}