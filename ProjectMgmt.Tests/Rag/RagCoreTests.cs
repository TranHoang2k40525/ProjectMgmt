using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DeliveryIntelligence.Infrastructure;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ProjectMgmt.Tests.Rag;

public class TextChunkerTests
{
    [Fact]
    public void Splits_long_text_within_size_with_overlap()
    {
        var paragraphs = Enumerable.Range(1, 40).Select(i => $"Yêu cầu số {i}: hệ thống phải lưu nhật ký thao tác của người dùng trong ít nhất chín mươi ngày.");
        var chunks = TextChunker.Split([new RagTextSegment(string.Join("\n\n", paragraphs))], chunkSize: 400, overlap: 80, maxChunks: 100);

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 400 + 80, $"Đoạn dài {chunk.Content.Length}"));
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.Index));
        // Phần cuối đoạn trước xuất hiện ở đầu đoạn sau.
        var tail = chunks[0].Content[^30..];
        Assert.Contains(tail.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last(), chunks[1].Content);
    }

    [Fact]
    public void Tracks_headings_and_never_mixes_pages()
    {
        var segments = new[]
        {
            new RagTextSegment("# 1. Đăng nhập\n\nNgười dùng đăng nhập bằng email và mật khẩu.", PageNumber: 1),
            new RagTextSegment("## 2. Phân quyền\n\nChủ tổ chức đổi vai trò thành viên.", PageNumber: 2)
        };
        var chunks = TextChunker.Split(segments, chunkSize: 1000, overlap: 100, maxChunks: 10);

        Assert.Equal(2, chunks.Count);
        Assert.Equal((1, "1. Đăng nhập"), (chunks[0].PageNumber!.Value, chunks[0].Heading));
        Assert.Equal((2, "2. Phân quyền"), (chunks[1].PageNumber!.Value, chunks[1].Heading));
        Assert.DoesNotContain("Phân quyền", chunks[0].Content);
    }

    [Fact]
    public void Short_sections_become_separate_chunks_with_their_own_heading()
    {
        var text = "# Nhắc lịch\n\nGửi email nhắc lịch trước giờ họp 30 phút.\n\n# Hủy đặt phòng\n\nĐược hủy trước 2 giờ.";
        var chunks = TextChunker.Split([new RagTextSegment(text)], chunkSize: 1200, overlap: 200, maxChunks: 10);

        Assert.Equal(["Nhắc lịch", "Hủy đặt phòng"], chunks.Select(chunk => chunk.Heading));
        Assert.Contains("30 phút", chunks[0].Content);
        Assert.DoesNotContain("30 phút", chunks[1].Content);
    }

    [Fact]
    public void Cuts_a_single_huge_paragraph_and_respects_max_chunks()
    {
        var chunks = TextChunker.Split([new RagTextSegment(new string('a', 5000))], chunkSize: 500, overlap: 0, maxChunks: 3);
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Content.Length <= 500));
    }
}

public class VectorMathTests
{
    [Fact]
    public void Normalized_vectors_round_trip_and_dot_is_cosine()
    {
        var a = VectorMath.Normalize([3f, 4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f]);
        var restored = VectorMath.FromBytes(VectorMath.ToBytes(a)).ToArray();

        Assert.Equal(a, restored);
        Assert.Equal(1f, VectorMath.Dot(a, a), 4);
        Assert.Equal(0f, VectorMath.Dot(VectorMath.Normalize([1f, 0f]), VectorMath.Normalize([0f, 1f])), 4);
    }

    [Fact]
    public void Rejects_zero_vector()
    {
        Assert.Throws<ArgumentException>(() => VectorMath.Normalize([0f, 0f]));
    }
}

public class DocumentChunkStoreTests
{
    private static readonly Guid ProjectA = Guid.NewGuid();
    private static readonly Guid ProjectB = Guid.NewGuid();

    private static DeliveryIntelligenceDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static RagIndexedChunk Chunk(int index, string text, params float[] vector) => new(new RagChunk(index, text, "Mục", 1), vector);

    [Fact]
    public async Task Search_ranks_by_similarity_and_stays_inside_the_project()
    {
        await using var db = NewDb();
        var store = new DocumentChunkStore(db);
        var docA = Guid.NewGuid();
        await store.ReplaceDocumentAsync(ProjectA, docA, "SRS.pdf", new string('a', 64), "m", [
            Chunk(0, "đăng nhập", 1f, 0f, 0f),
            Chunk(1, "báo cáo", 0f, 1f, 0f),
            Chunk(2, "gần đăng nhập", 0.9f, 0.1f, 0f)
        ]);
        // Cùng vector nhưng thuộc dự án khác: không bao giờ được trả về.
        await store.ReplaceDocumentAsync(ProjectB, Guid.NewGuid(), "Bí mật.pdf", new string('b', 64), "m", [Chunk(0, "dự án khác", 1f, 0f, 0f)]);

        var hits = await store.SearchAsync(ProjectA, "m", VectorMath.Normalize([1f, 0f, 0f]), topK: 2, minScore: 0.2);

        Assert.Equal(["đăng nhập", "gần đăng nhập"], hits.Select(hit => hit.Content));
        Assert.All(hits, hit => Assert.Equal(docA, hit.DocumentId));
        Assert.Equal(1.0, hits[0].Score, 3);
    }

