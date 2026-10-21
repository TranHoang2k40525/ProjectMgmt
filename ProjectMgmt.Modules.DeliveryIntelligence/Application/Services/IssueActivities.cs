using System.Globalization;
using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Application.Services;

/// <summary>Tạo bản ghi ActivityLog thống nhất (Source thuộc 'User' | 'System' | 'AI' theo CK_ActivityLog_Source).</summary>
public static class IssueActivities
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string ParentChanged = "ParentChanged";
    public const string Ranked = "Ranked";
    public const string SprintChanged = "SprintChanged";
    public const string CommentAdded = "CommentAdded";
    public const string CommentDeleted = "CommentDeleted";
    public const string AttachmentAdded = "AttachmentAdded";
    public const string AttachmentRemoved = "AttachmentRemoved";
    public const string LabelAdded = "LabelAdded";
    public const string LabelRemoved = "LabelRemoved";
    public const string LinkAdded = "LinkAdded";
    public const string LinkRemoved = "LinkRemoved";

    public static ActivityLog Create(
        Guid issueId,
        Guid? actorUserId,
        string action,
        DateTime createdAtUtc,
        string? fieldName = null,
        object? oldValue = null,
        object? newValue = null,
        string source = "User",
        object? detail = null) => new()
    {
        Id = Guid.NewGuid(),
        IssueId = issueId,
        UserId = actorUserId,
        Action = action,
        FieldName = fieldName,
        OldValue = Format(oldValue),
        NewValue = Format(newValue),
        Detail = detail is null ? null : JsonSerializer.Serialize(detail),
        Source = source,
        CreatedAt = createdAtUtc
    };

    /// <summary>So sánh từng trường và trả về một ActivityLog cho mỗi trường thay đổi.</summary>
    public static IEnumerable<ActivityLog> Changes(
        Guid issueId,
        Guid actorUserId,
        DateTime createdAtUtc,
        params (string Field, object? Old, object? New)[] fields) =>
        fields
            .Where(field => !Equals(Format(field.Old), Format(field.New)))
            .Select(field => Create(issueId, actorUserId, Updated, createdAtUtc, field.Field, field.Old, field.New));

    private static string? Format(object? value) => value switch
    {
        null => null,
        string text => text.Length > 1000 ? text[..1000] : text,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
