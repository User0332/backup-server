namespace User0332.Backup.Shared;

public static class Constants
{
	public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "User0332.Backup.Client");
	public const string BackupClientConfigFileName = "backup-config.json";
	public const string BackupClientConfigSchemaFileName = "config-schema.json";
	public static readonly string BackupClientConfigFilePath = Path.Combine(DataDirectory, BackupClientConfigFileName);
	public static readonly string BackupClientConfigSchemaFilePath = Path.Combine(DataDirectory, BackupClientConfigSchemaFileName);

	public const string DatabaseFileName = "backup_client.db";
	public static readonly string DatabaseFilePath = Path.Combine(DataDirectory, DatabaseFileName);

	public const string JobQueueFileName = "jobqueue.txt";
	public static readonly string JobQueueFilePath = Path.Combine(DataDirectory, JobQueueFileName);
}