using Microsoft.EntityFrameworkCore;
using User0332.Backup.Server;
using User0332.Backup.Server.Models;
using User0332.Backup.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddDbContext<BackupServerDbContext>(options =>
{
	options.UseSqlite(builder.Configuration.GetConnectionString("BackupServerDb"));
});

builder.Services.AddHostedService<SnapshotCleanupWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<BackupService>();
app.MapGrpcService<RestoreService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
