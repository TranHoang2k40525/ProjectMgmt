namespace ProjectMgmt.Solution.Services.Ai.Adapters;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure;
using ProjectMgmt.Core.VectorSearch;

public sealed class SpecHubKeywordFallbackProvider : ISpecKeywordFallbackProvider
{
    private readonly PlanningDbContext _planningContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SpecHubKeywordFallbackProvider> _logger;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "và", "là", "các", "những", "cho", "với", "được", "trong", "của", "đến", "khi",
        "the", "and", "in", "to", "of", "for", "with", "at", "by", "from"
    };

    public SpecHubKeywordFallbackProvider(
        PlanningDbContext planningContext,
        IHostEnvironment environment,
        ILogger<SpecHubKeywordFallbackProvider> logger)
    {
        _planningContext = planningContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchKeywordAsync(
        Guid projectId,
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<VectorSearchResult>();
        }

        var terms = Regex.Matches(query.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(m => m.Value)
            .Where(t => t.Length >= 2 && !StopWords.Contains(t))
            .Distinct()
            .ToList();

        if (terms.Count == 0)
        {
            return Array.Empty<VectorSearchResult>();
        }

        try
        {
            var documents = await _planningContext.ProjectDocuments
                .AsNoTracking()
                .Where(d => d.ProjectId == projectId && !d.IsDirectory)
                .ToListAsync(ct);

            if (documents.Count == 0)
            {
                return Array.Empty<VectorSearchResult>();
            }

            var scoredHits = new List<(double Score, VectorSearchResult Result)>();

            foreach (var doc in documents)
            {
                var title = string.IsNullOrWhiteSpace(doc.Title) ? doc.Name : doc.Title;
                var content = doc.Content ?? string.Empty;

                if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrEmpty(doc.RelativePath))
                {
                    var filePath = Path.Combine(_environment.ContentRootPath, "Spec_Hub", doc.RelativePath.TrimStart('/', '\\'));
                    if (File.Exists(filePath) && doc.Extension is ".txt" or ".md" or ".json")
                    {
                        try { content = await File.ReadAllTextAsync(filePath, ct); } catch { /* ignore read failure */ }
                    }
                }

                double score = 0;
                int snippetIndex = -1;

                foreach (var term in terms)
                {
                    if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.5;
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    foreach (var term in terms)
                    {
                        var idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            score += 0.2;
                            if (snippetIndex == -1) snippetIndex = idx;
                        }
                    }
                }

                if (score > 0)
                {
                    string snippet;
                    if (snippetIndex >= 0 && snippetIndex < content.Length)
                    {
                        var start = Math.Max(0, snippetIndex - 50);
                        var length = Math.Min(300, content.Length - start);
                        snippet = (start > 0 ? "..." : "") + content.Substring(start, length).Trim() + (start + length < content.Length ? "..." : "");
                    }
                    else
                    {
                        snippet = content.Length > 300 ? content[..300] + "..." : content;
                    }

                    var metadata = new Dictionary<string, string>
                    {
                        ["status"] = doc.Status ?? "Draft",
                        ["version"] = doc.Version.ToString(),
                        ["extension"] = doc.Extension ?? string.Empty
                    };

                    scoredHits.Add((
                        score,
                        new VectorSearchResult
                        {
                            Id = doc.Id.ToString(),
                            Title = title,
                            ContentSnippet = string.IsNullOrWhiteSpace(snippet) ? title : snippet,
                            SourcePath = doc.RelativePath ?? doc.Name,
                            Score = Math.Min(1.0, score),
                            Metadata = metadata
                        }));
                }
            }

            return scoredHits
                .OrderByDescending(h => h.Score)
                .Select(h => h.Result)
                .Take(topK)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SpecHubFallback] Lỗi tìm kiếm từ khóa cho ProjectId={ProjectId}", projectId);
            return Array.Empty<VectorSearchResult>();
        }
    }
}
