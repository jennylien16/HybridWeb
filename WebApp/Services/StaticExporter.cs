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

        // 讀取 Pages 用的子路徑（例如 "/HybridWeb"），本機預設空字串
        var basePath = Environment.GetEnvironmentVariable("BASE_PATH") ?? "";
        if (basePath.EndsWith("/")) basePath = basePath.TrimEnd('/');

        using var http = new HttpClient();

        foreach (var culture in cultures)
        {
            // 1) 固定路由
            foreach (var path in paths)
            {
                await ExportPage(http, baseUrl, culture, path, exportDir, basePath);
            }

            // 2) News 詳情頁
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var slugs = db.News.Where(n => n.IsPublished && n.Lang == culture)
                                   .Select(n => n.Slug).Distinct().ToList();

                foreach (var slug in slugs)
                {
                    var detailPath = $"/News/Detail/{slug}";
                    await ExportPage(http, baseUrl, culture, detailPath, exportDir, basePath);
                }
            }
        }

        // 3) 複製 wwwroot
        var src = Path.Combine(_env.ContentRootPath, "wwwroot");
        var dst = Path.Combine(exportDir, "wwwroot");
        CopyDirectory(src, dst);
        Console.WriteLine($"[Export] Assets → {dst}");

        // 4) 產生根目錄 index.html（導向預設語言資料夾）
        var defaultCulture = Environment.GetEnvironmentVariable("DEFAULT_CULTURE")
                             ?? cultures.FirstOrDefault() ?? "zh-TW";
        var rootIndex = Path.Combine(exportDir, "index.html");
        var redirect = $"./{defaultCulture}/"; // 用相對路徑，Pages/本機都適用
        await File.WriteAllTextAsync(rootIndex,
            "<!doctype html><meta charset='utf-8'>" +
            $"<meta http-equiv='refresh' content='0; url={redirect}'>" +
            $"<link rel='canonical' href='{redirect}'>");

        Console.WriteLine("[Export] Done.");
    }

    private async Task ExportPage(HttpClient http, string baseUrl, string culture, string path, string exportDir, string basePath)
    {
        var url = CombineUrl(baseUrl, path);
        var urlWithCulture = url + (url.Contains('?') ? "&" : "?") + $"culture={culture}";

        Console.WriteLine($"[Export] GET {urlWithCulture}");
        var html = await http.GetStringAsync(urlWithCulture);

        // ★ 調整 BasePath 與靜態資源位置（只有在 basePath 非空時）
        html = AdjustBasePath(html, basePath, culture);

        var outDir = Path.Combine(exportDir, culture, PathFromRoute(path));
        Directory.CreateDirectory(outDir);
        var outFile = Path.Combine(outDir, "index.html");
        await File.WriteAllTextAsync(outFile, html, Encoding.UTF8);
        Console.WriteLine($"[Export] → {outFile}");
    }

    private static string AdjustBasePath(string html, string basePath, string culture)
    {
        if (string.IsNullOrEmpty(basePath)) return html;

        // 調整 <base href="/{culture}/"> → "/{repo}/{culture}/"
        html = html.Replace($"<base href=\"/{culture}/\">", $"<base href=\"{basePath}/{culture}/\">");

        // 調整資源：/wwwroot/... → /{repo}/wwwroot/...
        html = html.Replace("href=\"/wwwroot/", $"href=\"{basePath}/wwwroot/");
        html = html.Replace("src=\"/wwwroot/", $"src=\"{basePath}/wwwroot/");

        return html;
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
