using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.Models;

public class NotificationPage
{
    public List<Notification> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public static class NotificationTypes
{
    public const string ProjectInvitation = "ProjectInvitation";
    public const string ProjectRoleChanged = "ProjectRoleChanged";
    public const string ProjectRoleRevoked = "ProjectRoleRevoked";
    public const string PasswordChanged = "PasswordChanged";
}
