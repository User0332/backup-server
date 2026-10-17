using Microsoft.Extensions.Options;
using User0332.Backup.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("backup-config.json", optional: false, reloadOnChange: false);

builder.Services.AddOptions<BackupClientOptions>()
	.Bind(builder.Configuration)
	.ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<BackupClientOptions>, BackupClientOptionsValidator>();

builder.Services.AddWindowsService(options =>
{
	options.ServiceName = "User0332 Backup Client";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
