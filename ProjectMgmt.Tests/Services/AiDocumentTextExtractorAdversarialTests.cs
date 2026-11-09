namespace ProjectMgmt.Tests.Services;

using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

/// <summary>
/// Challenger 2 Adversarial Stress Suite for AiDocumentTextExtractor.
/// Tests empty streams, corrupted archives, oversized files (>30MB),
/// binary garbage, special character sets, and cancellation tokens.
/// </summary>
public class AiDocumentTextExtractorAdversarialTests
{
    private readonly AiDocumentTextExtractor _extractor = new(NullLogger<AiDocumentTextExtractor>.Instance);

    #region Helpers

    private static IFormFile CreateFormFile(byte[] content, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private static IFormFile CreateOversizedMockFormFile(long declaredLength, string fileName)
    {
        // Creates a mock form file reporting a declared length without allocating 31MB in RAM
        return new DummyOversizedFormFile(declaredLength, fileName);
    }

    private sealed class DummyOversizedFormFile : IFormFile
    {
        public DummyOversizedFormFile(long length, string fileName)
        {
            Length = length;
            FileName = fileName;
        }

        public string ContentType => "application/octet-stream";
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length { get; }
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Stream OpenReadStream() => new MemoryStream(Encoding.UTF8.GetBytes("Dummy oversized payload content"));
    }

    #endregion

    #region 1. Empty Stream Challenges

    [Fact]
    public async Task ExtractFromStreamAsync_ZeroByteDocx_ThrowsCleanInvalidOperationException()
    {
        using var emptyStream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractFromStreamAsync(emptyStream, "empty.docx"));

        Assert.Contains("Không thể giải nén tài liệu Word .docx", ex.Message);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_ZeroBytePdf_ReturnsPlaceholderOrThrowsCleanException()
    {
        using var emptyStream = new MemoryStream();

        var result = await _extractor.ExtractFromStreamAsync(emptyStream, "empty.pdf");

        Assert.Equal("[Tài liệu PDF định dạng quét/hình ảnh hoặc bảo vệ mật khẩu]", result);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_ZeroByteXlsx_ReturnsEmptyWithoutCrashing()
    {
        using var emptyStream = new MemoryStream();

        var result = await _extractor.ExtractFromStreamAsync(emptyStream, "empty.xlsx");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_ZeroBytePlainText_ReturnsEmpty()
    {
        using var emptyStream = new MemoryStream();

        var resultTxt = await _extractor.ExtractFromStreamAsync(emptyStream, "empty.txt");
        var resultMd = await _extractor.ExtractFromStreamAsync(emptyStream, "empty.md");

        Assert.Empty(resultTxt);
        Assert.Empty(resultMd);
    }

    [Fact]
    public async Task ExtractAsync_ZeroByteFileAndWhitespaceNotes_ThrowsArgumentException()
    {
        var emptyFile = CreateFormFile(Array.Empty<byte>(), "empty.txt");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _extractor.ExtractAsync(emptyFile, "   \t\r\n  "));

        Assert.Contains("Không tìm thấy nội dung văn bản hợp lệ", ex.Message);
    }

    #endregion

    #region 2. Corrupted Archive Challenges

    [Fact]
    public async Task ExtractFromStreamAsync_DocxWithRandomBinaryGarbage_ThrowsCleanInvalidOperationException()
    {
        var randomGarbage = new byte[2048];
        new Random(42).NextBytes(randomGarbage);
        using var stream = new MemoryStream(randomGarbage);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractFromStreamAsync(stream, "corrupted.docx"));

        Assert.Contains("Không thể giải nén tài liệu Word .docx", ex.Message);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_DocxWithTruncatedZipHeader_ThrowsCleanInvalidOperationException()
    {
        // PK\x03\x04 followed by corrupted truncated data
        var truncatedZip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(truncatedZip);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractFromStreamAsync(stream, "truncated.docx"));

        Assert.Contains("Không thể giải nén tài liệu Word .docx", ex.Message);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_DocxZipWithoutDocumentXml_ReturnsEmptyString()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Zip contains another file, but NOT word/document.xml
            var entry = archive.CreateEntry("word/settings.xml");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes("<settings></settings>"));
        }

