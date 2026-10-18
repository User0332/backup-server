namespace User0332.Backup.Client.JobExecution;

public class ExpiryInfo
{
	public long? ExpiryTimestamp { get; init; }
	public int? MaxSnapshots { get; init; }

	public byte[] Serialize()
	{
		if (ExpiryTimestamp is not null)
		{
			return [0, ..BitConverter.GetBytes(ExpiryTimestamp.Value)];
		}

		return [1, ..BitConverter.GetBytes(MaxSnapshots!.Value)];
	}

	public static (ExpiryInfo Info, int BytesConsumed) Deserialize(byte[] data)
	{
		if (data[0] == 0)
		{
			long expiryTimestamp = BitConverter.ToInt64(data, 1);
			return (new ExpiryInfo { ExpiryTimestamp = expiryTimestamp }, 9);
		}
		else if (data[0] == 1)
		{
			int maxSnapshots = BitConverter.ToInt32(data, 1);
			return (new ExpiryInfo { MaxSnapshots = maxSnapshots }, 5);
		}

		throw new ArgumentException("Invalid data for ExpiryInfo deserialization.");
	}
}