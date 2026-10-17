using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

namespace User0332.Backup.Client.JobExecution;

static class CDCUtils
{
	const int DefaultGearMask = 0x3FFF; // 14 bits for average chunk size of 16KB
	const int ReadBufferSize = 64 * 1024; // 64KB read buffer to avoid virtual dispatch overhead for each byte read
	static readonly ulong[] GearTable = InitGearTable();

	static ulong[] InitGearTable()
	{
		var table = new ulong[256];
		var random = new Random(42);

		for (int i = 0; i < 256; i++)
		{
			table[i] = (ulong) random.NextInt64();
		}

		return table;
	}

	static ulong UpdateGearHash(ulong hash, byte newByte)
	{
		return (hash << 1) + GearTable[newByte];
	}

	public static uint CalculateIdentityHash(Span<byte> buffer)
	{
		var res = SHA256.HashData(buffer);

		return BitConverter.ToUInt32(res, 0);
	}

	public static List<int> CalculateChunkSizes(Stream dataStream, int minChunkSize, int maxChunkSize, ulong gearMask = DefaultGearMask)
	{
		if (minChunkSize <= 0 || maxChunkSize <= 0 || minChunkSize > maxChunkSize)
		{
			throw new ArgumentException("Invalid chunk size parameters.");
		}

		List<int> boundaries = [];
		int currentChunkSize = 0;
		ulong gearHash = 0;

		byte[] buffer = new byte[ReadBufferSize];
		int bufferIndex = 0;
		int bufferLength = 0;

		while (true)
		{
			// fill read buffer
			if (bufferIndex >= bufferLength)
			{
				bufferLength = dataStream.Read(buffer, 0, buffer.Length);
				bufferIndex = 0;

				if (bufferLength == 0)
					break;
			}

			// skip until minChunkSize-64 to avoid computing hashes for bytes that will never be relevant
			int targetFastForward = minChunkSize - 64;
			if (currentChunkSize < targetFastForward)
			{
				int jumpAmount = Math.Min(bufferLength - bufferIndex, targetFastForward - currentChunkSize);

				bufferIndex += jumpAmount;
				currentChunkSize += jumpAmount;

				if (currentChunkSize < targetFastForward)
					continue;
			}

			// calculate the hash for 64 bytes until we reach the minChunkSize
			if (currentChunkSize < minChunkSize)
			{
				gearHash = UpdateGearHash(gearHash, buffer[bufferIndex++]);
				currentChunkSize++;
				continue;
			}

			// check for chunk boundary
			gearHash = UpdateGearHash(gearHash, buffer[bufferIndex++]);
			currentChunkSize++;

			if (currentChunkSize >= maxChunkSize || (gearHash & gearMask) == 0)
			{
				boundaries.Add(currentChunkSize);

				currentChunkSize = 0;
				gearHash = 0;
			}
		}

		// all trailing bytes form the last chunk
		if (currentChunkSize > 0)
		{
			boundaries.Add(currentChunkSize);
		}

		return boundaries;
	}
}