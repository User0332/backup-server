using System.Security.Cryptography;
using System.Text.Json;
using User0332.Backup.Shared;
using User0332.Backup.Shared.ClientOnly.Configuration;

namespace User0332.Backup.CLITool;

public static class ConfigurationActions
{
	static string NonEmptyInput(string prompt)
	{
		while (true)
		{
			Console.Write(prompt);
			string? input = Console.ReadLine();
			if (!string.IsNullOrWhiteSpace(input))
			{
				return input;
			}
			Console.WriteLine("Input cannot be empty. Please try again.");
		}
	}

	static int PositiveIntegerInput(string prompt, int? defaultValue = null)
	{
		while (true)
		{
			Console.Write(defaultValue is null ? $"{prompt}: " : $"{prompt} [{defaultValue}]: ");
			string? input = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(input) && defaultValue is not null)
			{
				return defaultValue.Value;
			}

			if (int.TryParse(input, out int value) && value >= 1)
			{
				return value;
			}
			Console.WriteLine("Input must be a positive integer. Please try again.");
		}
	}

	static string InputWithDefault(string prompt, string? defaultValue = null)
	{
		Console.Write(defaultValue is null ? $"{prompt}: " : $"{prompt} [{defaultValue}]: ");
		string? input = Console.ReadLine();
		return string.IsNullOrWhiteSpace(input) ? defaultValue ?? NonEmptyInput(prompt) : input.Trim();
	}

	static string SelectUnit(string prompt, string[] units, string? defaultValue = null)
	{
		while (true)
		{
			Console.WriteLine($"{prompt} ({string.Join(", ", units)})");
			string value = InputWithDefault("Unit", defaultValue).ToLowerInvariant();
			if (units.Contains(value))
			{
				return value;
			}

			Console.WriteLine($"Unit must be one of: {string.Join(", ", units)}.");
		}
	}

	static TimeConfiguration ReadTimeConfiguration(string prompt, TimeConfiguration? existing, string[] units)
	{
		Console.WriteLine(prompt);
		return new TimeConfiguration
		{
			Unit = SelectUnit("Select a time unit", units, existing?.Unit),
			Value = PositiveIntegerInput("Value", existing?.Value)
		};
	}

	static JobOptions ReadJob(JobOptions? existing, int index)
	{
		Console.WriteLine($"\nConfiguring job {index + 1}.");

		string[] existingPatterns = existing?.FilePatterns ?? [];
		string patterns = InputWithDefault(
			"File patterns (comma-separated)",
			existingPatterns.Length == 0 ? null : string.Join(", ", existingPatterns));

		int minChunkSize = PositiveIntegerInput("Minimum chunk size in KB", existing?.ChunkSize.MinKB ?? 1);
		int maxChunkSize = PositiveIntegerInput("Maximum chunk size in KB", existing?.ChunkSize.MaxKB ?? 4);
		while (maxChunkSize < minChunkSize)
		{
			Console.WriteLine("Maximum chunk size must be greater than or equal to the minimum.");
			maxChunkSize = PositiveIntegerInput("Maximum chunk size in KB", existing?.ChunkSize.MaxKB ?? minChunkSize);
		}

		return new JobOptions
		{
			JobId = InputWithDefault("Job ID", existing?.JobId),
			BasePath = InputWithDefault("Base path", existing?.BasePath),
			FilePatterns = patterns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
			Interval = ReadTimeConfiguration(
				"Backup interval",
				existing?.Interval,
				["minutes", "hours", "days", "weeks"]),
			ExpiryPolicy = new ExpiryPolicy
			{
				MaxAge = ReadTimeConfiguration(
					"Snapshot expiry",
					existing?.ExpiryPolicy.MaxAge,
					["days", "weeks", "30d-months"])
			},
			ChunkSize = new ChunkSizeConstraint
			{
				MinKB = minChunkSize,
				MaxKB = maxChunkSize
			}
		};
	}

	public static BackupClientOptions Configure(BackupClientOptions? existingConfig = null)
	{
		string clientId = InputWithDefault("Client ID", existingConfig?.ClientId);
		string serverUrl = InputWithDefault("Server URL", existingConfig?.ServerAddress);
		string encryptionKey = InputWithDefault("Encryption key file name", existingConfig?.EncryptionKey);
		string encryptionKeyPath = Path.IsPathRooted(encryptionKey)
			? encryptionKey
			: Path.Combine(Constants.DataDirectory, encryptionKey);

		if (File.Exists(encryptionKey) && !Path.GetFullPath(encryptionKey).Equals(Path.GetFullPath(encryptionKeyPath), StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine($"Found '{encryptionKey}' in the current directory. Moving to '{encryptionKeyPath}'...");
			Directory.CreateDirectory(Path.GetDirectoryName(encryptionKeyPath)!);
			File.Move(encryptionKey, encryptionKeyPath, overwrite: false);
		}

		if (!File.Exists(encryptionKeyPath))
		{
			string choice = InputWithDefault($"Encryption key file '{encryptionKeyPath}' does not exist. Create a new one? (y/n)", "y").ToLowerInvariant();

			if (choice == "y" || choice == "yes")
			{
				byte[] key = new byte[32];
				RandomNumberGenerator.Fill(key);

				Directory.CreateDirectory(Path.GetDirectoryName(encryptionKeyPath)!);
				File.WriteAllBytes(encryptionKeyPath, key);

				Console.WriteLine($"New encryption key generated and saved to {encryptionKeyPath}");
			}
			else
			{
				throw new InvalidOperationException($"Encryption key file '{encryptionKeyPath}' does not exist.");
			}
		}

		int numberOfJobs = PositiveIntegerInput("Number of backup jobs", existingConfig?.Jobs.Length ?? 1);
		var jobs = new JobOptions[numberOfJobs];
		for (int i = 0; i < jobs.Length; i++)
		{
			jobs[i] = ReadJob(existingConfig?.Jobs.ElementAtOrDefault(i), i);
		}

		BackupClientOptions newConfig = new()
		{
			ClientId = clientId,
			ServerAddress = serverUrl,
			EncryptionKey = Path.GetRelativePath(Constants.DataDirectory, encryptionKeyPath),
			Jobs = jobs
		};

		string json = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
		Directory.CreateDirectory(Constants.DataDirectory);
		File.WriteAllText(Constants.BackupClientConfigFilePath, json);
		Console.WriteLine($"Configuration saved to {Constants.BackupClientConfigFilePath}.");
		return newConfig;
	}

	public static BackupClientOptions InitializeConfig()
	{
		return Configure();
	}
}