        memoryStream.Position = 0;
        var result = await _extractor.ExtractFromStreamAsync(memoryStream, "no_doc.docx");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_DocxWithMalformedXmlInDocumentEntry_ThrowsCleanInvalidOperationException()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            await using var entryStream = entry.Open();
            // Unclosed XML tag
            const string brokenXml = "<w:document><w:body><w:p><w:t>Unclosed text";
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes(brokenXml));
        }

        memoryStream.Position = 0;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractFromStreamAsync(memoryStream, "broken_xml.docx"));

        Assert.Contains("Không thể giải nén tài liệu Word .docx", ex.Message);
    }

    [Fact]
    public async Task ExtractFromStreamAsync_XlsxWithCorruptedData_DegradesGracefullyWithoutCrashing()
    {
        var randomGarbage = new byte[1024];
        new Random(99).NextBytes(randomGarbage);
        using var stream = new MemoryStream(randomGarbage);

        var result = await _extractor.ExtractFromStreamAsync(stream, "corrupt.xlsx");

        Assert.Empty(result);
    }

    #endregion

    #region 3. Oversized Files (>30MB) & Boundary Challenges

    [Fact]
    public async Task ExtractAsync_FileExceeding30MB_ThrowsInvalidOperationExceptionWithExactLimit()
    {
        const long thirtyOneMegabytes = (30 * 1024 * 1024) + 1;
        var oversizedFile = CreateOversizedMockFormFile(thirtyOneMegabytes, "massive_spec.docx");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractAsync(oversizedFile, null));

        Assert.Contains("Tệp tải lên vượt quá dung lượng cho phép (30MB)", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_HugeFile100MB_ThrowsInvalidOperationException()
    {
        const long hundredMegabytes = 100 * 1024 * 1024;
        var massiveFile = CreateOversizedMockFormFile(hundredMegabytes, "huge_requirements.pdf");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _extractor.ExtractAsync(massiveFile, "Some pasted text"));

        Assert.Contains("Tệp tải lên vượt quá dung lượng cho phép (30MB)", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_BoundaryCheck_Exact30MB_DoesNotThrowOversizedException()
    {
        const long exactThirtyMegabytes = 30 * 1024 * 1024;
        var boundaryFile = CreateOversizedMockFormFile(exactThirtyMegabytes, "valid_boundary.txt");

        // Should not throw InvalidOperationException for file size (opens stream and extracts)
        var result = await _extractor.ExtractAsync(boundaryFile, null);

        Assert.Contains("Dummy oversized payload content", result);
    }

    #endregion

    #region 4. Binary Garbage & Special Characters

    [Fact]
    public async Task ExtractFromStreamAsync_PlainTextWithNullBytesAndBinaryEntropy_DoesNotCrash()
    {
        var binaryBytes = new byte[] { 0x00, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0xFF, 0xFE, 0x00 };
        using var stream = new MemoryStream(binaryBytes);

        var result = await _extractor.ExtractFromStreamAsync(stream, "binary.txt");

        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public async Task ExtractAsync_VietnameseDiacriticsAndTones_PreservesExactAccents()
    {
        const string vietnameseText = "Đặc tả yêu cầu phần mềm: Quản lý Sprint, Phân rã Epic, Tiêu chí nghiệm thu chuẩn xác.";
        var file = CreateFormFile(Encoding.UTF8.GetBytes(vietnameseText), "srs_vi.txt");

        var extracted = await _extractor.ExtractAsync(file, "Ghi chú: Cần hoàn tất trước thứ Sáu.");

        Assert.Contains(vietnameseText, extracted);
        Assert.Contains("Cần hoàn tất trước thứ Sáu.", extracted);
    }

    [Fact]
    public async Task ExtractAsync_CJKCharactersAndInternationalUnicode_PreservesMultilingualText()
    {
        const string cjkText = "需求规格说明书 (Requirements) - 開発スプリント計画 - 스프린트 계획";
        var file = CreateFormFile(Encoding.UTF8.GetBytes(cjkText), "cjk_spec.md");

        var extracted = await _extractor.ExtractAsync(file, null);

        Assert.Contains(cjkText, extracted);
    }

    [Fact]
    public async Task ExtractAsync_XmlInjectionAndScriptVectors_PreservesContentWithoutSanitizationCrash()
    {
        const string injectionPayload = "<script>alert('xss')</script><w:p><![CDATA[malicious]]></w:p>";
        var file = CreateFormFile(Encoding.UTF8.GetBytes(injectionPayload), "injection.txt");

        var extracted = await _extractor.ExtractAsync(file, "' OR 1=1; DROP TABLE Projects; --");

        Assert.Contains("<script>alert('xss')</script>", extracted);
        Assert.Contains("DROP TABLE Projects", extracted);
    }

    #endregion

    #region 5. Cancellation Token Challenges

    [Fact]
    public async Task ExtractFromStreamAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Some text content"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _extractor.ExtractFromStreamAsync(stream, "test.txt", cts.Token));
    }

    [Fact]
    public async Task ExtractAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var file = CreateFormFile(Encoding.UTF8.GetBytes("Nội dung tài liệu"), "test.txt");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _extractor.ExtractAsync(file, "Pasted text", cts.Token));
    }

    #endregion
}
