namespace HockeyPickup.Api.Models.Domain;

public class BotProtectionOptions
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "Turnstile";
    public string? SecretKey { get; set; }
    public string ExpectedAction { get; set; } = "buy_spot";
    public string[] ExpectedHostnames { get; set; } = ["app.hockeypickup.com"];
    public int MaxTokenAgeSeconds { get; set; } = 30;
    public bool AllowBeforeBuyWindow { get; set; } = false;
}

