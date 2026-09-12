using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CongVan.Services;

namespace CongVan.Controllers.Api;

// Xác thực API Key cho các API cấp cho web riêng của từng đơn vị (xem VanBanNoiBoApiController).
// Đọc header "X-Api-Key", băm SHA-256 rồi so với DonVi_ApiKey.ApiKeyHash — KHÔNG BAO GIỜ lưu/so
// plaintext. Key hợp lệ → gán HttpContext.Items["ApiMaDV"] để controller tự động khoanh MỌI truy vấn
// vào đúng đơn vị của key đó; 1 key của đơn vị A không bao giờ đọc/ghi được dữ liệu đơn vị B vì
// controller luôn lấy MaDV từ đây, không bao giờ tin MaDV do client tự gửi lên.
public class ApiKeyAuthFilter : IAsyncActionFilter
{
    private readonly DbService _db;
    public ApiKeyAuthFilter(DbService db) => _db = db;

    public static byte ApiMaDV(HttpContext ctx) => (byte)ctx.Items["ApiMaDV"]!;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            context.Result = new ObjectResult(new { error = "Thiếu header X-Api-Key." }) { StatusCode = 401 };
            return;
        }

        var apiKey = keyValues.ToString().Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();
        var resolved = await _db.ResolveApiKeyAsync(hash);
        if (resolved == null)
        {
            context.Result = new ObjectResult(new { error = "API Key không hợp lệ hoặc đã bị thu hồi." }) { StatusCode = 401 };
            return;
        }

        context.HttpContext.Items["ApiMaDV"] = resolved.Value.MaDV;
        await _db.CapNhatNgaySuDungApiKeyAsync(resolved.Value.ID);
        await next();
    }
}
