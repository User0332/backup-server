namespace User0332.Backup.Client;

public class BackupClientOptions
{
	public required string ClientId { get; set; }
	public required string ServerAddress { get; set; }
	public required string EncryptionKey { get; set; }
	public required JobOptions[] Jobs { get; set; }
}