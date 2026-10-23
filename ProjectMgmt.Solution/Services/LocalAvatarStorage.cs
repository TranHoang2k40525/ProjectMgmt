using IdentityExperience.Application.IServices;

namespace ProjectMgmt.Solution.Services;

public class LocalAvatarStorage : IAvatarStorage
{
    private const string RequestPrefix = "/assets/avatars/";
    private static readonly Action<ILogger, string, Exception?> LogDeleteFailure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3001, "AvatarDeleteFailure"),
            "Không thể xóa ảnh đại diện cũ {AvatarFileName}.");

    private readonly string _avatarDirectory;
    private readonly ILogger<LocalAvatarStorage> _logger;

    public LocalAvatarStorage(
        IHostEnvironment environment,
        ILogger<LocalAvatarStorage> logger)
    {
        _avatarDirectory = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "Assets", "avatars"));
        _logger = logger;
    }

    public async Task<string> SaveAsync(Guid userId, Stream content, string extension)
    {
        Directory.CreateDirectory(_avatarDirectory);
        var fileName = $"{userId:N}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_avatarDirectory, fileName);

        await using var file = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81_920,
            FileOptions.Asynchronous);
        await content.CopyToAsync(file);
        await file.FlushAsync();
        return RequestPrefix + fileName;
    }

    public Task DeleteAsync(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl)
            || !avatarUrl.StartsWith(RequestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var fileName = avatarUrl[RequestPrefix.Length..];
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var filePath = Path.GetFullPath(Path.Combine(_avatarDirectory, fileName));
        if (!filePath.StartsWith(_avatarDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException exception)
        {
            LogDeleteFailure(_logger, fileName, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogDeleteFailure(_logger, fileName, exception);
        }

        return Task.CompletedTask;
    }
}
