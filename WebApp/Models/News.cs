namespace WebApp.Models;

public class News
{
    public int Id { get; set; }
    public string Lang { get; set; } = "zh-TW";
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty; // 也可以未來改 Markdown
    public bool IsPublished { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}