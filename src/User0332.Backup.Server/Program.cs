using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using User0332.Backup.Server;
using User0332.Backup.Server.Models;
using User0332.Backup.Server.Repositories;
using User0332.Backup.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var backupStoragePath = builder.Configuration["BackupStoragePath"]
	?? throw new InvalidOperationException("BackupStoragePath is not configured.");
Directory.CreateDirectory(backupStoragePath);

var connectionString = builder.Configuration.GetConnectionString("BackupServerDb");
var connection = new SqliteConnection(connectionString);
connection.Open();

using (var command = connection.CreateCommand())
{
	command.CommandText = "PRAGMA journal_mode=WAL;Busy Timeout=5000;";
	command.ExecuteNonQuery();
}

builder.Services.AddDbContext<BackupServerDbContext>(options =>
{
	options.UseSqlite(connection);
});

builder.Services.AddScoped<IBackupRepository, BackupRepository>();

builder.Services.AddSingleton<ServerLockManager>();
builder.Services.AddHostedService<SnapshotCleanupWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<BackupServerDbContext>();

	dbContext.Database.Migrate();
}

app.MapGrpcService<BackupService>();
app.MapGrpcService<RestoreService>();

app.Run();
