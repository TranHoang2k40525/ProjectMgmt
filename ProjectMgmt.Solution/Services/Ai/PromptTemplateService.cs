namespace ProjectMgmt.Solution.Services.Ai;

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

public class PromptTemplateService : IPromptTemplateService
{
    private readonly ILogger<PromptTemplateService> _logger;
    private readonly ConcurrentDictionary<string, (string Content, DateTime LastModifiedUtc)> _cache = new();
    private readonly string[] _searchDirectories;

    public PromptTemplateService(ILogger<PromptTemplateService> logger)
    {
        _logger = logger;

        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = FindRepoRoot(baseDir);

        _searchDirectories = new[]
        {
            Path.Combine(repoRoot, "ProjectMgmt.Solution", "Prompts"),
            Path.Combine(currentDir, "Prompts"),
            Path.Combine(baseDir, "Prompts"),
            Path.Combine(currentDir, "ProjectMgmt.Solution", "Prompts")
        }.Distinct().Where(Directory.Exists).ToArray();
    }

    public async Task<string> GetTemplateAsync(string templateName, CancellationToken ct = default)
    {
        var fileName = templateName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : $"{templateName}.md";

        foreach (var dir in _searchDirectories)
        {
            var filePath = Path.Combine(dir, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(filePath);
                    if (_cache.TryGetValue(filePath, out var cached) && cached.LastModifiedUtc >= lastWrite)
                    {
                        return cached.Content;
                    }

                    var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
                    _cache[filePath] = (content, lastWrite);
                    return content;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PromptTemplateService] Lỗi khi đọc mẫu prompt từ file {FilePath}", filePath);
                }
            }
        }

        _logger.LogWarning("[PromptTemplateService] Không tìm thấy file mẫu prompt '{FileName}' trong các thư mục tìm kiếm", fileName);
        return string.Empty;
    }

    public async Task<string> RenderTemplateAsync(string templateName, IDictionary<string, string> variables, CancellationToken ct = default)
    {
        var template = await GetTemplateAsync(templateName, ct);
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(template);
        foreach (var (key, value) in variables)
        {
            var placeholder = key.StartsWith("{{", StringComparison.Ordinal) && key.EndsWith("}}", StringComparison.Ordinal)
                ? key
                : $"{{{{{key}}}}}";

            sb.Replace(placeholder, value ?? string.Empty);
        }

        return sb.ToString();
    }

    private static string FindRepoRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ProjectMgmt.slnx")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return startPath;
    }
}
