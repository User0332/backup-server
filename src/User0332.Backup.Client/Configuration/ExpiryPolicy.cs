namespace User0332.Backup.Client.Configuration;

public class ExpiryPolicy
{
	public TimeConfiguration? MaxAge { get; set; }
	public int? MaxCount { get; set; } = 0;
}