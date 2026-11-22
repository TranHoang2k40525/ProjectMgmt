namespace Planning.Infrastructure.Services;

public sealed record XlsxSheetDto(string Name, List<List<string>> Rows);
public sealed record XlsxPreviewDto(string FileName, List<XlsxSheetDto> Sheets);
public sealed record DocxPreviewDto(string FileName, string ExtractedHtml, string RawText);

public interface ISpecHubStorageService
{
    string GetProjectRootPath(string organizationName, string projectName);
    Task<string> EnsureProjectFolderAsync(string organizationName, string projectName, CancellationToken cancellationToken = default);
    Task<string> CreateFolderAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default);
    Task<string> WriteTextFileAsync(string organizationName, string projectName, string relativePath, string content, CancellationToken cancellationToken = default);
    Task<(string RelativePath, long FileSize)> SaveUploadedFileAsync(string organizationName, string projectName, string relativePath, Stream stream, CancellationToken cancellationToken = default);
    Task<string> ReadTextFileAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default);
    Task<DocxPreviewDto> ExtractDocxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default);
    Task<XlsxPreviewDto> ExtractXlsxPreviewAsync(string organizationName, string projectName, string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string organizationName, string projectName, string relativePath, bool isDirectory, CancellationToken cancellationToken = default);
    Task RenameAsync(string organizationName, string projectName, string oldRelativePath, string newRelativePath, bool isDirectory, CancellationToken cancellationToken = default);
}
