using System.Security.Cryptography;

namespace User0332.Backup.Shared.ClientOnly;

public static class EncryptionUtils
{
	public static byte[] EncryptData(Span<byte> chunk, AesGcm aes)
	{
		byte[] nonce = new byte[12];
		RandomNumberGenerator.Fill(nonce);

		byte[] ciphertext = new byte[chunk.Length];
		byte[] tag = new byte[16];

		aes.Encrypt(nonce, chunk, ciphertext, tag);

		return [..nonce, ..ciphertext, ..tag];
	}

	public static byte[] DecryptData(Span<byte> encryptedChunk, AesGcm aes)
	{
		const int nonceLength = 12;
		const int tagLength = 16;

		if (encryptedChunk.Length < nonceLength + tagLength)
		{
			throw new ArgumentException("Encrypted chunk is too short to contain a valid nonce and tag.");
		}

		byte[] nonce = encryptedChunk[..nonceLength].ToArray();
		int ciphertextLength = encryptedChunk.Length - nonceLength - tagLength;
		byte[] ciphertext = encryptedChunk.Slice(nonceLength, ciphertextLength).ToArray();
		byte[] tag = encryptedChunk[(nonceLength + ciphertextLength)..].ToArray();

		byte[] plaintext = new byte[ciphertextLength];

		aes.Decrypt(nonce, ciphertext, tag, plaintext);

		return plaintext;
	}
}