namespace User0332.Backup.Client.Configuration;

public class JobOptions
{
	public required string JobId { get; set; }
	public required string BasePath { get; set; }
	public required string[] FilePatterns { get; set; }
	public required TimeConfiguration Interval { get; set; }
	public ExpiryPolicy? ExpiryPolicy { get; set; }
	public ChunkSizeConstraint ChunkSize { get; set; } = new();
}