using System.Text.Json;
using Grpc.Net.Client;
using User0332.Backup.RestorationTool;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly;

var config = JsonSerializer.Deserialize<MinimalConfig>(File.ReadAllText(Constants.BackupClientConfigFilePath))!;

Console.WriteLine("Welcome to the Backup Restoration Tool!");
Console.WriteLine("Enter Client ID (optional): ");
var clientIdFilter = Console.ReadLine();

if (string.IsNullOrEmpty(clientIdFilter))
{
	clientIdFilter = config.ClientId;

	Console.WriteLine($"Using current Client ID of {clientIdFilter}");
}

Console.WriteLine("Enter Job ID (optional): ");
var jobIdInput = Console.ReadLine();

Console.WriteLine("Fetching snapshots...");

using var channel = GrpcChannel.ForAddress(config.ServerAddress);
using var db =  await ClientDatabase.GetContext();

var restorer = new RestorationActions(db, channel, config);

var snapshotIds = await restorer.ListSnapshots(clientIdFilter, jobIdInput);

if (snapshotIds.Count == 0)
{
	Console.WriteLine("No snapshots found.");
	return;
}

Console.WriteLine("Available snapshots:");

for (int i = 0; i < snapshotIds.Count; i++)
{
	var snapshotId = snapshotIds[i];
	Console.WriteLine($"{i + 1}. {snapshotId}");
}

Console.WriteLine("Enter the number of the snapshot to restore: ");
var snapshotIndexInput = Console.ReadLine();

if (!int.TryParse(snapshotIndexInput, out int snapshotIndex) || snapshotIndex < 1 || snapshotIndex > snapshotIds.Count)
{
	Console.WriteLine("Invalid selection.");
	return;
}

var selectedSnapshotId = snapshotIds[snapshotIndex - 1];

Console.WriteLine($"Restoring snapshot {selectedSnapshotId}...");

await restorer.RestoreSnapshot(selectedSnapshotId);