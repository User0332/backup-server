using ZstdSharp;

namespace User0332.Backup.Shared.ClientOnly;

public class CompressionUtils
{
	static readonly Compressor _compressor = new();
	static readonly Decompressor _decompressor = new();

	public static byte[] CompressData(Span<byte> data)
	{
		return [.._compressor.Wrap(data)];
	}

	public static byte[] DecompressData(Span<byte> compressedData)
	{
		return [.._decompressor.Unwrap(compressedData)];
	}
}