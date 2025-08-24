using Microsoft.AspNetCore.Mvc;
using WebApp.Data;


namespace WebApp.Controllers;

public class NewsController : Controller
{
    private readonly AppDbContext _db;
    public NewsController(AppDbContext db) => _db = db;


    public IActionResult Index(string? lang)
    {
        var culture = lang ?? (HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.UICulture.Name ?? "zh-TW");
        var list = _db.News.Where(n => n.IsPublished && n.Lang == culture)
                   .AsEnumerable()                    // ← 關鍵
                   .OrderByDescending(n => n.CreatedAt)
                   .ToList();

        return View(list);
    }


    [Route("News/Detail/{slug}")]
    public IActionResult Detail(string slug)
    {
        var culture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.UICulture.Name ?? "zh-TW";
        var item = _db.News.FirstOrDefault(n => n.Slug == slug && n.Lang == culture);
        if (item == null) return NotFound();
        return View(item);
    }
}