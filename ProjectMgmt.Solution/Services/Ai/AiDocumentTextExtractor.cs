namespace ProjectMgmt.Solution.Services.Ai;

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class AiDocumentTextExtractor : IAiDocumentTextExtractor
{
    private const long MaxFileSizeBytes = 30 * 1024 * 1024; // 30 MB
    private readonly ILogger<AiDocumentTextExtractor> _logger;

    public AiDocumentTextExtractor(ILogger<AiDocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractAsync(IFormFile? file, string? pastedText, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        if (file != null && file.Length > 0)
        {
            if (file.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException($"Tệp tải lên vượt quá dung lượng cho phép ({MaxFileSizeBytes / 1024 / 1024}MB).");
            }

            await using var stream = file.OpenReadStream();
            var extracted = await ExtractFromStreamAsync(stream, file.FileName, ct);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                sb.AppendLine($"# TÀI LIỆU ĐÍNH KÈM: {file.FileName}");
                sb.AppendLine(extracted);
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(pastedText))
        {
            sb.AppendLine("# GHI CHÚ / YÊU CẦU BỔ SUNG:");
            sb.AppendLine(pastedText.Trim());
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Không tìm thấy nội dung văn bản hợp lệ từ tệp hoặc ghi chú đã nhập.");
        }

        return result;
    }

    public async Task<string> ExtractFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch
        {
            ".docx" => await ExtractFromDocxAsync(stream, ct),
            ".pdf" => await ExtractFromPdfAsync(stream, ct),
            ".xlsx" => await ExtractFromXlsxAsync(stream, ct),
            ".txt" or ".md" or ".json" or ".csv" => await ExtractPlainTextAsync(stream, ct),
            _ => throw new NotSupportedException($"Định dạng tệp '{ext}' chưa được hỗ trợ. Vui lòng sử dụng .docx, .pdf, .txt, .md, hoặc .xlsx.")
        };
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return string.Empty;

            await using var entryStream = entry.Open();
            var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, ct);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var body = doc.Root?.Element(w + "body");
            if (body == null) return string.Empty;

            foreach (var elem in body.Elements())
            {
                if (elem.Name == w + "p")
                {
                    var text = string.Concat(elem.Descendants(w + "t").Select(t => t.Value));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                else if (elem.Name == w + "tbl")
                {
                    foreach (var row in elem.Elements(w + "tr"))
                    {
                        var cells = row.Elements(w + "tc")
                            .Select(c => string.Concat(c.Descendants(w + "t").Select(t => t.Value)));
                        sb.AppendLine(string.Join(" | ", cells));
                    }
                    sb.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất tệp Word .docx");
            throw new InvalidOperationException($"Không thể giải nén tài liệu Word .docx: {ex.Message}", ex);
        }

        return sb.ToString().Trim();
    }

    private async Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            var content = Encoding.ASCII.GetString(bytes);

            var streamIdx = 0;
            while ((streamIdx = content.IndexOf("stream", streamIdx, StringComparison.Ordinal)) != -1)
            {
                var endStreamIdx = content.IndexOf("endstream", streamIdx, StringComparison.Ordinal);
                if (endStreamIdx == -1) break;

                var streamStart = streamIdx + 6;
                if (streamStart < endStreamIdx && content[streamStart] == '\r') streamStart++;
                if (streamStart < endStreamIdx && content[streamStart] == '\n') streamStart++;

                var streamBytes = bytes.Skip(streamStart).Take(endStreamIdx - streamStart).ToArray();
                try
                {
                    using var decompStream = new MemoryStream(streamBytes);
                    using var ds = new DeflateStream(decompStream, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    await ds.CopyToAsync(decompressed, ct);
                    var text = Encoding.UTF8.GetString(decompressed.ToArray());
                    ExtractPdfCommands(text, sb);
                }
                catch
                {
                    var rawText = Encoding.ASCII.GetString(streamBytes);
                    ExtractPdfCommands(rawText, sb);
                }

                streamIdx = endStreamIdx + 9;
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]" : result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất PDF");
            throw new InvalidOperationException($"Không thể giải nén tài liệu PDF: {ex.Message}", ex);
        }
    }

    private static void ExtractPdfCommands(string streamContent, StringBuilder sb)
    {
        var idx = 0;
        while (idx < streamContent.Length)
        {
            var openParen = streamContent.IndexOf('(', idx);
            if (openParen == -1) break;
            var closeParen = streamContent.IndexOf(')', openParen + 1);
            if (closeParen == -1) break;

            var str = streamContent.Substring(openParen + 1, closeParen - openParen - 1);
            if (!string.IsNullOrWhiteSpace(str) && str.Length > 1 && !str.StartsWith('/') && !str.StartsWith('%'))
            {
                sb.Append(str + " ");
            }
            idx = closeParen + 1;
        }
    }

    private async Task<string> ExtractFromXlsxAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = new List<string>();
            var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (ssEntry != null)
            {
                await using var ssStream = ssEntry.Open();
                var ssDoc = await XDocument.LoadAsync(ssStream, LoadOptions.None, ct);
                XNamespace ssNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                sharedStrings.AddRange(ssDoc.Descendants(ssNs + "si").Select(si => string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value))));
            }

            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (sheetEntry != null)
            {
                await using var sStream = sheetEntry.Open();
                var sDoc = await XDocument.LoadAsync(sStream, LoadOptions.None, ct);
                XNamespace sNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var row in sDoc.Descendants(sNs + "row"))
                {
                    var cells = row.Elements(sNs + "c").Select(c =>
                    {
                        var t = c.Attribute("t")?.Value;
                        var val = c.Element(sNs + "v")?.Value ?? "";
                        if (t == "s" && int.TryParse(val, out var idx) && idx >= 0 && idx < sharedStrings.Count)
                            return sharedStrings[idx];
                        return val;
                    });
                    sb.AppendLine(string.Join(" | ", cells));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất Excel .xlsx");
        }

        return sb.ToString().Trim();
    }
}
