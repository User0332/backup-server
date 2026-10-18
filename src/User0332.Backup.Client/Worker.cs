using System.Text;
using Microsoft.Extensions.Options;
using User0332.Backup.Client.Configuration;

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

			sb.AppendFormat("  Expiry Policy: Max Age: {0} {1}\n", job.ExpiryPolicy.MaxAge.Value, job.ExpiryPolicy.MaxAge.Unit);

			sb.AppendFormat("  Chunk Size Range: {0} KB - {1} KB\n", job.ChunkSize.MinKB, job.ChunkSize.MaxKB);
		}

		logger.LogInformation("Configuration:\n{config}", sb.ToString());
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogConfiguration(options.Value);

		PriorityQueue<JobOptions, DateTime> jobQueue = new();

		string[] jobQueueFileLines;

		if (File.Exists("jobqueue.txt") && (jobQueueFileLines = File.ReadAllLines("jobqueue.txt")).Length == options.Value.Jobs.Length)
		{
			foreach (var line in jobQueueFileLines)
			{
				var parts = line.Split('$');

				if (parts.Length != 2)
				{
					logger.LogWarning("Invalid job queue entry: {line}", line);
					continue;
				}

				var jobId = parts[0];
				var executionTimeStr = parts[1];

				if (!DateTime.TryParse(executionTimeStr, out var executionTime))
				{
					logger.LogWarning("Invalid execution time for job '{jobId}': {executionTimeStr}", jobId, executionTimeStr);
					continue;
				}

				var job = options.Value.Jobs.FirstOrDefault(j => j.JobId == jobId);

				if (job is null)
				{
					logger.LogWarning("Job '{jobId}' not found in configuration", jobId);
					continue;
				}

				jobQueue.Enqueue(job, executionTime);
			}
		}
		else
		{
			string[] lines = new string[options.Value.Jobs.Length];

			for (int i = 0; i < options.Value.Jobs.Length; i++)
			{
				var job = options.Value.Jobs[i];
				var nextExecutionTime = DateTime.UtcNow + job.Interval.ToTimeSpan();

				jobQueue.Enqueue(job, nextExecutionTime);
				lines[i] = $"{job.JobId}${nextExecutionTime}";
			}

			File.WriteAllLines("jobqueue.txt", lines);
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			if (jobQueue.TryPeek(out var pendingJob, out var executionTime))
			{
				var now = DateTime.UtcNow;

				logger.LogInformation("Next job '{jobId}' scheduled for {executionTime}, current time is {now}", pendingJob.JobId, executionTime, now);

				if (executionTime <= now)
				{
					jobQueue.Dequeue();

					logger.LogInformation("Executing job '{jobId}' at {time}", pendingJob.JobId, now);

					// execute job


					// reschedule job
					var newExecutionTime = now + pendingJob.Interval.ToTimeSpan();

					jobQueue.Enqueue(pendingJob, newExecutionTime);

					// update job queue file
					var newLines = File.ReadAllLines("jobqueue.txt");

					var lineIndex = Array.FindIndex(newLines, l => l.StartsWith(pendingJob.JobId + '$'));

					if (lineIndex >= 0)
					{
						newLines[lineIndex] = $"{pendingJob.JobId}${newExecutionTime}";
					}

					File.WriteAllLines("jobqueue.txt", newLines);
				}
				else
				{
					int msTillNextJob = (int) (executionTime - now).TotalMilliseconds;
					int sleepDuration = Math.Min(msTillNextJob, 1000*300); // sleep for up to 5 minutes

					await Task.Delay(sleepDuration, stoppingToken);
				}
			}
		}
	}
}
