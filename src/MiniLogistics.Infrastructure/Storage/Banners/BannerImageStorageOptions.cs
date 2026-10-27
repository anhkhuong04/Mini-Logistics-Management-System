namespace MiniLogistics.Infrastructure.Storage.Banners;

public sealed class BannerImageStorageOptions
{
    public string RootPath
    {
        get; set;
    } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MiniLogistics", "banner-images");
}
