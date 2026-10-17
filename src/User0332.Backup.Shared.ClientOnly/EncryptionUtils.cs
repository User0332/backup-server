using System.Security.Cryptography;

namespace User0332.Backup.Shared.ClientOnly;

public static class EncryptionUtils
{
	public static byte[] EncryptChunk(Span<byte> chunk, AesGcm aes)
	{
		byte[] nonce = new byte[12];
		RandomNumberGenerator.Fill(nonce);

		byte[] ciphertext = new byte[chunk.Length];
		byte[] tag = new byte[16];

		aes.Encrypt(nonce, chunk, ciphertext, tag);

		return [..ciphertext, ..tag];
	}

	public static byte[] DecryptChunk(Span<byte> encryptedChunk, AesGcm aes)
	{
		if (encryptedChunk.Length < 16)
		{
			throw new ArgumentException("Encrypted chunk is too short to contain a valid tag.");
		}

		int ciphertextLength = encryptedChunk.Length - 16;
		byte[] ciphertext = encryptedChunk[..ciphertextLength].ToArray();
		byte[] tag = encryptedChunk[ciphertextLength..].ToArray();

		byte[] plaintext = new byte[ciphertextLength];

		aes.Decrypt(new byte[12], ciphertext, tag, plaintext);

		return plaintext;
	}
}