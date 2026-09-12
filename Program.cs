using System.Net;
using CongVan.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// Data Protection: lưu key ra disk để antiforgery token còn hợp lệ sau khi restart
var dpKeysPath = Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
    .SetApplicationName("CongVan");

// ── Performance ──────────────────────────────────────────────
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
        "text/html", "text/css", "application/javascript",
        "application/json", "text/plain", "image/svg+xml"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<CongVan.Services.EmailService>();
builder.Services.AddSingleton<CongVan.Services.PdfTextService>();
// Dong bo tam thoi tu server cu (giai doan van thu con dung song song 2 he thong de danh gia) —
// chi chay khi co cau hinh OldServerSync:ConnectionString, xem Services/OldServerSyncService.cs.
builder.Services.AddHostedService<CongVan.Services.OldServerSyncService>();
builder.Services.AddScoped<CongVan.Controllers.Api.ApiKeyAuthFilter>();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// API Văn bản nội bộ đơn vị (xác thực bằng API Key, không dùng session) — cho phép web riêng của
// đơn vị gọi vào từ domain khác. Không có cookie/credential nào đi qua CORS ở đây nên mở origin
// thoải mái là an toàn; ranh giới bảo mật thật nằm ở API Key (ApiKeyAuthFilter), không phải ở CORS.
builder.Services.AddCors(o => o.AddPolicy("ApiCors", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Chạy sau reverse proxy (aaPanel/nginx tại qlvb.vnkgu.edu.vn) — đọc X-Forwarded-Proto/-For để app
// biết đúng scheme (https) và IP thật của khách, thay vì tưởng mọi request là http từ 127.0.0.1.
// Chỉ tin header khi request đến TỪ dải IP nội bộ (proxy cùng máy / docker bridge / LAN) — request
// gõ thẳng vào cổng 5100 từ ngoài sẽ không giả mạo được.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2
};
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(forwardedOptions);

app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

// Static files với long-cache cho versioned assets
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        // Tất cả static file trong lib/ và có version query → cache 1 năm
        if (ctx.Context.Request.Query.ContainsKey("v") ||
            ctx.Context.Request.Path.StartsWithSegments("/lib"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
        }
        else
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");
        }
    }
});

app.UseRouting();
// KHÔNG truyền tên policy ở đây — làm vậy sẽ biến "ApiCors" thành default áp dụng cho TOÀN BỘ app
// (kể cả các action MVC dùng session cookie), không chỉ riêng API. [EnableCors("ApiCors")] đã gắn
// trực tiếp trên VanBanNoiBoApiController mới là nơi thật sự áp dụng policy này.
app.UseCors();
app.UseSession();
app.UseAuthorization();

// Bắt riêng FileUploadException (dung lượng/loại file không hợp lệ, hoặc lỗi ghi đĩa lúc upload —
// xem FileService.SaveAsync) ở MỌI action một lần duy nhất ở đây, thay vì phải sửa try/catch lặp
// lại trong 13 chỗ gọi SaveAsync rải khắp các controller. Request AJAX/JSON (nút upload không tải
// lại trang) nhận JSON { ok:false, msg }; request form thường (submit + redirect) được đưa lại
// đúng trang cũ kèm TempData["Error"] — đúng quy ước lỗi đã dùng sẵn trong toàn bộ app — thay vì để
// lộ trang lỗi 500 mặc định cho văn thư khi họ đang thao tác nhập công văn.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (CongVan.Services.FileUploadException ex)
    {
        var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            || context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (isAjax)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { ok = false, msg = ex.Message });
            return;
        }
        var tempData = context.RequestServices
            .GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>()
            .GetTempData(context);
        tempData["Error"] = ex.Message;
        var back = context.Request.Headers.Referer.ToString();
        context.Response.Redirect(string.IsNullOrEmpty(back) ? "/" : back);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers(); // cần cho các controller dùng attribute routing thuần (Controllers/Api/*)

// Download file nằm ngoài wwwroot (luôn ép tải về)
app.MapGet("/files/{**relativePath}", async (string relativePath, CongVan.Services.FileService fileSvc, HttpContext ctx) =>
{
    var fullPath = fileSvc.GetFullPath(Uri.UnescapeDataString(relativePath));
    if (!File.Exists(fullPath)) return Results.NotFound();
    var mime = CongVan.Services.FileService.GetMimeType(fullPath);
    return Results.File(fullPath, mime, Path.GetFileName(fullPath));
});

// Xem trực tiếp trên trình duyệt (không ép tải về) — dùng cho nút "Xem PDF online"
app.MapGet("/view/{**relativePath}", async (string relativePath, CongVan.Services.FileService fileSvc, HttpContext ctx) =>
{
    var fullPath = fileSvc.GetFullPath(Uri.UnescapeDataString(relativePath));
    if (!File.Exists(fullPath)) return Results.NotFound();
    var mime = CongVan.Services.FileService.GetMimeType(fullPath);
    return Results.File(fullPath, mime);
});

// Tự mở trình duyệt khi chạy tay (không chạy khi là Windows Service)
if (Environment.UserInteractive)
{
    var url = "http://localhost:5100";
    _ = Task.Run(async () =>
    {
        await Task.Delay(1500);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    });
}

app.Run();
