using System.Security.Cryptography;

namespace User0332.Backup.Shared;

public static class TransitIntegrityUtils
{
	public static uint CalculateTransitHash(ReadOnlySpan<byte> data)
	{
		byte[] hash = SHA256.HashData(data);
		return BitConverter.ToUInt32(hash, 0);
	}
}