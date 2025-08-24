using WebApp.Models;


namespace WebApp.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.News.Any())
        {
            db.News.AddRange(
            new News { Lang = "zh-TW", Title = "網站啟動！", Slug = "site-launched", Html = "<p>第一篇新聞，Hello MVC + Export!</p>" },
            new News { Lang = "en-US", Title = "Site Launched!", Slug = "site-launched", Html = "<p>Hello MVC + Export!</p>" }
            );
            db.SaveChanges();
        }
    }
}