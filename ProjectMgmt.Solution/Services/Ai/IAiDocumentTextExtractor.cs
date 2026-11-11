namespace ProjectMgmt.Solution.Services.Ai;

using Microsoft.AspNetCore.Http;

public interface IAiDocumentTextExtractor
{
    Task<string> ExtractAsync(IFormFile? file, string? pastedText, CancellationToken ct = default);
    Task<string> ExtractFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default);
}
