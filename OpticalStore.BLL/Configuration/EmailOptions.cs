namespace OpticalStore.BLL.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Optical Store";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public string OrderHistoryPath { get; set; } = "/profile/orders";
}
