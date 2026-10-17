namespace User0332.Backup.Server;

public class ServerLockManager
{
	private readonly SemaphoreSlim _databaseLock = new(1, 1);

	public async Task<IDisposable> AcquireDatabaseLockAsync(CancellationToken cancellationToken)
	{
		await _databaseLock.WaitAsync(cancellationToken);
		return new Releaser(_databaseLock);
	}

	class Releaser(SemaphoreSlim semaphore) : IDisposable
	{
		public void Dispose()
		{
			semaphore.Release();
		}
	}
}