namespace User0332.Backup.Shared;

public static class Constants
{
	public static readonly string DataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
	public const string BackupClientConfigFileName = "backup-config.json";
	public static readonly string BackupClientConfigFilePath = Path.Combine(DataDirectory, "User0332.Backup.Client", BackupClientConfigFileName);
	public const string DatabaseFileName = "backup_client.db";
	public static readonly string DatabaseFilePath = Path.Combine(DataDirectory, "User0332.Backup.Client", DatabaseFileName);
}