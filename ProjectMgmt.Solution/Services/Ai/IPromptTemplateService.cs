namespace ProjectMgmt.Solution.Services.Ai;

public interface IPromptTemplateService
{
    /// <summary>
    /// Lấy nội dung mẫu prompt theo tên (hỗ trợ đọc file động từ đĩa để chỉnh sửa tức thì mà không cần build lại).
    /// </summary>
    Task<string> GetTemplateAsync(string templateName, CancellationToken ct = default);

    /// <summary>
    /// Thay thế các biến placeholder dạng {{VARIABLE}} trong mẫu prompt.
    /// </summary>
    Task<string> RenderTemplateAsync(string templateName, IDictionary<string, string> variables, CancellationToken ct = default);
}

