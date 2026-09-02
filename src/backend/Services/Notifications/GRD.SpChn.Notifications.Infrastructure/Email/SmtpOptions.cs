namespace GRD.SpChn.Notifications.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "notifications@grd.local";
    public string FromName { get; init; } = "GRD Supply Chain";
    public int PollingIntervalSeconds { get; init; } = 5;
    public int MaxAttempts { get; init; } = 5;
}
