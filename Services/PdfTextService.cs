using UglyToad.PdfPig;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace CongVan.Services;

// Trích xuất chữ từ file PDF để phục vụ tìm kiếm theo nội dung — đọc trực tiếp lớp chữ trước
// (nhanh, không cần gì thêm) nếu PDF là văn bản gõ sẵn; nếu lớp chữ rỗng/quá ít (PDF là bản
// scan/chụp ảnh giấy — rất phổ biến với văn bản đến), chuyển sang OCR (Tesseract) từng trang.
// OCR chậm hơn nhiều (vài giây/trang) nên LUÔN gọi từ nền (fire-and-forget sau khi upload xong),
// không bao giờ chặn request upload — xem cách gọi ở CongVanDenController.
public class PdfTextService
{
    private readonly string _tessDataPath;
    private const int SoTrangOcrToiDa = 15; // chặn trần số trang OCR cho 1 file — tránh 1 file quá dài làm nghẽn hàng đợi nền

    public PdfTextService(IConfiguration config)
    {
        _tessDataPath = config["Ocr:TessDataPath"] ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
    }

    public bool LaFilePdf(string tenFile) =>
        Path.GetExtension(tenFile).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<string?> ExtractTextAsync(string fullPath)
    {
        if (!LaFilePdf(fullPath) || !File.Exists(fullPath)) return null;

        var textLayer = DocTextLopChu(fullPath);
        if (DuDaiDeCoiLaCoChu(textLayer)) return textLayer;

        // Lớp chữ rỗng/quá ít → khả năng cao là bản scan/ảnh, chuyển sang OCR.
        return await OcrTungTrangAsync(fullPath);
    }

    private static string DocTextLopChu(string fullPath)
    {
        try
        {
            using var pdf = PdfDocument.Open(fullPath);
            var sb = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }
        catch
        {
            return ""; // PDF hỏng/không đọc được lớp chữ — để OCR thử tiếp
        }
    }

    // Ngưỡng đơn giản: trung bình dưới 20 ký tự chữ mỗi trang coi như "không có chữ thật" (PDF scan
    // thường có 0 ký tự; PDF gõ tay dù ngắn cũng thường vượt xa ngưỡng này).
    private static bool DuDaiDeCoiLaCoChu(string text)
    {
        var soKyTuChu = text.Count(char.IsLetterOrDigit);
        return soKyTuChu >= 20;
    }

    private async Task<string?> OcrTungTrangAsync(string fullPath)
    {
        try
        {
            var pdfBytes = await File.ReadAllBytesAsync(fullPath);
            var soTrang = Conversion.GetPageCount(pdfBytes, password: null);
            var sb = new System.Text.StringBuilder();
            using var engine = new TesseractEngine(_tessDataPath, "vie", EngineMode.Default);

            for (int i = 0; i < Math.Min(soTrang, SoTrangOcrToiDa); i++)
            {
                using SKBitmap bitmap = Conversion.ToImage(pdfBytes, page: i, password: null, options: new RenderOptions(Dpi: 200));
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
                using var pix = Pix.LoadFromMemory(data.ToArray());
                using var ocrPage = engine.Process(pix);
                sb.AppendLine(ocrPage.GetText());
            }
            return sb.ToString();
        }
        catch
        {
            return null; // lỗi OCR (file hỏng, thiếu tessdata...) — coi như chưa trích xuất được, không chặn gì khác
        }
    }
}
