namespace MiniLogistics.Web.Services;

public sealed class BrowserSpaOptions
{
    public const string SectionName = "BrowserSpa";
    public bool Enabled { get; set; }
    public bool ServeAtRoot { get; set; }
    public string DistPath { get; set; } = string.Empty;
}
