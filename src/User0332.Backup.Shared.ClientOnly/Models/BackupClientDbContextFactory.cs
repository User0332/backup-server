using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace User0332.Backup.Shared.ClientOnly.Models;

public class BackupClientDbContextFactory : IDesignTimeDbContextFactory<BackupClientDbContext>
{
	public BackupClientDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<BackupClientDbContext>();
		optionsBuilder.UseSqlite($"Data Source={Constants.DatabaseFilePath}");

		return new BackupClientDbContext(optionsBuilder.Options);
	}
}