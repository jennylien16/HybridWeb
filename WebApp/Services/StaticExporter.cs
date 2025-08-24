// WebApp/Services/StaticExporter.cs
using System.Text;
using Microsoft.Extensions.DependencyInjection;          // ★ 重要：為了 GetRequiredService / CreateScope
using WebApp.Data;

namespace WebApp.Services;

public class StaticExporter
{
    private readonly IWebHostEnvironment _env;
    private readonly IServiceScopeFactory _scopeFactory; // ★ 透過它建立 Scope 取得 DbContext

    public StaticExporter(IWebHostEnvironment env, IServiceScopeFactory scopeFactory)
    {
        _env = env;
        _scopeFactory = scopeFactory;
    }

    public async Task RunAsync(string baseUrl, string exportDir)
    {
        Console.WriteLine($"[Export] Start → {exportDir}");
        var (cultures, paths) = await LoadRoutesAsync();

        using var http = new HttpClient();

        foreach (var culture in cultures)
        {
            // 1) 先匯出固定路由
            foreach (var path in paths)
            {
                await ExportPage(http, baseUrl, culture, path, exportDir);
            }

            // 2) 再匯出 News 詳情頁（從 DB 撈出每個 slug）
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var slugs = db.News
                              .Where(n => n.IsPublished && n.Lang == culture)
                              .Select(n => n.Slug)
                              .Distinct()
                              .ToList();

                foreach (var slug in slugs)
                {
                    var detailPath = $"/News/Detail/{slug}";
                    await ExportPage(http, baseUrl, culture, detailPath, exportDir);
                }
            }
        }

        // 3) 複製 wwwroot 靜態資源到 dist-static/wwwroot
        var src = Path.Combine(_env.ContentRootPath, "wwwroot");
        var dst = Path.Combine(exportDir, "wwwroot");
        CopyDirectory(src, dst);
        Console.WriteLine($"[Export] Assets → {dst}");

        Console.WriteLine("[Export] Done.");
    }

    private async Task ExportPage(HttpClient http, string baseUrl, string culture, string path, string exportDir)
    {
        var url = CombineUrl(baseUrl, path);
        var urlWithCulture = url + (url.Contains('?') ? "&" : "?") + $"culture={culture}";

        Console.WriteLine($"[Export] GET {urlWithCulture}");
        var html = await http.GetStringAsync(urlWithCulture);

        var outDir = Path.Combine(exportDir, culture, PathFromRoute(path));
        Directory.CreateDirectory(outDir);
        var outFile = Path.Combine(outDir, "index.html");
        await File.WriteAllTextAsync(outFile, html, Encoding.UTF8);
        Console.WriteLine($"[Export] → {outFile}");
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (path.StartsWith("/")) path = path[1..];
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return baseUrl + path;
    }

    private static string PathFromRoute(string route)
    {
        // "/" → "."；其他像 "/News/Detail/site-launched" → "News/Detail/site-launched"
        return route == "/" ? "." : route.Trim('/');
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private async Task<(List<string> cultures, List<string> paths)> LoadRoutesAsync()
    {
        var jsonFile = Path.Combine(_env.ContentRootPath, "export-routes.json");
        if (!File.Exists(jsonFile))
        {
            return (new List<string> { "zh-TW" }, new List<string> { "/" });
        }
        var json = await File.ReadAllTextAsync(jsonFile, Encoding.UTF8);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cultures = root.GetProperty("cultures").EnumerateArray().Select(e => e.GetString()!).ToList();
        var paths = root.GetProperty("paths").EnumerateArray().Select(e => e.GetString()!).ToList();
        return (cultures, paths);
    }
}