    [Fact]
    public async Task Search_ignores_other_models_and_low_scores()
    {
        await using var db = NewDb();
        var store = new DocumentChunkStore(db);
        await store.ReplaceDocumentAsync(ProjectA, Guid.NewGuid(), "a.md", new string('a', 64), "model-cu", [Chunk(0, "cũ", 1f, 0f)]);
        await store.ReplaceDocumentAsync(ProjectA, Guid.NewGuid(), "b.md", new string('b', 64), "model-moi", [Chunk(0, "vuông góc", 0f, 1f)]);

        Assert.Empty(await store.SearchAsync(ProjectA, "model-moi", VectorMath.Normalize([1f, 0f]), 5, minScore: 0.2));
    }

    [Fact]
    public async Task Replace_and_remove_keep_one_version_per_document()
    {
        await using var db = NewDb();
        var store = new DocumentChunkStore(db);
        var kept = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        await store.ReplaceDocumentAsync(ProjectA, kept, "a.md", new string('1', 64), "m", [Chunk(0, "v1", 1f, 0f), Chunk(1, "v1b", 0f, 1f)]);
        await store.ReplaceDocumentAsync(ProjectA, kept, "a.md", new string('2', 64), "m", [Chunk(0, "v2", 1f, 0f)]);
        await store.ReplaceDocumentAsync(ProjectA, deleted, "b.md", new string('3', 64), "m", [Chunk(0, "xóa", 1f, 0f)]);

        Assert.Equal(1, await store.RemoveDocumentsExceptAsync(ProjectA, new HashSet<Guid> { kept }));
        var versions = await store.GetIndexedVersionsAsync(ProjectA);
        Assert.Equal(new string('2', 64), Assert.Single(versions).Value);
        Assert.Equal((1, 1), await store.CountAsync(ProjectA));
    }
}

public class OpenAiCompatibleClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request, Body);
        }
    }

    private static RagEndpointOptions Options(string endpoint = "https://ai.example.test/v1") =>
        new() { Provider = "OpenAi", Endpoint = endpoint, Model = "text-embedding-3-small", ApiKey = "sk-test-secret", Dimensions = 3 };

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Embedding_request_follows_configured_endpoint_and_orders_by_index()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK,
            """{"data":[{"index":1,"embedding":[0,1,0]},{"index":0,"embedding":[1,0,0]}]}"""));
        var client = new OpenAiCompatibleEmbeddingClient(new HttpClient(handler), Options());

        var vectors = await client.EmbedAsync(["một", "hai"]);

        Assert.Equal("https://ai.example.test/v1/embeddings", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.NotNull(handler.Request.Content!.Headers.ContentLength);
        var body = JsonNode.Parse(handler.Body)!;
        Assert.Equal(("text-embedding-3-small", 3, 2), (body["model"]!.GetValue<string>(), body["dimensions"]!.GetValue<int>(), body["input"]!.AsArray().Count));
        Assert.Equal([1f, 0f, 0f], vectors[0]);
        Assert.Equal("text-embedding-3-small@3", client.Model);
    }

    [Fact]
    public async Task Provider_error_is_reported_without_leaking_the_key()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided"}}"""));
        var client = new OpenAiCompatibleEmbeddingClient(new HttpClient(handler), Options());

        var error = await Assert.ThrowsAsync<RagProviderException>(() => client.EmbedAsync(["x"]));

        Assert.Equal(401, error.StatusCode);
        Assert.Contains("Incorrect API key", error.Message);
        Assert.DoesNotContain("sk-test-secret", error.Message);
    }

    [Fact]
    public async Task Vision_sends_image_as_data_url_and_returns_text()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"  Sơ đồ đăng nhập  "}}]}"""));
        var options = Options();
        options.Model = "gpt-4o-mini";
        var reader = new OpenAiCompatibleVisionClient(new HttpClient(handler), options);

        var text = await reader.ReadAsync([1, 2, 3], "image/png");

        Assert.Equal("Sơ đồ đăng nhập", text);
        Assert.Equal("https://ai.example.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Contains("data:image/png;base64,AQID", handler.Body);
    }

    [Theory]
    [InlineData("", "m", false)]
    [InlineData("ftp://ai.example.test", "m", false)]
    [InlineData("https://ai.example.test/v1", "", false)]
    [InlineData("http://localhost:11434/v1", "nomic-embed-text", true)]
    public void Endpoint_is_configured_only_with_http_url_and_model(string endpoint, string model, bool expected)
    {
        Assert.Equal(expected, new RagEndpointOptions { Endpoint = endpoint, Model = model }.IsConfigured);
    }
}
