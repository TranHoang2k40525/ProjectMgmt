using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;

namespace IdentityExperience.Application.Services;

public class ForYouServices : IForYouServices
{
    private readonly IUserWorkReader _userWorkReader;

    public ForYouServices(IUserWorkReader userWorkReader)
    {
        _userWorkReader = userWorkReader;
    }

    public async Task<ForYouDto> GetAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return new ForYouDto
            {
                Success = false,
                ErrorCode = "AUTH_UNAUTHENTICATED",
                Message = "Phiên đăng nhập không hợp lệ."
            };
        }

        var overview = await _userWorkReader.GetOverviewAsync(userId);
        var attention = new List<string>();
        if (overview.OverdueCount > 0)
        {
            attention.Add($"Bạn có {overview.OverdueCount} công việc quá hạn cần xử lý.");
        }

        if (overview.DueSoonCount > 0)
        {
            attention.Add($"Bạn có {overview.DueSoonCount} công việc đến hạn trong 3 ngày tới.");
        }

        if (overview.ActiveCount > 0)
        {
            attention.Add($"Bạn đang có {overview.ActiveCount} công việc chưa hoàn thành.");
        }

        if (attention.Count == 0)
        {
            attention.Add("Hiện không có công việc khẩn cấp cần chú ý.");
        }

        return new ForYouDto
        {
            Success = true,
            AssignedIssues = overview.AssignedIssues.Select(Map).ToList(),
            RecentIssues = overview.RecentIssues.Select(Map).ToList(),
            AttentionFocus = attention
        };
    }

    private static ForYouDto Map(UserWorkItem item)
    {
        return new ForYouDto
        {
            IssueId = item.IssueId,
            ProjectId = item.ProjectId,
            IssueKey = item.IssueKey,
            Title = item.Title,
            StatusName = item.StatusName,
            DueDate = item.DueDate
        };
    }
}
