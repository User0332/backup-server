using Microsoft.EntityFrameworkCore;
using User0332.Backup.Shared.ClientOnly.Models;

namespace User0332.Backup.Shared.ClientOnly;

public static class ClientDatabase
{
	public static async Task<BackupClientDbContext> GetContext()
	{
		var optionsBuilder = new DbContextOptionsBuilder<BackupClientDbContext>();
		optionsBuilder.UseSqlite($"Data Source={Constants.DatabaseFilePath}");

		var db = new BackupClientDbContext(optionsBuilder.Options);

		await db.Database.MigrateAsync();

		return db;
	}
}