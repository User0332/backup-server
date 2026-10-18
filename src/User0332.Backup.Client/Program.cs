using Microsoft.Extensions.Options;
using User0332.Backup.Client;
using User0332.Backup.Client.Configuration;
using User0332.Backup.Shared;

var builder = Host.CreateApplicationBuilder(args);

if (!Directory.Exists(Constants.DataDirectory))
{
	Directory.CreateDirectory(Constants.DataDirectory);

	Console.WriteLine($"Created data directory at {Constants.DataDirectory}. Please edit the configuration file at {Constants.BackupClientConfigFilePath} and then restart the service.");
	return;
}

builder.Configuration.AddJsonFile(Constants.BackupClientConfigFilePath, optional: false, reloadOnChange: false);

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
