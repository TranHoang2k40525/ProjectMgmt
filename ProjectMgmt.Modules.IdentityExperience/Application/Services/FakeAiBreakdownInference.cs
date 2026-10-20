using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;

namespace IdentityExperience.Application.Services;

public class FakeAiBreakdownInference : IAiBreakdownInference
{
    public Task<List<AiBreakdownDto>> GenerateAsync(AiBreakdownDto source)
    {
        var storyTitle = string.IsNullOrWhiteSpace(source.StoryTitle)
            ? "yêu cầu"
            : source.StoryTitle.Trim();
        var contextNote = string.IsNullOrWhiteSpace(source.ProjectContext)
            ? "Tuân thủ kiến trúc và quy ước hiện có của dự án."
            : $"Bối cảnh cần tuân thủ: {source.ProjectContext.Trim()}";

        var suggestions = new List<AiBreakdownDto>
        {
            new()
            {
                TempId = "temp_1",
                Title = $"Phân tích và thiết kế kỹ thuật cho {storyTitle}",
                Description = $"Làm rõ dữ liệu vào/ra, quy tắc nghiệp vụ và các trường hợp biên. {contextNote}",
                EstimatedHours = 2,
                EstimatePoints = 1,
                AcceptanceCriteria =
                [
                    "Các quy tắc nghiệp vụ và trường hợp biên được mô tả rõ ràng.",
                    "Thiết kế xác định được ảnh hưởng tới API, dữ liệu và phân quyền."
                ],
                SuggestedSkills = ["analysis", "backend"]
            },
            new()
            {
                TempId = "temp_2",
                Title = $"Triển khai {storyTitle}",
                Description = "Hiện thực luồng nghiệp vụ chính, kiểm tra dữ liệu và xử lý lỗi theo chuẩn của hệ thống.",
                EstimatedHours = 4,
                EstimatePoints = 2,
                AcceptanceCriteria =
                [
                    "Luồng chính hoạt động từ API tới cơ sở dữ liệu.",
                    "Dữ liệu sai và người dùng thiếu quyền nhận mã lỗi phù hợp.",
                    "Không ghi secret hoặc dữ liệu nhạy cảm vào log."
                ],
                SuggestedSkills = ["dotnet", "mysql"]
            },
            new()
            {
                TempId = "temp_3",
                Title = $"Kiểm thử và hoàn thiện {storyTitle}",
                Description = "Bổ sung kiểm thử cho luồng thành công, thất bại và chống thực thi trùng; cập nhật tài liệu tích hợp.",
                EstimatedHours = 3,
                EstimatePoints = 2,
                AcceptanceCriteria =
                [
                    "Có kiểm thử cho luồng thành công và các lỗi quan trọng.",
                    "Build và toàn bộ kiểm thử liên quan đều đạt.",
                    "Tài liệu API phản ánh đúng hành vi đã triển khai."
                ],
                SuggestedSkills = ["testing", "documentation"]
            }
        };

        return Task.FromResult(suggestions);
    }
}
