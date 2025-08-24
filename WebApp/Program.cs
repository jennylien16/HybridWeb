using Markdig;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Services;


var builder = WebApplication.CreateBuilder(args);


// 多國資源（之後可加 .resx），先備好 RequestLocalization
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");


builder.Services
.AddControllersWithViews()
.AddViewLocalization()
.AddDataAnnotationsLocalization();


// EF Core - SQLite
var conn = builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(conn));


// Markdown 管線（之後給 Docs 用）
builder.Services.AddSingleton(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());


// 匯出器服務
builder.Services.AddSingleton<StaticExporter>();


var app = builder.Build();


// 支援語系
var supportedCultures = new[] { "zh-TW", "en-US" };
var locOpts = new RequestLocalizationOptions()
.SetDefaultCulture("zh-TW")
.AddSupportedCultures(supportedCultures)
.AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(locOpts);


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}


app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");


// 自動建庫 + 種子
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // 直接建立資料庫與表（PoC 快速法）
    DbSeeder.Seed(db);
}


// --export：啟動內建小伺服器，爬取頁面並輸出到 dist-static
if (args.Contains("--export"))
{
    var baseUrl = "http://localhost:5055"; // 匯出時用臨時連線埠
    app.Urls.Add(baseUrl);
    var runTask = app.RunAsync();


    var exporter = app.Services.GetRequiredService<StaticExporter>();
    var exportDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "dist-static"));
    Directory.CreateDirectory(exportDir);


    await exporter.RunAsync(baseUrl, exportDir);
    return; // 匯出完成即結束
}


app.Run();