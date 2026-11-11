using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectMgmt.Solution.Security;
using ProjectMgmt.Solution.Services.Ai.Rag;

namespace ProjectMgmt.Solution.Controllers;

public sealed class RagSearchRequest
{
    [Required, StringLength(1000, MinimumLength = 2)] public string Query { get; set; } = "";
    [Range(1, 20)] public int? TopK { get; set; }
}

/// <summary>Trạng thái, đánh chỉ mục thủ công và tìm kiếm ngữ nghĩa trên tài liệu Spec Hub của một dự án.</summary>
[Route("api/projects/{projectId:guid}/spec-hub/rag")]
[Route("api/v1/projects/{projectId:guid}/spec-hub/rag")]
public sealed class SpecHubRagController(
    ProjectAccess access,
    DocumentChunkStore store,
    IOptions<RagOptions> options,
    IServiceProvider services) : ApiControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(Guid projectId, CancellationToken ct)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();
        var (documents, chunks) = await store.CountAsync(projectId, ct);
        var settings = options.Value;
        return Ok(new
        {
            enabled = settings.IsSearchReady,
            embeddingProvider = settings.Embedding.Provider,
            embeddingModel = settings.Embedding.Model,
            visionEnabled = settings.IsVisionReady,
            indexedDocuments = documents,
            chunks
        });
    }

    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex(Guid projectId, CancellationToken ct)
    {
        if (!await access.AllowsAsync(projectId, "project.settings.manage")) return Forbid();
        if (!options.Value.IsSearchReady) return RagNotConfigured();
        var result = await services.GetRequiredService<SpecDocumentIndexer>().SyncProjectAsync(projectId, ct);
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(Guid projectId, [FromBody] RagSearchRequest request, CancellationToken ct)
    {
        if (!await access.AllowsAsync(projectId)) return Forbid();
        if (!options.Value.IsSearchReady) return RagNotConfigured();
        try
        {
            var hits = await services.GetRequiredService<RagSearchService>().SearchAsync(projectId, request.Query, request.TopK, ct);
            return Ok(hits);
        }
        catch (RagProviderException ex)
        {
            return Problem(title: "Không gọi được API embedding.", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private ObjectResult RagNotConfigured() =>
        Problem(title: "Chưa bật tìm kiếm ngữ nghĩa.", detail: "Cấu hình Rag:Enabled, Rag:Embedding:Endpoint/Model/ApiKey trong appsettings hoặc biến môi trường.",
            statusCode: StatusCodes.Status409Conflict);
}
