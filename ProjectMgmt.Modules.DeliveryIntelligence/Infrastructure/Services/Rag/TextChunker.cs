using System.Text;
using System.Text.RegularExpressions;

namespace DeliveryIntelligence.Infrastructure.Services.Rag;

/// <summary>Một khối văn bản trích từ tài liệu: một trang PDF, một ảnh, một sheet hoặc cả file văn bản.</summary>
public sealed record RagTextSegment(string Text, int? PageNumber = null, string? Heading = null);

public sealed record RagChunk(int Index, string Content, string? Heading, int? PageNumber);

/// <summary>
/// Chia văn bản thành các đoạn ~ChunkSize ký tự theo ranh giới đoạn văn/câu, gối đầu ChunkOverlap ký tự
/// để câu hỏi chạm ranh giới vẫn tìm thấy. Mỗi đoạn thuộc đúng một trang và nhớ đề mục gần nhất.
/// </summary>
public static partial class TextChunker
{
    public static IReadOnlyList<RagChunk> Split(IEnumerable<RagTextSegment> segments, int chunkSize, int overlap, int maxChunks)
    {
        chunkSize = Math.Max(200, chunkSize);
        overlap = Math.Clamp(overlap, 0, chunkSize / 2);
        var chunks = new List<RagChunk>();
        var buffer = new StringBuilder();
        string? heading = null;
        string? chunkHeading = null;
        int? page = null;
        var carried = 0; // độ dài phần gối đầu mang sang từ đoạn trước

        void Flush(bool keepOverlap)
        {
            var content = buffer.ToString().Trim();
            if (buffer.Length > carried && content.Length > 0 && chunks.Count < maxChunks) chunks.Add(new RagChunk(chunks.Count, content, chunkHeading, page));
            buffer.Clear();
            carried = 0;
            if (keepOverlap && overlap > 0 && content.Length > overlap)
            {
                var tail = content[^overlap..];
                var cut = tail.IndexOfAny([' ', '\n']);
                buffer.Append(cut >= 0 ? tail[(cut + 1)..] : tail).Append('\n');
                carried = buffer.Length;
            }
            chunkHeading = heading;
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Text)) continue;
            if (buffer.Length > 0 && segment.PageNumber != page) Flush(keepOverlap: false);
            page = segment.PageNumber;
            if (segment.Heading is not null) heading = segment.Heading;
            chunkHeading ??= heading;

            foreach (var paragraph in Paragraphs(segment.Text))
            {
                if (IsHeading(paragraph))
                {
                    // Đề mục mới mở đoạn mới (không gối đầu sang mục khác) để mỗi đoạn mang đúng đề mục của nó.
                    if (buffer.Length > carried) Flush(keepOverlap: false);
                    buffer.Clear();
                    carried = 0;
                    heading = Truncate(paragraph.TrimStart('#', ' ').Trim('[', ']'), 500);
                    chunkHeading = heading;
                }
                foreach (var piece in Pieces(paragraph, chunkSize))
                {
                    if (buffer.Length > 0 && buffer.Length + piece.Length + 1 > chunkSize) Flush(keepOverlap: true);
                    buffer.Append(piece).Append('\n');
                }
                if (chunks.Count >= maxChunks) return chunks;
            }
        }
        Flush(keepOverlap: false);
        return chunks;
    }

    private static IEnumerable<string> Paragraphs(string text) =>
        ParagraphBreak().Split(text.Replace("\r\n", "\n").Replace('\r', '\n'))
            .Select(paragraph => SpaceRun().Replace(paragraph, " ").Trim())
            .Where(paragraph => paragraph.Length > 0);

    /// <summary>Đoạn dài hơn chunkSize: tách theo câu, câu vẫn dài thì cắt cứng.</summary>
    private static IEnumerable<string> Pieces(string paragraph, int chunkSize)
    {
        if (paragraph.Length <= chunkSize)
        {
            yield return paragraph;
            yield break;
        }
        var current = new StringBuilder();
        foreach (var sentence in SentenceEnd().Split(paragraph))
        {
            for (var start = 0; start < sentence.Length; start += chunkSize)
            {
                var part = sentence.Substring(start, Math.Min(chunkSize, sentence.Length - start));
                if (current.Length > 0 && current.Length + part.Length + 1 > chunkSize)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }
                current.Append(part).Append(' ');
            }
        }
        if (current.Length > 0) yield return current.ToString().Trim();
    }

    private static bool IsHeading(string paragraph) =>
        paragraph.Length <= 200 && !paragraph.Contains('\n')
        && (paragraph.StartsWith('#') || (paragraph.StartsWith('[') && paragraph.EndsWith(']')) || NumberedHeading().IsMatch(paragraph));

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpaceRun();

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceEnd();

    /// <summary>"1. Giới thiệu", "2.3 Yêu cầu chức năng" — đề mục đánh số ngắn, không kết thúc bằng dấu chấm câu.</summary>
    [GeneratedRegex(@"^\d+(\.\d+)*\.?\s+\S.{0,100}[^.:;,]$")]
    private static partial Regex NumberedHeading();
}
