namespace User0332.Backup.Client;

public class ExpiryPolicy
{
	public TimeConfiguration? MaxAge { get; set; }
	public int? MaxCount { get; set; } = 0;
}