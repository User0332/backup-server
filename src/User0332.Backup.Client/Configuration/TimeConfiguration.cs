namespace User0332.Backup.Client;

public class TimeConfiguration
{
	public required string Unit { get; set; }
	public required int Value { get; set; }

	public TimeSpan ToTimeSpan()
	{
		return Unit.ToLower() switch
		{
			"minutes" => TimeSpan.FromMinutes(Value),
			"hours" => TimeSpan.FromHours(Value),
			"days" => TimeSpan.FromDays(Value),
			"weeks" => TimeSpan.FromDays(Value * 7),
			"30d-months" => TimeSpan.FromDays(Value * 30),
			_ => throw new ArgumentException($"Invalid time unit: {Unit}")
		};
	}
}