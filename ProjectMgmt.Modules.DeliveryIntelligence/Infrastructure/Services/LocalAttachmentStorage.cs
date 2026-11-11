using DeliveryIntelligence.Application.IServices;

namespace DeliveryIntelligence.Infrastructure.Services;

/// <summary>
/// Lưu tệp đính kèm trên đĩa ngoài webroot (mặc định ContentRoot/App_Data/attachments, Git bỏ qua).
/// Tên lưu luôn là "{Guid}{.ext}" do service sinh; mọi đường dẫn được chuẩn hóa về đúng thư mục gốc.
/// </summary>
public class LocalAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public LocalAttachmentStorage(string rootPath)
    {
        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string storedName, Stream content)
    {
        var target = Resolve(storedName);
        try
        {
            await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file);
        }
        catch
        {
            File.Delete(target);
            throw;
        }
    }

    public Stream? OpenRead(string storedName)
    {
        var target = Resolve(storedName);
        return File.Exists(target) ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public void Delete(string storedName)
    {
        var target = Resolve(storedName);
        if (File.Exists(target))
        {
            File.Delete(target);
        }
    }

    private string Resolve(string storedName)
    {
        var path = Path.GetFullPath(Path.Combine(_root, Path.GetFileName(storedName.Replace('\\', '/'))));
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Length == 0)
        {
            throw new InvalidOperationException("Tên tệp lưu trữ không hợp lệ.");
        }

        return path;
    }
}
