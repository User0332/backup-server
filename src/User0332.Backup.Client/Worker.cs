using System.Text;
using Microsoft.Extensions.Options;

namespace User0332.Backup.Client;

public class Worker(ILogger<Worker> logger, IOptions<BackupClientOptions> options) : BackgroundService
{
	void LogConfiguration(BackupClientOptions config)
	{
		if (!logger.IsEnabled(LogLevel.Information)) return;

		StringBuilder sb = new();

		sb.AppendFormat("Client Name: {0}\n", config.ClientId);
		sb.AppendFormat("Server Address: {0}\n", config.ServerAddress);
		sb.AppendFormat("Encryption Key file: {0}\n", config.EncryptionKey);

		sb.AppendFormat("Found {0} jobs.\n", config.Jobs.Length);

		foreach (var job in config.Jobs)
		{
			sb.AppendFormat("Job '{0}':\n", job.JobId);
			sb.AppendFormat("  Base Path: {0}\n", job.BasePath);
			sb.AppendFormat("  File Patterns: {0}\n", string.Join(", ", job.FilePatterns));
			var interval = job.Interval;

			sb.AppendFormat("  Interval: {0} {1}\n", interval.Value, interval.Unit);

			if (job.ExpiryPolicy is not null)
			{
				if (job.ExpiryPolicy.MaxAge is not null)
				{
					var maxAge = job.ExpiryPolicy.MaxAge;

					sb.AppendFormat("  Expiry Policy: Max Age: {0} {1}\n", maxAge.Value, maxAge.Unit);
				}
				else
				{
					sb.AppendFormat("  Expiry Policy: Max Count: {0}\n", job.ExpiryPolicy.MaxCount);
				}
			}
			else
			{
				sb.Append("  Expiry Policy: None\n");
			}

			sb.AppendFormat("  Chunk Size Range: {0} KB - {1} KB\n", job.ChunkSize.MinKB, job.ChunkSize.MaxKB);
		}

		logger.LogInformation("Configuration:\n{config}", sb.ToString());
	}

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		LogConfiguration(options.Value);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
