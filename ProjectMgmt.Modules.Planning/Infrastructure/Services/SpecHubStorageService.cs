#pragma warning disable CA1305, CA1848
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Planning.Infrastructure.Services;

public class SpecHubStorageService : ISpecHubStorageService
{
    private readonly string _specHubRoot;
    private readonly ILogger<SpecHubStorageService> _logger;

    public SpecHubStorageService(IHostEnvironment environment, ILogger<SpecHubStorageService> logger)
    {
        _specHubRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "Spec_Hub"));
        _logger = logger;
        Directory.CreateDirectory(_specHubRoot);
    }

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Default";
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name.Trim())
        {
            if (invalidChars.Contains(c) || c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }
        var result = sb.ToString().Trim('.', ' ', '_');
        return string.IsNullOrEmpty(result) ? "Item" : result;
    }

    public string GetProjectRootPath(string organizationName, string projectName)
    {
        var safeOrg = SanitizeName(organizationName);
        var safeProject = SanitizeName(projectName);
        var folderName = $"{safeProject}_thuộc_{safeOrg}";
        return Path.GetFullPath(Path.Combine(_specHubRoot, safeOrg, folderName));
    }

    private string ResolveSafePath(string organizationName, string projectName, string relativePath)
    {
        var projectRoot = GetProjectRootPath(organizationName, projectName);
        var normalizedRelative = (relativePath ?? string.Empty).TrimStart('/', '\\').Replace('\\', '/');
        var fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedRelative));

        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Truy cập đường dẫn không hợp lệ ngoài phạm vi dự án.");
        }

        return fullPath;
    }

    public Task<string> EnsureProjectFolderAsync(string organizationName, string projectName, CancellationToken cancellationToken = default)
    {
        var projectRoot = GetProjectRootPath(organizationName, projectName);
        Directory.CreateDirectory(projectRoot);
        return Task.FromResult(projectRoot);
    }

    public Task<string> CreateFolderAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        Directory.CreateDirectory(fullPath);
        return Task.FromResult(fullPath);
    }

    public async Task<string> WriteTextFileAsync(string organizationName, string projectName, string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, Encoding.UTF8, cancellationToken);
        return fullPath;
    }

    public async Task<(string RelativePath, long FileSize)> SaveUploadedFileAsync(string organizationName, string projectName, string relativePath, Stream stream, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);

        var fileInfo = new FileInfo(fullPath);
        return (relativePath, fileInfo.Length);
    }

    public async Task<string> ReadTextFileAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Tệp tin không tồn tại trên hệ thống tệp Spec Hub.", relativePath);
        }

        return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken);
    }

    public Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Tệp tin không tồn tại trên hệ thống tệp Spec Hub.", relativePath);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain; charset=utf-8",
            ".md" => "text/markdown; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };

        return Task.FromResult(((Stream)stream, contentType, Path.GetFileName(fullPath)));
    }

    public async Task<DocxPreviewDto> ExtractDocxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Tệp tin Word không tồn tại.", relativePath);
        }

        var fileName = Path.GetFileName(fullPath);
        var htmlBuilder = new StringBuilder();
        var rawTextBuilder = new StringBuilder();

        try
        {
            await using var fileStream = File.OpenRead(fullPath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null)
            {
                return new DocxPreviewDto(fileName, "<p class='text-slate-400'>Tệp Word không chứa nội dung tài liệu chuẩn.</p>", "");
            }

            await using var entryStream = entry.Open();
            var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, cancellationToken);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var body = doc.Root?.Element(w + "body");
            if (body != null)
            {
                foreach (var element in body.Elements())
                {
                    if (element.Name == w + "p")
                    {
                        var pText = string.Concat(element.Descendants(w + "t").Select(t => t.Value));
                        if (string.IsNullOrWhiteSpace(pText)) continue;

                        rawTextBuilder.AppendLine(pText);

                        // Kiểm tra heading
                        var pStyle = element.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value;
                        if (!string.IsNullOrEmpty(pStyle) && pStyle.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                        {
                            var level = pStyle.Length > 7 && char.IsDigit(pStyle[7]) ? pStyle[7] : '2';
                            htmlBuilder.Append($"<h{level} class='text-lg font-bold text-slate-900 dark:text-white mt-4 mb-2'>{System.Net.WebUtility.HtmlEncode(pText)}</h{level}>");
                        }
                        else
                        {
                            htmlBuilder.Append($"<p class='text-sm text-slate-700 dark:text-slate-200 mb-2 leading-relaxed'>{System.Net.WebUtility.HtmlEncode(pText)}</p>");
                        }
                    }
                    else if (element.Name == w + "tbl")
                    {
                        htmlBuilder.Append("<div class='overflow-x-auto my-4'><table class='min-w-full divide-y divide-slate-200 dark:divide-slate-800 border border-slate-200 dark:border-slate-800 text-xs text-left'>");
                        var isFirstRow = true;
                        foreach (var row in element.Elements(w + "tr"))
                        {
                            htmlBuilder.Append("<tr class='hover:bg-slate-50 dark:hover:bg-slate-800/50'>");
                            foreach (var cell in row.Elements(w + "tc"))
                            {
                                var cellText = string.Concat(cell.Descendants(w + "t").Select(t => t.Value));
                                rawTextBuilder.Append(cellText + "\t");
                                if (isFirstRow)
                                {
                                    htmlBuilder.Append($"<th class='px-3 py-2 bg-slate-100 dark:bg-slate-800 font-bold border-r border-slate-200 dark:border-slate-800'>{System.Net.WebUtility.HtmlEncode(cellText)}</th>");
                                }
                                else
                                {
                                    htmlBuilder.Append($"<td class='px-3 py-2 border-t border-r border-slate-200 dark:border-slate-800'>{System.Net.WebUtility.HtmlEncode(cellText)}</td>");
                                }
                            }
                            htmlBuilder.Append("</tr>");
                            isFirstRow = false;
                            rawTextBuilder.AppendLine();
                        }
                        htmlBuilder.Append("</table></div>");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất tệp Word {FileName}", fileName);
            htmlBuilder.Append($"<p class='text-amber-500'>Không thể trích xuất toàn bộ tệp Word: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>");
        }

        return new DocxPreviewDto(fileName, htmlBuilder.ToString(), rawTextBuilder.ToString());
    }

    public async Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Tệp tin Excel không tồn tại.", relativePath);
        }

        var fileName = Path.GetFileName(fullPath);
        var sheets = new List<XlsxSheetDto>();

        try
        {
            await using var fileStream = File.OpenRead(fullPath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // 1. Đọc bảng chuỗi dùng chung (sharedStrings.xml)
            var sharedStrings = new List<string>();
            var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry != null)
            {
                await using var ssStream = sharedStringsEntry.Open();
                var ssDoc = await XDocument.LoadAsync(ssStream, LoadOptions.None, cancellationToken);
                XNamespace ssNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var si in ssDoc.Descendants(ssNs + "si"))
                {
                    sharedStrings.Add(string.Concat(si.Descendants(ssNs + "t").Select(t => t.Value)));
                }
            }

            // 2. Đọc danh sách sheet trong workbook.xml
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var sheetMeta = new List<(string Name, string SheetId)>();
            if (workbookEntry != null)
            {
                await using var wbStream = workbookEntry.Open();
                var wbDoc = await XDocument.LoadAsync(wbStream, LoadOptions.None, cancellationToken);
                XNamespace wbNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

                var sheetIndex = 1;
                foreach (var sheetElem in wbDoc.Descendants(wbNs + "sheet"))
                {
                    var sheetName = sheetElem.Attribute("name")?.Value ?? $"Sheet{sheetIndex}";
                    sheetMeta.Add((sheetName, $"sheet{sheetIndex}.xml"));
                    sheetIndex++;
                }
            }

            if (sheetMeta.Count == 0)
            {
                sheetMeta.Add(("Sheet1", "sheet1.xml"));
            }

            // 3. Đọc dữ liệu từng sheet
            XNamespace sNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var meta in sheetMeta)
            {
                var sheetEntry = archive.GetEntry($"xl/worksheets/{meta.SheetId}");
                if (sheetEntry == null) continue;

                await using var sStream = sheetEntry.Open();
                var sDoc = await XDocument.LoadAsync(sStream, LoadOptions.None, cancellationToken);
                var rowsList = new List<List<string>>();

                foreach (var rowElem in sDoc.Descendants(sNs + "row"))
                {
                    var rowCells = new List<string>();
                    foreach (var c in rowElem.Elements(sNs + "c"))
                    {
                        var cellType = c.Attribute("t")?.Value;
                        var val = c.Element(sNs + "v")?.Value;

                        if (cellType == "s" && int.TryParse(val, out var strIdx) && strIdx >= 0 && strIdx < sharedStrings.Count)
                        {
                            rowCells.Add(sharedStrings[strIdx]);
                        }
                        else
                        {
                            rowCells.Add(val ?? string.Empty);
                        }
                    }
                    if (rowCells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                    {
                        rowsList.Add(rowCells);
                    }
                }

                sheets.Add(new XlsxSheetDto(meta.Name, rowsList));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi trích xuất tệp Excel {FileName}", fileName);
            sheets.Add(new XlsxSheetDto("Sheet1", new List<List<string>> { new() { $"Lỗi trích xuất: {ex.Message}" } }));
        }

        return new XlsxPreviewDto(fileName, sheets);
    }

    public Task DeleteAsync(string organizationName, string projectName, string relativePath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(organizationName, projectName, relativePath);
        if (isDirectory)
        {
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
        else
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }

    public Task RenameAsync(string organizationName, string projectName, string oldRelativePath, string newRelativePath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        var oldFullPath = ResolveSafePath(organizationName, projectName, oldRelativePath);
        var newFullPath = ResolveSafePath(organizationName, projectName, newRelativePath);

        if (isDirectory)
        {
            if (Directory.Exists(oldFullPath))
            {
                Directory.Move(oldFullPath, newFullPath);
            }
        }
        else
        {
            if (File.Exists(oldFullPath))
            {
                File.Move(oldFullPath, newFullPath, overwrite: true);
            }
        }

        return Task.CompletedTask;
    }
}
