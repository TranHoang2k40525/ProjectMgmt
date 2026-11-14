namespace ProjectMgmt.Solution.Services.Ai;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectMgmt.Core.Security;

public class AiDocumentTextExtractor : IAiDocumentTextExtractor
{
    private const long MaxFileSizeBytes = 30 * 1024 * 1024; // 30 MB
    public const long MaxSingleEntryUncompressedBytes = 25L * 1024 * 1024; // 25 MB per ZIP entry
    public const long MaxTotalUncompressedBytes = 50L * 1024 * 1024; // 50 MB total uncompressed per archive
    public const int MaxZipEntries = 1000;
    public const int MaxExtractedTextLength = 500_000;
    public const string ExtractionWarningPrefix = "[CẢNH BÁO TRÍCH XUẤT HÌNH ẢNH/SCAN]:";
    public const string ScannedPdfMarker = "[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]";

    private sealed class ZipArchiveSecurityException(string message) : InvalidOperationException(message);

    private static readonly Regex PdfImageObjectRegex = new(
        @"/Subtype\s*/Image\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        var result = ClampExtractedText(sb.ToString().Trim());
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Không tìm thấy nội dung văn bản hợp lệ từ tệp hoặc ghi chú đã nhập.");
        }

        return result;
    }

    public async Task<string> ExtractFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        var rawExtracted = ext switch
        {
            ".docx" => await ExtractFromDocxAsync(stream, ct),
            ".pdf" => await ExtractFromPdfAsync(stream, ct),
            ".xlsx" => await ExtractFromXlsxAsync(stream, ct),
            ".txt" or ".md" or ".json" or ".csv" => await ExtractPlainTextAsync(stream, ct),
            _ => throw new NotSupportedException($"Định dạng tệp '{ext}' chưa được hỗ trợ. Vui lòng sử dụng .docx, .pdf, .txt, .md, hoặc .xlsx.")
        };

        var sanitized = InputGuardrail.SanitizeDocumentContent(rawExtracted).SanitizedText;
        return ClampExtractedText(sanitized);
    }

    /// <summary>
    /// Trích xuất danh sách cảnh báo về tài liệu ảnh quét / sơ đồ nhúng từ kết quả đọc tài liệu.
    /// </summary>
    public static List<string> ExtractDocumentWarnings(string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return [];
        }

        var warnings = new List<string>();
        var lines = extractedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith(ExtractionWarningPrefix, StringComparison.Ordinal))
            {
                var detail = line[ExtractionWarningPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(detail) && !warnings.Contains(detail, StringComparer.Ordinal))
                {
                    warnings.Add(detail);
                }
            }
        }

        if (warnings.Count == 0 && extractedText.Contains(ScannedPdfMarker, StringComparison.Ordinal))
        {
            warnings.Add("Tệp PDF không chứa lớp văn bản (dạng ảnh quét/scan hoặc bảo vệ mật khẩu); vui lòng bổ sung mô tả văn bản để AI phân rã chính xác.");
        }

        return warnings;
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[8192];
        var sb = new StringBuilder();
        while (sb.Length < MaxExtractedTextLength)
        {
            ct.ThrowIfCancellationRequested();
            var toRead = Math.Min(buffer.Length, MaxExtractedTextLength - sb.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (read <= 0) break;
            sb.Append(buffer, 0, read);
        }

        return sb.ToString();
    }

    private async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var mediaCount = 0;
        var hasDocumentXml = false;
        long streamByteLength = stream.CanSeek ? stream.Length : 0;

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            ValidateZipArchiveMetadata(archive);

            mediaCount = archive.Entries.Count(e =>
                e.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(e.Name));

            var entry = archive.GetEntry("word/document.xml");
            if (entry != null)
            {
                hasDocumentXml = true;
                long cumulativeRead = 0;
                using var docBytesStream = await ReadZipEntryBoundedAsync(entry, MaxSingleEntryUncompressedBytes, cumulativeRead, MaxTotalUncompressedBytes, ct);
                cumulativeRead += docBytesStream.Length;

                var doc = await LoadXmlSecurelyAsync(docBytesStream, ct);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                var body = doc.Root?.Element(w + "body");
                if (body != null)
                {
                    foreach (var elem in body.Elements())
                    {
                        if (sb.Length >= MaxExtractedTextLength) break;

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
                                if (sb.Length >= MaxExtractedTextLength) break;
                            }
                            sb.AppendLine();
                        }
                    }
                }
            }
        }
        catch (ZipArchiveSecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất tệp Word .docx");
            throw new InvalidOperationException($"Không thể giải nén tài liệu Word .docx: {ex.Message}", ex);
        }

        var extracted = sb.ToString().Trim();
        var validCharCount = CountValidCharacters(extracted);

        if (mediaCount > 0 || (hasDocumentXml && streamByteLength > 8192 && validCharCount < 50))
        {
            var note = $"{ExtractionWarningPrefix} Tài liệu Word chứa {mediaCount} tệp hình ảnh/sơ đồ nhúng (word/media/*) với mật độ văn bản {validCharCount} ký tự hợp lệ. Nội dung chữ trong ảnh/sơ đồ không thể trích xuất từ XML thuần; vui lòng kiểm tra hoặc mô tả bổ sung cho Trợ lý AI.";
            extracted = string.IsNullOrEmpty(extracted)
                ? note
                : $"{extracted}\n\n{note}";
        }

        return extracted;
    }

    private async Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            if (bytes.Length == 0)
            {
                return ScannedPdfMarker;
            }

            var content = Encoding.ASCII.GetString(bytes);
            var imageObjectCount = PdfImageObjectRegex.Count(content);

            var streamIdx = 0;
            long totalDecompressed = 0;
            while ((streamIdx = content.IndexOf("stream", streamIdx, StringComparison.Ordinal)) != -1)
            {
                ct.ThrowIfCancellationRequested();
                var endStreamIdx = content.IndexOf("endstream", streamIdx, StringComparison.Ordinal);
                if (endStreamIdx == -1) break;

                var streamStart = streamIdx + 6;
                if (streamStart < endStreamIdx && content[streamStart] == '\r') streamStart++;
                if (streamStart < endStreamIdx && content[streamStart] == '\n') streamStart++;

                var streamLength = Math.Max(0, endStreamIdx - streamStart);
                var streamBytes = bytes.AsSpan(streamStart, streamLength).ToArray();
                try
                {
                    using var decompStream = new MemoryStream(streamBytes);
                    using var ds = new DeflateStream(decompStream, CompressionMode.Decompress);
                    using var decompressed = await ReadStreamBoundedAsync(
                        ds,
                        MaxSingleEntryUncompressedBytes,
                        totalDecompressed,
                        MaxTotalUncompressedBytes,
                        "PDF FlateDecode stream",
                        ct);
                    totalDecompressed += decompressed.Length;
                    var text = Encoding.UTF8.GetString(decompressed.ToArray());
                    ExtractPdfCommands(text, sb);
                }
                catch (ZipArchiveSecurityException)
                {
                    throw;
                }
                catch
                {
                    var rawText = Encoding.ASCII.GetString(streamBytes);
                    ExtractPdfCommands(rawText, sb);
                }

                if (sb.Length >= MaxExtractedTextLength) break;
                streamIdx = endStreamIdx + 9;
            }

            var result = sb.ToString().Trim();
            var validCharCount = CountValidCharacters(result);

            if (string.IsNullOrEmpty(result))
            {
                var scanNote = $"{ExtractionWarningPrefix} Tài liệu PDF dung lượng {bytes.Length / 1024.0:0.1} KB không có lớp văn bản trích xuất được (phát hiện {imageObjectCount} khối ảnh/sơ đồ). Hãy nhập mô tả yêu cầu bổ sung hoặc chuyển qua bước phân tích ảnh (Multimodal Vision) nếu khả dụng.";
                return $"{ScannedPdfMarker}\n{scanNote}";
            }

            if ((bytes.Length > 2048 && validCharCount < 50) || imageObjectCount > 0)
            {
                var diagramNote = $"{ExtractionWarningPrefix} Tài liệu PDF có mật độ văn bản thấp ({validCharCount} ký tự hợp lệ / {bytes.Length / 1024.0:0.1} KB) và phát hiện {imageObjectCount} đối tượng hình ảnh/sơ đồ nhúng (/Subtype /Image). Một số thông tin dạng ảnh/biểu đồ có thể cần bổ sung mô tả bằng văn bản.";
                return $"{result}\n\n{diagramNote}";
            }

            return result;
        }
        catch (ZipArchiveSecurityException)
        {
            throw;
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
        while (idx < streamContent.Length && sb.Length < MaxExtractedTextLength)
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
            ValidateZipArchiveMetadata(archive);

            long cumulativeRead = 0;
            var sharedStrings = new List<string>();
            var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (ssEntry != null)
            {
                using var ssStream = await ReadZipEntryBoundedAsync(ssEntry, MaxSingleEntryUncompressedBytes, cumulativeRead, MaxTotalUncompressedBytes, ct);
                cumulativeRead += ssStream.Length;

                var ssDoc = await LoadXmlSecurelyAsync(ssStream, ct);
                XNamespace ssNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                sharedStrings.AddRange(ssDoc.Descendants(ssNs + "si").Select(si => string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value))));
            }

            var sheetEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            XNamespace sNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var sheetEntry in sheetEntries)
            {
                if (sb.Length >= MaxExtractedTextLength) break;

                using var sStream = await ReadZipEntryBoundedAsync(sheetEntry, MaxSingleEntryUncompressedBytes, cumulativeRead, MaxTotalUncompressedBytes, ct);
                cumulativeRead += sStream.Length;

                var sDoc = await LoadXmlSecurelyAsync(sStream, ct);
                if (sheetEntries.Count > 1)
                {
                    sb.AppendLine($"--- {sheetEntry.Name} ---");
                }

                foreach (var row in sDoc.Descendants(sNs + "row"))
                {
                    var cells = row.Elements(sNs + "c").Select(c =>
                    {
                        var t = c.Attribute("t")?.Value;
                        var val = c.Element(sNs + "v")?.Value ?? string.Empty;
                        if (t == "s" && int.TryParse(val, out var idx) && idx >= 0 && idx < sharedStrings.Count)
                            return sharedStrings[idx];
                        return val;
                    });
                    var rowLine = string.Join(" | ", cells);
                    if (!string.IsNullOrWhiteSpace(rowLine))
                    {
                        sb.AppendLine(rowLine);
                    }
                    if (sb.Length >= MaxExtractedTextLength) break;
                }
            }
        }
        catch (ZipArchiveSecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất Excel .xlsx");
        }

        return sb.ToString().Trim();
    }

    private static void ValidateZipArchiveMetadata(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxZipEntries)
        {
            throw new ZipArchiveSecurityException(
                $"Cảnh báo bảo mật (Zip Bomb Protection): Số lượng tệp bên trong gói nén ({archive.Entries.Count}) vượt ngưỡng tối đa {MaxZipEntries} entries.");
        }

        long declaredTotal = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxSingleEntryUncompressedBytes)
            {
                throw new ZipArchiveSecurityException(
                    $"Cảnh báo bảo mật (Zip Bomb Protection): Mục '{entry.FullName}' có dung lượng giải nén ({entry.Length / (1024 * 1024.0):0.1}MB) vượt ngưỡng cho phép tối đa {MaxSingleEntryUncompressedBytes / (1024 * 1024)}MB.");
            }

            declaredTotal += Math.Max(0, entry.Length);
            if (declaredTotal > MaxTotalUncompressedBytes)
            {
                throw new ZipArchiveSecurityException(
                    $"Cảnh báo bảo mật (Zip Bomb Protection): Tổng dung lượng giải nén của gói nén vượt giới hạn an toàn tối đa {MaxTotalUncompressedBytes / (1024 * 1024)}MB.");
            }
        }
    }

    private static Task<MemoryStream> ReadZipEntryBoundedAsync(
        ZipArchiveEntry entry,
        long maxEntryBytes,
        long cumulativeReadBefore,
        long maxTotalBytes,
        CancellationToken ct)
    {
        using var rawStream = entry.Open();
        return ReadStreamBoundedAsync(rawStream, maxEntryBytes, cumulativeReadBefore, maxTotalBytes, entry.FullName, ct);
    }

    private static async Task<MemoryStream> ReadStreamBoundedAsync(
        Stream source,
        long maxEntryBytes,
        long cumulativeReadBefore,
        long maxTotalBytes,
        string entryLabel,
        CancellationToken ct)
    {
        var ms = new MemoryStream();
        var buffer = new byte[65536];
        long entryBytesRead = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0) break;

            entryBytesRead += read;
            if (entryBytesRead > maxEntryBytes)
            {
                await ms.DisposeAsync();
                throw new ZipArchiveSecurityException(
                    $"Cảnh báo bảo mật (Zip Bomb Protection): Dữ liệu giải nén thực tế của '{entryLabel}' vượt giới hạn tối đa {maxEntryBytes / (1024 * 1024)}MB.");
            }

            if (cumulativeReadBefore + entryBytesRead > maxTotalBytes)
            {
                await ms.DisposeAsync();
                throw new ZipArchiveSecurityException(
                    $"Cảnh báo bảo mật (Zip Bomb Protection): Tổng dữ liệu giải nén thực tế vượt ngưỡng an toàn {maxTotalBytes / (1024 * 1024)}MB.");
            }

            ms.Write(buffer, 0, read);
        }

        ms.Position = 0;
        return ms;
    }

    private static async Task<XDocument> LoadXmlSecurelyAsync(Stream xmlStream, CancellationToken ct)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Async = true,
            MaxCharactersInDocument = MaxTotalUncompressedBytes
        };

        using var reader = XmlReader.Create(xmlStream, settings);
        return await XDocument.LoadAsync(reader, LoadOptions.None, ct);
    }

    private static int CountValidCharacters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var count = 0;
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) count++;
        }
        return count;
    }

    private static string ClampExtractedText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxExtractedTextLength)
        {
            return text;
        }

        const string truncatedNotice = "\n\n[Nội dung trích xuất đã được rút gọn để bảo vệ bộ nhớ máy chủ (giới hạn 500.000 ký tự).]";
        var keep = Math.Max(0, MaxExtractedTextLength - truncatedNotice.Length);
        return string.Concat(text.AsSpan(0, keep), truncatedNotice);
    }
}
