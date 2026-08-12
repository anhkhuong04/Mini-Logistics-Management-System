using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Banners;

public class Banner : AuditableEntity
{
    private Banner() { } // EF Core

    public Banner(string title, string imageUrl, string? linkUrl, bool isActive, int sortOrder)
        : base(Guid.NewGuid(), DateTimeOffset.UtcNow)
    {
        Title = title;
        ImageUrl = imageUrl;
        LinkUrl = linkUrl;
        IsActive = isActive;
        SortOrder = sortOrder;
    }

    public string Title { get; private set; } = null!;
    public string ImageUrl { get; private set; } = null!;
    public string? LinkUrl { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string title, string imageUrl, string? linkUrl, int sortOrder)
    {
        Title = title;
        ImageUrl = imageUrl;
        LinkUrl = linkUrl;
        SortOrder = sortOrder;
        MarkUpdated(DateTimeOffset.UtcNow);
    }

    public void ToggleActiveStatus()
    {
        IsActive = !IsActive;
        MarkUpdated(DateTimeOffset.UtcNow);
    }
}
