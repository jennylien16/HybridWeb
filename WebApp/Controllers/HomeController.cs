using Microsoft.AspNetCore.Mvc;
using WebApp.Data;


namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;


    public IActionResult Index()
    {
        var latest = _db.News.Where(n => n.IsPublished)
                     .AsEnumerable()                  // ← 關鍵：轉到記憶體處理
                     .OrderByDescending(n => n.CreatedAt)
                     .Take(3)
                     .ToList();

        return View(latest);
    }
}