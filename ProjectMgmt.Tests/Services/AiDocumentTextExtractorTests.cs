namespace ProjectMgmt.Tests.Services;

using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

public class AiDocumentTextExtractorTests
{
    private readonly AiDocumentTextExtractor _extractor = new(NullLogger<AiDocumentTextExtractor>.Instance);

    [Fact]
    public async Task ExtractFromStreamAsync_PlainTextFile_ExtractsContentCorrectly()
    {
        const string textContent = "Nội dung đặc tả yêu cầu chức năng (SRS) bằng tiếng Việt.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(textContent));

        var extracted = await _extractor.ExtractFromStreamAsync(stream, "requirements.txt");

        Assert.Equal(textContent, extracted.Trim());
    }

    [Fact]
    public async Task ExtractFromStreamAsync_MarkdownFile_ExtractsContentCorrectly()
    {
        const string mdContent = "# Tiêu đề SRS\n\n- Yêu cầu 1: Đăng nhập\n- Yêu cầu 2: Phân quyền Scrum";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mdContent));

        var extracted = await _extractor.ExtractFromStreamAsync(stream, "spec.md");

        Assert.Equal(mdContent, extracted.Trim());
    }

    [Fact]
    public async Task ExtractFromStreamAsync_DocxArchive_ExtractsParagraphsAndTables()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            await using var entryStream = entry.Open();
            const string docXml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:r><w:t>Đặc tả Sprint Goal M2</w:t></w:r></w:p>
    <w:tbl>
      <w:tr>
        <w:tc><w:p><w:r><w:t>Mã Task</w:t></w:r></w:p></w:tc>
        <w:tc><w:p><w:r><w:t>Tiêu đề</w:t></w:r></w:p></w:tc>
      </w:tr>
      <w:tr>
        <w:tc><w:p><w:r><w:t>TASK-1</w:t></w:r></w:p></w:tc>
        <w:tc><w:p><w:r><w:t>Cài đặt SSE Controller</w:t></w:r></w:p></w:tc>
      </w:tr>
    </w:tbl>
  </w:body>
</w:document>";
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes(docXml));
        }

        memoryStream.Position = 0;
        var extracted = await _extractor.ExtractFromStreamAsync(memoryStream, "test.docx");

        Assert.Contains("Đặc tả Sprint Goal M2", extracted);
        Assert.Contains("Mã Task | Tiêu đề", extracted);
        Assert.Contains("TASK-1 | Cài đặt SSE Controller", extracted);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_UnsupportedExtension_ThrowsNotSupportedException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("unknown content"));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _extractor.ExtractFromStreamAsync(stream, "file.exe"));

        Assert.Contains("chưa được hỗ trợ", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_EmptyFileAndPastedText_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _extractor.ExtractAsync(null, "   "));

        Assert.Contains("Không tìm thấy nội dung văn bản", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_WithPastedText_ReturnsFormattedSection()
    {
        const string pastedText = "Cần ưu tiên hoàn thành task bảo mật zero-secret-leakage.";

        var extracted = await _extractor.ExtractAsync(null, pastedText);

        Assert.Contains("# GHI CHÚ / YÊU CẦU BỔ SUNG:", extracted);
        Assert.Contains(pastedText, extracted);
    }
}
