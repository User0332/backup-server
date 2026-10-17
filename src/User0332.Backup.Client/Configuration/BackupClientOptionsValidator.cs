using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace User0332.Backup.Client.Configuration;

public class BackupClientOptionsValidator : IValidateOptions<BackupClientOptions>
{
	public ValidateOptionsResult Validate(string? name, BackupClientOptions options)
	{
		var schema = JsonSchema.FromText(File.ReadAllText("config-schema.json"));

		var doc = JsonDocument.Parse(File.ReadAllText("backup-config.json"));

		var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

		if (!result.IsValid)
		{
			var errors = string.Join(", ", result.Errors!.Select(e => $"{e.Key}: {e.Value}"));

			return ValidateOptionsResult.Fail($"Configuration validation failed: {errors}");
		}

		if (!File.Exists(options.EncryptionKey)) return ValidateOptionsResult.Fail($"Encryption key file '{options.EncryptionKey}' does not exist.");

		HashSet<string> jobNames = [];

		foreach (var job in options.Jobs)
		{
			if (!Directory.Exists(job.BasePath)) return ValidateOptionsResult.Fail($"Base path '{job.BasePath}' for job '{job.JobId}' does not exist.");

			if (jobNames.Contains(job.JobId)) return ValidateOptionsResult.Fail($"Duplicate job name '{job.JobId}' found.");

			jobNames.Add(job.JobId);

			var interval = job.Interval.ToTimeSpan();

			if (job.ExpiryPolicy?.MaxAge is not null)
			{
				var maxAge = job.ExpiryPolicy.MaxAge.ToTimeSpan();

				if (maxAge <= interval) return ValidateOptionsResult.Fail($"MaxAge '{job.ExpiryPolicy.MaxAge}' for job '{job.JobId}' cannot be less than or equal to the interval '{job.Interval}'.");
			}
		}

		return ValidateOptionsResult.Success;
	}
}