using User0332.Backup.Server.Repositories;

namespace User0332.Backup.Server;

public class SnapshotCleanupWorker(ILogger<SnapshotCleanupWorker> logger, ServerLockManager lockManager, IBackupRepository db) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		logger.LogInformation("Snapshot cleanup worker started.");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var nextExpiryTime = await db.GetNextSnapshotExpiryTime(stoppingToken);

				if (nextExpiryTime is null)
				{
					logger.LogInformation("No snapshots found for cleanup.");

					await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
					continue;
				}

				var timeUntilNextExpiry = nextExpiryTime.Value - DateTimeOffset.UtcNow;
				if (timeUntilNextExpiry > TimeSpan.Zero)
				{
					logger.LogInformation($"Next snapshot expiry in {timeUntilNextExpiry}. Waiting...");
					await Task.Delay(timeUntilNextExpiry, stoppingToken);
				}

				using (await lockManager.AcquireDatabaseLockAsync(stoppingToken))
				{
					await CleanupSnapshotsAsync(stoppingToken);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error during snapshot cleanup.");
			}
		}
	}

	private async Task CleanupSnapshotsAsync(CancellationToken cancellationToken)
	{
		var snapshots = await db.ListSnapshotsAsync(null, null, cancellationToken);

		foreach (var snapshot in snapshots)
		{
			if (snapshot.ExpiresAt < DateTime.UtcNow)
			{
				logger.LogInformation("Deleting expired snapshot: {SnapshotId}", snapshot.SnapshotId);

				var filesToCleanup = await db.DeleteSnapshotAsync(snapshot, cancellationToken);

				foreach (var file in filesToCleanup)
				{
					try
					{
						if (File.Exists(file))
						{
							File.Delete(file);
							logger.LogInformation("Deleted file: {FilePath}", file);
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error deleting file: {FilePath}", file);
					}
				}
			}
		}
	}
}