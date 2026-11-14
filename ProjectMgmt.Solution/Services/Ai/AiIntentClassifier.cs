using System.Text.RegularExpressions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.Dtos.Ai;

namespace ProjectMgmt.Solution.Services.Ai;

/// <summary>
/// Bộ phân loại ý định và rào chắn phạm vi nghiêm ngặt (Strict Scope Firewall) cho Trợ Lý AI Quản lý Dự án.
/// Vận hành theo nguyên tắc Default-Deny: mọi yêu cầu lập trình hộ, kiến thức ngoài lề, thao tác ngoài quyền hạn
/// hoặc câu bẫy từ khóa đồng âm đều bị từ chối bằng <c>out_of_scope_refusal</c> (hoặc <c>security_refusal</c>).
/// </summary>
internal static partial class AiIntentClassifier
{
    private static readonly string[] OffTopicLifeAndAcademicKeywords =
    [
        // Đời sống, ăn uống, du lịch, thời tiết, thể thao, giải trí, game
        "thời tiết", "dự báo thời tiết", "nhiệt độ hôm nay", "trời có mưa",
        "nấu ăn", "công thức nấu", "nấu phở", "nấu bún", "nấu cơm", "món ngon", "món ăn",
        "quán ăn", "ăn phở", "ăn gì", "ăn sáng", "ăn trưa", "ăn tối", "đi chợ", "nấu lẩu",
        "bánh mì", "trà sữa", "nhà hàng ngon", "thực đơn",
        "du lịch", "phượt", "khách sạn", "homestay", "vé máy bay", "đặt vé", "đặt phòng",
        "bóng đá", "world cup", "ngoại hạng anh", "champions league", "tỷ số trận", "lịch thi đấu", "cầu thủ",
        "xem phim", "rạp chiếu phim", "review phim", "bài hát", "lời bài hát", "ca sĩ", "nhóm nhạc", "idol",
        "liên quân", "free fire", "pubg", "valorant", "chơi game", "tài xỉu", "anime", "manga",
        "tử vi", "bói toán", "cung hoàng đạo", "phong thủy", "xem ngày tốt", "xổ số", "lô đề", "vietlott",
        "giảm cân", "tăng cân", "tập gym", "chạy bộ", "thể hình",
        "thời trang", "phối đồ", "mỹ phẩm", "skincare", "nội thất", "thiệp cưới", "đám cưới",
        "xe máy", "ô tô", "xe hơi", "điều hòa", "tủ lạnh", "máy giặt", "mã giảm giá", "shopee", "lazada",
        "dọn nhà", "rửa bát", "nuôi mèo", "nuôi chó", "chăm sóc cây", "trồng hoa",

        // Tài chính cá nhân, chứng khoán, crypto, bất động sản, pháp lý, y tế, tâm lý
        "chứng khoán", "cổ phiếu", "vnindex", "bitcoin", "crypto", "tiền ảo", "forex",
        "giá vàng", "tỷ giá đô", "lãi suất ngân hàng", "vay vốn", "bất động sản", "chung cư",
        "thuế thu nhập", "tư vấn pháp luật", "luật hôn nhân", "ly hôn",
        "triệu chứng bệnh", "uống thuốc gì", "đau đầu", "cảm cúm", "xét nghiệm máu", "huyết áp",
        "tư vấn tình cảm", "thất tình", "tán tỉnh", "tâm sự", "người yêu",

        // Sáng tác văn chương, copywriting, CV, đơn từ cá nhân
        "làm thơ", "viết thơ", "bài thơ", "sáng tác thơ", "thơ tình",
        "sáng tác nhạc", "kể chuyện cười", "kể chuyện", "truyện cổ tích", "truyện ngắn", "tiểu thuyết",
        "viết văn", "bài văn", "luận văn", "viết kịch bản", "kịch bản tiktok", "caption facebook",
        "bài quảng cáo", "chế meme", "chơi nối chữ", "câu đố", "test iq",
        "viết cv", "sửa cv", "đơn xin việc", "đơn xin nghỉ", "thư xin lỗi", "thư mời cưới", "lời chúc sinh nhật",

        // Học thuật Toán - Lý - Hóa - Sử - Địa - Sinh
        "giải phương trình", "hệ phương trình", "bất phương trình", "đạo hàm", "tích phân",
        "ma trận nghịch đảo", "định lý pytago", "bài tập toán", "bài tập lý", "bài tập hóa",
        "cân bằng phương trình", "bảng tuần hoàn", "hóa học", "vật lý lượng tử", "thuyết tương đối",
        "sinh học", "quang hợp", "tiến hóa", "chiến tranh thế giới", "lịch sử thế giới",
        "lịch sử việt nam", "địa lý tự nhiên", "thủ đô của", "ai phát minh", "el nino",
        "độc lập tự do", "trái đất", "mặt trăng", "mặt trời", "vũ trụ học",

        // Thao tác đa phương tiện / OS / SaaS ngoài & phá hoại tài khoản/DB
        "vẽ tranh", "vẽ ảnh", "tạo ảnh", "sinh ảnh", "tạo video", "tạo nhạc",
        "nhắn tin zalo", "đăng bài facebook", "mở youtube", "tìm kiếm trên google",
        "đặt báo thức", "cài win", "cài đặt windows", "diệt virus", "phá khóa", "hack ",
        "đổi mật khẩu", "tạo tài khoản user mới", "tạo user mới", "xóa tài khoản", "xóa user",
        "xóa toàn bộ dự án", "xóa vĩnh viễn dự án", "xóa sạch database", "xóa sprint khỏi",
        "bạn ăn cơm chưa", "bạn bao nhiêu tuổi", "ai sinh ra bạn", "elon musk", "donald trump", "sơn tùng",
        "giả vờ bạn là", "đóng vai "
    ];

    private static readonly string[] PolysemyLifeContextTraps =
    [
        "đi chợ", "nấu cơm", "nấu ăn", "rửa bát", "dọn nhà", "du lịch", "phượt", "khách sạn", "homestay",
        "giảm cân", "tăng cân", "tập gym", "chạy bộ 100m", "marathon", "thời trang", "quần áo", "nội thất",
        "phong thủy", "xây nhà", "trồng cây", "từ thiện", "tiệc cưới", "đám cưới", "sinh nhật",
        "xe máy", "ô tô", "điều hòa", "tủ lạnh", "máy giặt", "iphone bị treo", "wifi nhà",
        "facebook", "zalo", "gmail cá nhân", "tiktok", "instagram", "youtube", "shopee", "lazada",
        "bóng đá", "nhóm nhạc", "kpop", "bts", "ca sĩ", "free fire", "liên quân",
        "bài tập về nhà", "thi đại học", "học tiếng anh", "lái xe",
        "bất động sản", "vinhomes", "chứng khoán", "cổ phiếu", "bitcoin", "tiền ảo",
        "covid", "xét nghiệm máu", "huyết áp", "tiêu hóa", "iq", "nhân cách", "mã giảm giá", "hoàn tiền",
        "nuôi mèo", "nuôi chó", "bọ xít", "ngoài vườn"
    ];

    private static readonly string[] StrictProjectManagementKeywords =
    [
        "sprint", "backlog", "product backlog", "scrum", "agile", "kanban",
        "velocity", "vận tốc", "story point", "story points", "rollover", "overdues", "overdue", "quá hạn",
        "user story", "subtask", "sub-task", "việc con", "công việc", "đầu việc", "giao việc",
        "phân rã", "phân tách", "bóc tách", "kế hoạch sprint", "lập kế hoạch sprint", "mục tiêu sprint", "sprint goal",
        "đề xuất", "bảng đề xuất", "bộ lọc", "kho việc", "tồn đọng", "kế thừa",
        "workflow", "quy trình workflow", "quy trình done", "quy trình to do", "quy trình in progress",
        "mức ưu tiên", "ưu tiên", "priority", "trạng thái", "status",
        "issue type", "loại việc", "nhãn", "label", "tiêu chí nghiệm thu", "nghiệm thu", "acceptance criteria",
        "trích dẫn", "spec hub", "spechub", "đặc tả", "biên bản họp", "biên bản cuộc họp",
        "thành viên", "nhân sự", "đội ngũ", "người phụ trách", "phụ trách", "assignee",
        "năng lực", "capacity", "giờ công", "giờ khả dụng", "giờ rảnh", "ước lượng",
        "dự án", "qlda", "tiến độ", "excel", "xlsx", "xuất báo cáo", "đẩy vào backlog", "áp dụng vào sprint",
        "blocker", "burndown", "burn-down", "definition of ready", "definition of done",
        "ai đang rảnh", "bao nhiêu giờ rảnh", "còn rảnh", "công suất đội ngũ", "chia việc",
        "lên kế hoạch", "gom việc", "giao task", "bản nháp", "hội thoại trước", "bảng board", "thẻ công việc"
    ];

    private static readonly string[] SoftwareEngineeringTerms =
    [
        "api", "endpoint", "controller", "service", "backend", "frontend", "fullstack", "devops",
        "database", "mysql", "redis", "signalr", "jwt", "oauth", "oauth2", "rbac", "middleware",
        "authentication", "authorization", "webhook", "microservice", "angular", "dotnet", ".net",
        "playwright", "selenium", "cypress", "e2e", "swagger", "openapi", "srs", "brd", "prd",
        "module", "phân hệ", "tính năng", "chức năng", "kiến trúc", "hệ thống", "giao diện", "ui/ux",
        "kiểm thử", "bảo mật", "phân quyền", "đồng bộ", "thời gian thực", "real-time", "realtime",
        "migration", "index", "caching", "cache", "deployment", "ci/cd", "docker", "kubernetes",
        "@mention", "mention", "oauth2", "xss", "csrf", "brute force", "rate limit", "log", "audit log"
    ];

    [GeneratedRegex(@"(?i)\b(task|tasks|subtask|subtasks|epic|epics|sprint|sprints|backlog|backlogs|sp|srs|brd|prd|qa|scrum|kanban|qlda)\b")]
    private static partial Regex StrictScrumTokenRegex();

    [GeneratedRegex(@"(?i)\b(?:bug|story)\b")]
    private static partial Regex BugOrStoryTokenRegex();

    [GeneratedRegex(@"\b[a-z][a-z0-9]{1,9}-\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex IssueKeyTokenRegex();

    [GeneratedRegex(@"(?i)\b(?:ai\d+|task_0?\d+)\b")]
    private static partial Regex DraftTaskIdRegex();

    [GeneratedRegex(@"(?i)#?mem-\d+")]
    private static partial Regex MemoryRefRegex();

    [GeneratedRegex(@"(?i)^\s*(?:hãy\s+|giúp\s+|làm\s+ơn\s+|vui\s+lòng\s+|bạn\s+)?(?:viết|sinh|tạo|làm|chạy|sửa|fix|debug|refactor|tối\s+ưu|giải\s+thích|chuyển|đọc|kiểm\s+tra)?\s*code\b")]
    private static partial Regex StartsWithCodeCommandRegex();

    [GeneratedRegex(@"(?i)\b(?:viết|sinh|xuất|gửi|cho\s+tôi|giúp\s+tôi|làm\s+hộ|tạo\s+hộ|cung\s+cấp|đưa\s+ra)\s+(?:ra\s+)?(?:một\s+|đoạn\s+|file\s+|tệp\s+|các\s+|vài\s+)?(?:code|mã\s+nguồn|boilerplate|snippet|script|chương\s+trình|hàm|function|class|component|controller|service\s+class|stored\s+procedure|câu\s+(?:truy\s+vấn|lệnh)\s+sql|query\s+sql|html\s+css|html/css|mã\s+html|mã\s+css|dockerfile|docker-compose|bash\s+script|powershell\s+script|regex|biểu\s+thức\s+chính\s+quy|unit\s+test\s+cho\s+hàm)\b")]
    private static partial Regex DirectCodeArtifactGenerationRegex();

    [GeneratedRegex(@"(?i)\b(?:code\s+(?:cho\s+tôi|giúp\s+tôi|giúp\s+mình|hộ\s+tôi|giùm|ngay|luôn|đi|bằng\s+(?:c#|csharp|java|python|js|ts|typescript|javascript|go|golang|rust|php|c\+\+|react|angular|sql)|một\s+|chức\s+năng|tính\s+năng|task|công\s+việc|giao\s+diện|api)|lập\s+trình\b|(?:viết|sinh|sửa|fix|debug|refactor|tối\s+ưu|giải\s+thích|chuyển\s+đổi|convert)\s+(?:đoạn\s+|lỗi\s+)?(?:code|mã\s+nguồn|lỗi\s+compile|null\s*reference|syntax\s+error|stack\s+trace|bug\s+trong\s+code)|tạo\s+(?:một\s+|đoạn\s+)?(?:class\b|hàm\b|function\b|script\b|dockerfile|unit\s+test|snippet|component|controller|stored\s+procedure|bảng\s+sql|query\s+sql))\b")]
    private static partial Regex DirectCodeHelpRegex();

    [GeneratedRegex(@"(?i)\b(?:viết|tạo|sinh|lập\s+trình|sửa|fix|debug|giải\s+thích|chuyển\s+đổi|convert|tối\s+ưu|cài\s+đặt\s+thuật\s+toán)\b.*?\b(?:python|javascript|typescript|c#|csharp|java\b|golang|class\s+go\b|ngôn\s+ngữ\s+go\b|bằng\s+go\b|rust\b|php\b|c\+\+|react|angular|html|css|tailwind|scss|linq|ef\s+core|dockerfile|nginx|git\s+(?:rebase|cherry-pick|merge)|powershell|regex|dijkstra|bubble\s+sort|quicksort|fibonacci\b)\b")]
    private static partial Regex LanguageSpecificCodeTaskRegex();

    [GeneratedRegex(@"(?i)\b(?:ai\s+(?:đang\s+)?(?:làm|phụ\s+trách|code|xử\s+lý|test|rảnh)|(?:team|đội)(?:\s+(?:mình|dự\s+án))?\s+(?:có\s+(?:những\s+)?ai|gồm\s+ai|gồm\s+những\s+ai))\b")]
    private static partial Regex TeamRoleInquiryRegex();

    [GeneratedRegex(@"(?i)\b(?:xây\s+dựng|triển\s+khai|phát\s+triển|thiết\s+kế)\s+(?:một\s+|hệ\s+thống\s+)?(?:tính\s+năng|chức\s+năng|module|phân\s+hệ|api|giao\s+diện|màn\s+hình|luồng)\b|\b(?:api|chức\s+năng|tính\s+năng)\s+xuất\s+(?:báo\s+cáo|dữ\s+liệu|excel)\b")]
    private static partial Regex BuildFeatureOrApiRegex();

    [GeneratedRegex(@"(?i)\b(?:đẩy|đưa|lưu|chuyển|cho|nạp)\b.{0,55}\b(?:vào|sang)\s+(?:kho\s+|product\s+|hàng\s+chờ\s+)?backlog\b")]
    private static partial Regex PushBacklogCommandRegex();

    [GeneratedRegex(@"(?i)\b(?:áp\s+dụng|commit|đưa|đẩy|chuyển|cho|nạp|lưu)\b.{0,55}\b(?:vào|lên)\s+sprint\b")]
    private static partial Regex ApplySprintCommandRegex();

    [GeneratedRegex(@"(?i)\b(?:xóa|xoá|gỡ|gỡ\s+bỏ|loại\s+bỏ|rút|hoàn\s+tác|hủy\s+áp\s+dụng|rollback|revert|undo|delete|remove)\b")]
    private static partial Regex RemoveOrDeleteCommandVerbRegex();

    [GeneratedRegex(@"(?i)\b(?:công\s+việc|các\s+việc|những\s+việc|mấy\s+việc|đầu\s+việc|hạng\s+mục|task|tasks|subtask|subtasks|sub-task|việc\s+con|user\s+story|story|bug|bugs)\b")]
    private static partial Regex WorkItemRemovalNounRegex();

    [GeneratedRegex(@"(?i)\b(?:dịch\s+(?:đoạn\s+|câu\s+|bài\s+|từ\s+|văn\s+bản\s+)?(?:này\s+|sau\s+)?sang\s+tiếng|translate\s+.*\s+to|tiếng\s+(?:anh|nhật|hàn|trung|pháp)\s+nói\s+thế\s+nào|phát\s+âm\s+từ)\b")]
    private static partial Regex GeneralTranslationRegex();

    [GeneratedRegex(@"(?i)\b(?:vẽ|tạo|sinh)\s+(?:giúp\s+|cho\s+tôi\s+)?(?:một\s+)?(?:bức\s+)?(?:ảnh|hình|tranh|logo|poster|banner|avatar|video|clip)\b")]
    private static partial Regex MediaGenerationRegex();

    [GeneratedRegex(@"(?i)\b(?:gửi\s+(?:giúp\s+|hộ\s+)?email|nhắn\s+tin\s+(?:qua\s+)?(?:zalo|telegram|slack|teams)|tạo\s+lịch\s+(?:họp\s+)?google\s+calendar|tra\s+cứu\s+google)\b")]
    private static partial Regex ExternalAppActionRegex();

    [GeneratedRegex(@"(?i)^\s*(?:[\d\s+\-*/^=().%?]+$|(?:tính\s+)?\d+\s*[+\-*/x×÷^]\s*\d+.*)")]
    private static partial Regex PureMathExpressionRegex();

    [GeneratedRegex(@"(?i)\b(?:lập\s+kế\s+hoạch|lên\s+kế\s+hoạch|chuẩn\s+bị\s+kế\s+hoạch|tạo\s+kế\s+hoạch|đẩy\s+kế\s+hoạch|tạo\s+sprint\s+goal|tạo\s+epic|đề\s+xuất\s+(?:sprint|danh\s+sách\s+công\s+việc|bộ\s+công\s+việc)|lên\s+danh\s+sách\s+công\s+việc|tạo\s+danh\s+sách\s+công\s+việc|soạn\s+danh\s+sách|phân\s+tách|phân\s+rã|bóc\s+tách|tạo\s+task|sinh\s+task|thêm\s+task|tạo\s+việc|thêm\s+việc|sinh\s+việc|tạo\s+(?:các\s+)?(?:user\s+)?story|tạo\s+(?:các\s+)?đầu\s+việc|tạo\s+(?:các\s+)?công\s+việc|thêm\s+subtask|tạo\s+subtask|chia\s+nhỏ|chia\s+việc|chia\s+công\s+việc|gom\s+việc|giao\s+task)\b")]
    private static partial Regex ExplicitPlanningActionRegex();

    [GeneratedRegex(@"(?i)\b(?:sửa|đổi|thay\s+đổi|điều\s+chỉnh|thêm|xóa|bỏ|gỡ|tách|chia\s+nhỏ|gộp|tăng|giảm|gán|(?<!\bvận\s+)chuyển(?!\s+(?:đổi|hóa|khoản|tiền|phát)\b)|giao(?!\s+(?:diện|thức|dịch|hàng|nhận)\b)|làm\s+lại|cập\s+nhật|bổ\s+sung|giới\s+hạn|tối\s+đa|chỉ\s+giữ|chỉ\s+giao|không\s+giao|nâng\s+ưu\s+tiên|hạ\s+ưu\s+tiên|chỉnh\s+số|chỉnh\s+lại|loại\s+bỏ)\b")]
    private static partial Regex RefinementVerbRegex();

    [GeneratedRegex(@"(?i)\b(?:task|công\s+việc|việc|subtask|việc\s+con|sp|story\s+point|story\s+points|giờ|ưu\s+tiên|priority|trạng\s+thái|status|người\s+làm|phụ\s+trách|thành\s+viên|mô\s+tả|tiêu\s+chí|nghiệm\s+thu|sprint|backlog|epic|nhãn|tag|tags|đề\s+xuất|đầu\s+tiên|thứ\s+(?:hai|ba|tư|năm|\d+)|cuối\s+cùng|backend|frontend|qa|tester|ai\d+|task_0?\d+|[a-z]{2,10}-ai\d+)\b")]
    private static partial Regex ProposalTargetNounRegex();

    [GeneratedRegex(@"(?i)(?<!\bđề\s+)\b(export|download|tải\s*về|tải\s*xuống|tải\s*file|lấy\s*file|xuất\s*file|xuất\s*excel|xuất\s*ra|xuất\s*bảng|(?<!\bđề\s+)xuất\s*báo\s*cáo|(?<!\bđề\s+)xuất\s*danh\s*sách|(?<!\bđề\s+)xuất\s*dữ\s*liệu)\b")]
    private static partial Regex ExplicitExportPhraseRegex();

    [GeneratedRegex(@"(?i)(?<!\b(?:đề|truy|sản|đột|xác|năng|hiệu|tần|lãi|công|diễn|tái|kiểm|nhập)\s+)\bxuất\b(?!\s+\b(?:hiện|phát|sắc|xứ|bản|khẩu|viện|huyết|toán|cảnh|gia|tinh)\b)")]
    private static partial Regex BareExportVerbRegex();

    [GeneratedRegex(@"(?i)\b(pdf|word|docx|pptx|powerpoint)\b")]
    private static partial Regex UnsupportedExportFormatRegex();

    [GeneratedRegex(@"(?i)\bsprint\s*[-#:]?\s*\d+\b")]
    private static partial Regex SprintNumberLookupRegex();

    public static string Classify(
        string? userPrompt,
        string? docContent,
        AiSprintProposalDto? existingProposal)
    {
        var promptText = (userPrompt ?? string.Empty).Trim();
        var docText = AiIndexedMemoryEngine.StripExtractorHeader(docContent);
        if (!string.IsNullOrWhiteSpace(promptText) &&
            string.Equals(docText, promptText, StringComparison.OrdinalIgnoreCase))
        {
            docText = string.Empty;
        }

        // Bỏ qua câu prompt mặc định khi client chỉ đính kèm file để docText làm tín hiệu chính
        var isDefaultDocPrompt =
            promptText.Equals("Tạo Sprint Goal, Tasks và Subtasks chuẩn Scrum.", StringComparison.OrdinalIgnoreCase) ||
            promptText.Equals("Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.", StringComparison.OrdinalIgnoreCase);

        var hasAttachedDoc = !string.IsNullOrWhiteSpace(docText);
        var combined = (!string.IsNullOrWhiteSpace(promptText) && !isDefaultDocPrompt)
            ? (hasAttachedDoc ? $"{promptText}\n{docText}" : promptText)
            : (hasAttachedDoc ? docText : promptText);

        var lowerPrompt = promptText.ToLowerInvariant();
        var lower = combined.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(lower))
        {
            return "out_of_scope_refusal";
        }

        // 0. Guardrail Bảo mật (Prompt Injection, rò rỉ bí mật/System Prompt, SQLi/XSS, truy xuất chéo tổ chức)
        if (InputGuardrail.ContainsPromptInjectionOrExfiltration(combined, out _) ||
            InputGuardrail.ContainsUnsafeScript(combined) ||
            combined.Contains("[GUARDRAIL_FILTERED]", StringComparison.Ordinal))
        {
            return "security_refusal";
        }

        var hasActiveConversation = existingProposal?.ConversationHistory != null && existingProposal.ConversationHistory.Count > 0;
        var hasExistingTasks = existingProposal?.Tasks != null && existingProposal.Tasks.Count > 0;

        // Xác định văn bản đánh giá ý định chính (ưu tiên prompt người dùng nếu không phải default prompt)
        var primaryText = (!string.IsNullOrWhiteSpace(promptText) && !isDefaultDocPrompt) ? lowerPrompt : lower;
        var isSoftwareOrScrumContext = HasStrictPmDomain(lower, combined) || HasSoftwareEngineeringContext(lower);

        // 1. Chặn yêu cầu Lập trình hộ / Viết Code / Fix lỗi code / Viết SQL / DevOps trực tiếp
        if (IsDirectCodingOrTechExecutionRequest(primaryText, promptText))
        {
            return "out_of_scope_refusal";
        }

        // 2. Chặn chủ đề ngoài phạm vi & bẫy từ đồng âm (thơ, thời tiết, nấu ăn, du lịch, tài chính, y tế, thao tác ngoài...)
        if (IsExplicitOffTopicOrTrap(primaryText, hasAttachedDoc))
        {
            return "out_of_scope_refusal";
        }

        if (GeneralTranslationRegex().IsMatch(primaryText) &&
            !primaryText.Contains("đề xuất") &&
            !primaryText.Contains("spec") &&
            !primaryText.Contains("tài liệu") &&
            !primaryText.Contains("backlog"))
        {
            return "out_of_scope_refusal";
        }

        bool hasSoftwareOrFeaturePlanningFrame = ExplicitPlanningActionRegex().IsMatch(primaryText) ||
                                                 BuildFeatureOrApiRegex().IsMatch(primaryText) ||
                                                 HasStrictPmDomain(lower, combined);

        if (MediaGenerationRegex().IsMatch(primaryText) ||
            (ExternalAppActionRegex().IsMatch(primaryText) && !hasSoftwareOrFeaturePlanningFrame) ||
            PureMathExpressionRegex().IsMatch(primaryText))
        {
            return "out_of_scope_refusal";
        }

        bool isPureInquiryQuestion = IsPureInquiryQuestion(lower);

        // 3. Phân giải Xuất báo cáo Excel (export_excel) hoặc từ chối định dạng chưa hỗ trợ -> project_qa
        var exportCandidate = (!string.IsNullOrWhiteSpace(promptText) && !isDefaultDocPrompt)
            ? lowerPrompt
            : (docText.Length <= 380 ? docText.ToLowerInvariant() : string.Empty);

        if (!string.IsNullOrWhiteSpace(exportCandidate) && exportCandidate.Length <= 420)
        {
            var exportIntent = TryClassifyExportIntent(exportCandidate, existingProposal, isPureInquiryQuestion, isSoftwareOrScrumContext);
            if (exportIntent != null)
            {
                return exportIntent;
            }
        }

        // 3b. Gỡ khỏi Sprint / Xóa công việc thực tế trong dự án (luôn yêu cầu bảng xác nhận trước khi thực thi)
        if (!string.IsNullOrWhiteSpace(promptText) && promptText.Length <= 420)
        {
            var removalIntent = TryClassifyRemoveOrDeleteTasksIntent(
                promptText,
                lowerPrompt,
                existingProposal,
                isPureInquiryQuestion);
            if (removalIntent != null)
            {
                return removalIntent;
            }
        }

        // 4. Lệnh hành động trực tiếp trên đề xuất hiện có (push_backlog / apply_sprint)
        if (hasExistingTasks && lower.Length < 180 && !isPureInquiryQuestion)
        {
            bool asksToCreateOrModify = Regex.IsMatch(
                lower,
                @"(?i)\b(?:thêm\s+(?:task|việc|subtask|story|công\s+việc|một|1|\d+)|tạo\s+(?:task|việc|story|epic|backlog)|sinh\s+(?:task|việc)|phân\s+tách\s+.*\s+thành|phân\s+rã\s+.*\s+thành|chia\s+nhỏ|sửa\s+|xóa\s+|làm\s+lại|đổi\s+|cập\s+nhật|điều\s+chỉnh)\b");
            if (!asksToCreateOrModify)
            {
                if (PushBacklogCommandRegex().IsMatch(lower))
                {
                    return "push_backlog";
                }

                if (ApplySprintCommandRegex().IsMatch(lower))
                {
                    return "apply_sprint";
                }
            }
        }

        // 5. Chào hỏi & Hỏi khả năng của Trợ Lý AI
        bool isGreetingOrHelp = AiIndexedMemoryEngine.IsGreetingOrCapabilityQuestion(lower) &&
                                !AiIndexedMemoryEngine.ContainsProjectDataKeyword(lower) &&
                                !IsNonPmTutorialQuestion(lower);
        if (isGreetingOrHelp && !hasAttachedDoc)
        {
            return "project_qa";
        }

        // Khi người dùng nhắn chào ngắn kèm file đính kèm (ví dụ "hello" bên cạnh doc có sẵn), vẫn trả lời greeting QA
        if (AiIndexedMemoryEngine.IsGreetingOrCapabilityQuestion(lowerPrompt) &&
            !AiIndexedMemoryEngine.ContainsProjectDataKeyword(lowerPrompt) &&
            !IsNonPmTutorialQuestion(lowerPrompt) &&
            !isDefaultDocPrompt &&
            lowerPrompt.Length < 80)
        {
            return "project_qa";
        }

        bool hasStrictPm = HasStrictPmDomain(lower, combined);
        bool hasMemoryRecallRef = hasActiveConversation && HasMemoryRecallReference(lower);
        bool hasAnchoredFollowUpRef = hasActiveConversation && HasAnchoredFollowUpReference(lower);

        bool hasExplicitActionDirectivePrefix =
            ExplicitPlanningActionRegex().IsMatch(lower) ||
            IsBacklogTargetRequested(lower) ||
            (hasExistingTasks && Regex.IsMatch(lower, @"(?i)^\s*(?:hãy\s+)?(?:thêm|tạo|bổ\s+sung|sửa|đổi|xóa|bỏ|chỉnh|tăng|giảm|gán|gộp|tách|chuyển)\b"));

        bool rawImperativeQaVerb = lower.Contains("tóm tắt") || lower.Contains("liệt kê") ||
                                   lower.Contains("phân tích") || lower.Contains("đánh giá") ||
                                   lower.Contains("so sánh") || lower.Contains("giải thích") ||
                                   lower.Contains("làm rõ") || lower.Contains("rà soát") ||
                                   lower.Contains("kiểm tra xem") || lower.Contains("kiểm tra và") || lower.Contains("kiểm tra tình hình") ||
                                   lower.Contains("tổng hợp") || lower.Contains("đọc giúp") ||
                                   lower.Contains("đếm") || lower.Contains("thống kê") ||
                                   lower.Contains("nhận xét") || lower.Contains("cho xem") ||
                                   lower.Contains("hiển thị") || lower.Contains("tra cứu") ||
                                   lower.Contains("nói ngắn gọn");

        bool hasStrongQaOverrideVerb = lower.Contains("liệt kê") || lower.Contains("nhận xét") ||
                                       lower.Contains("làm rõ") || lower.Contains("nói ngắn gọn") ||
                                       lower.Contains("đọc giúp") || lower.Contains("tóm tắt lại") ||
                                       lower.Contains("giải thích");

        // Tránh hiểu nhầm từ khóa QA nằm bên trong tên task/module (VD: "module thống kê", "tạo task rà soát", "phân tích ... và phân rã")
        bool hasImperativeQaVerb = rawImperativeQaVerb &&
                                   (!hasExplicitActionDirectivePrefix || hasStrongQaOverrideVerb || lower.Contains('?'));

        bool hasQuestionOrQaDirective = isPureInquiryQuestion || hasImperativeQaVerb || lower.Contains('?');
        bool asksForDecomposition = ExplicitPlanningActionRegex().IsMatch(lower) && !hasQuestionOrQaDirective;
        bool asksForBacklogPlanning = IsBacklogTargetRequested(lower) && !isPureInquiryQuestion && !hasImperativeQaVerb && !lower.Contains('?');
        bool asksToModifyProposal = hasExistingTasks &&
                                    !isPureInquiryQuestion &&
                                    !hasImperativeQaVerb &&
                                    RefinementVerbRegex().IsMatch(lower) &&
                                    (hasStrictPm || ProposalTargetNounRegex().IsMatch(lower));

        // 6. Hỏi đáp dự án / Spec Hub / Sprint / Đề xuất / Phiên hội thoại (project_qa)
        bool isQuestionPattern = lower.Contains('?') ||
                                 hasMemoryRecallRef ||
                                 isPureInquiryQuestion ||
                                 lower.Contains("những ai") ||
                                 lower.Contains("cho hỏi") ||
                                 lower.Contains("cho tôi biết") ||
                                 lower.Contains("nhắc lại") ||
                                 lower.Contains("xem lại") ||
                                 lower.StartsWith("xem ", StringComparison.Ordinal) ||
                                 (lower.Contains("danh sách ") && !asksForDecomposition && !asksForBacklogPlanning) ||
                                 lower.Contains("tình hình ") ||
                                 lower.Contains("tổng quan") ||
                                 lower.Contains("có mấy") ||
                                 lower.Contains("có những") ||
                                 lower.Contains("gồm những") ||
                                 lower.Contains("đang dùng workflow") ||
                                 lower.Contains("mức ưu tiên nào") ||
                                 (hasAnchoredFollowUpRef && !asksToModifyProposal);

        bool isTerseProjectEntityLookup = !hasAttachedDoc &&
                                          lower.Length < 180 &&
                                          !asksForDecomposition &&
                                          !asksForBacklogPlanning &&
                                          !asksToModifyProposal &&
                                          !lower.Contains("biên bản") &&
                                          !lower.Contains("cuộc họp") &&
                                          (SprintNumberLookupRegex().IsMatch(lower) ||
                                           IssueKeyTokenRegex().IsMatch(promptText) ||
                                           (hasStrictPm &&
                                            (lower.Contains("nội dung") ||
                                             lower.Contains("chi tiết") ||
                                             lower.Contains("thông tin") ||
                                             lower.Contains("trạng thái") ||
                                             lower.Contains("tiến độ") ||
                                             lower.Contains("người làm") ||
                                             lower.Contains("phụ trách") ||
                                             lower.Contains("backlog") ||
                                             lower.Contains("epic") ||
                                             lower.Contains("workflow") ||
                                             lower.Contains("quy trình") ||
                                             lower.Contains("velocity") ||
                                             lower.Contains("năng lực") ||
                                             lower.Contains("nhân sự") ||
                                             lower.Contains("thành viên") ||
                                             lower.Contains("tài liệu") ||
                                             lower.Contains("spec"))));

        bool hasQaGrounding = hasStrictPm || hasMemoryRecallRef || hasAnchoredFollowUpRef;

        if ((isQuestionPattern || hasImperativeQaVerb || isTerseProjectEntityLookup) &&
            hasQaGrounding &&
            !asksForDecomposition &&
            !asksForBacklogPlanning &&
            !asksToModifyProposal &&
            lower.Length < 600)
        {
            return "project_qa";
        }

        // 7. Tinh chỉnh đề xuất hiện có trong phiên (refine_proposal) - CHỈ khi có chỉ dẫn chỉnh sửa hợp lệ
        bool asksForFreshSprintFromDocOrTranscript =
            hasAttachedDoc || lower.Contains("sprint tiếp theo") || lower.Contains("biên bản");
        bool isProposalSpecificRefactor =
            !asksForDecomposition ||
            (hasExistingTasks && !asksForFreshSprintFromDocOrTranscript && RefinementVerbRegex().IsMatch(lower) && !lower.Contains("lập kế hoạch") && !lower.Contains("tạo epic"));
        if (hasExistingTasks && asksToModifyProposal && isProposalSpecificRefactor && !asksForBacklogPlanning)
        {
            return "refine_proposal";
        }

        // 8. Lập kế hoạch đưa vào Kho Backlog (backlog_plan)
        if (IsBacklogTargetRequested(lower) && (hasStrictPm || HasSoftwareEngineeringContext(lower) || hasAttachedDoc))
        {
            return "backlog_plan";
        }

        // Khi phiên đang có đề xuất và người dùng gửi chỉ dẫn bổ sung/sửa rõ ràng về đề xuất hoặc tài liệu PM
        if (hasExistingTasks &&
            hasStrictPm &&
            !asksForDecomposition &&
            !isPureInquiryQuestion &&
            !IsExplicitOffTopicOrTrap(lower, hasAttachedDoc) &&
            (ProposalTargetNounRegex().IsMatch(lower) || RefinementVerbRegex().IsMatch(lower) || hasAttachedDoc))
        {
            return "refine_proposal";
        }

        // 9. Lập kế hoạch & phân rã Sprint (sprint_plan) - BẮT BUỘC có bằng chứng lập kế hoạch hoặc đặc tả phần mềm thực sự
        if (hasAttachedDoc && !IsPureOffTopicDocument(docText))
        {
            return "sprint_plan";
        }

        if (asksForDecomposition && (hasStrictPm || HasSoftwareEngineeringContext(lower)))
        {
            return "sprint_plan";
        }

        if (HasGenuineSoftwarePlanningInput(lower, hasStrictPm))
        {
            return "sprint_plan";
        }

        // 10. Default-Deny Strict Guardrail: Mọi đầu vào ngoài phạm vi hoặc vô nghĩa còn lại đều từ chối an toàn
        return "out_of_scope_refusal";
    }

    internal static bool ContainsTrueExportVerb(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (ExplicitExportPhraseRegex().IsMatch(text))
        {
            return true;
        }

        return BareExportVerbRegex().IsMatch(text);
    }

    internal static bool IsBacklogTargetRequested(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("vào kho backlog") ||
               lower.Contains("vào product backlog") ||
               lower.Contains("vào backlog") ||
               lower.Contains("đẩy vào backlog") ||
               lower.Contains("thêm vào backlog") ||
               lower.Contains("lưu vào backlog") ||
               lower.Contains("đưa vào backlog") ||
               lower.Contains("đưa vào kho backlog") ||
               lower.Contains("cho kho backlog") ||
               lower.Contains("tạo backlog");
    }

    private static string? TryClassifyRemoveOrDeleteTasksIntent(
        string promptText,
        string lowerPrompt,
        AiSprintProposalDto? existingProposal,
        bool isPureInquiryQuestion)
    {
        if (string.IsNullOrWhiteSpace(lowerPrompt)) return null;
        if (!RemoveOrDeleteCommandVerbRegex().IsMatch(lowerPrompt)) return null;

        // Tránh nhầm yêu cầu lập kế hoạch chức năng có chữ "xóa" / "gỡ lỗi" (VD: "Tạo task xây dựng tính năng xóa user")
        if (ExplicitPlanningActionRegex().IsMatch(lowerPrompt) ||
            BuildFeatureOrApiRegex().IsMatch(lowerPrompt))
        {
            return null;
        }

        // Tránh nhầm thao tác hệ thống ngoài phạm vi công việc (xóa cache, xóa phiên chat, xóa file đính kèm)
        if (lowerPrompt.Contains("xóa cache") || lowerPrompt.Contains("xoá cache") ||
            lowerPrompt.Contains("xóa bộ nhớ") || lowerPrompt.Contains("xóa phiên") ||
            lowerPrompt.Contains("xoá phiên") || lowerPrompt.Contains("xóa lịch sử") ||
            lowerPrompt.Contains("gỡ file") || lowerPrompt.Contains("xóa file"))
        {
            return null;
        }

        // Nếu đang hỏi cách làm / ai đã xóa / có nên gỡ hay không -> giữ nguyên project_qa
        bool isHowToOrQaQuestion =
            lowerPrompt.Contains("làm sao") || lowerPrompt.Contains("cách nào") ||
            lowerPrompt.Contains("làm thế nào") || lowerPrompt.Contains("ai đã xóa") ||
            lowerPrompt.Contains("ai vừa xóa") || lowerPrompt.Contains("đã xóa những gì") ||
            lowerPrompt.Contains("bị xóa chưa") || isPureInquiryQuestion;
        if (isHowToOrQaQuestion)
        {
            return null;
        }

        bool hasRealIssueKey = IssueKeyTokenRegex().Matches(promptText)
            .Any(m => !m.Value.Contains("-AI", StringComparison.OrdinalIgnoreCase));
        bool hasWorkItemTarget = WorkItemRemovalNounRegex().IsMatch(lowerPrompt) || hasRealIssueKey;
        if (!hasWorkItemTarget)
        {
            return null;
        }

        bool mentionsDraftProposal =
            lowerPrompt.Contains("đề xuất") || lowerPrompt.Contains("bản nháp") ||
            lowerPrompt.Contains("nháp") || lowerPrompt.Contains("bảng trên") ||
            DraftTaskIdRegex().IsMatch(lowerPrompt);

        bool mentionsAppliedOrCommittedAction =
            lowerPrompt.Contains("áp dụng") || lowerPrompt.Contains("apply") || lowerPrompt.Contains("commit") ||
            lowerPrompt.Contains("vừa đẩy") || lowerPrompt.Contains("mới đẩy") || lowerPrompt.Contains("đã đẩy") || lowerPrompt.Contains("bạn đẩy") ||
            lowerPrompt.Contains("hoàn tác") || lowerPrompt.Contains("rollback") || lowerPrompt.Contains("undo") || lowerPrompt.Contains("revert");

        bool mentionsRealProjectStorage =
            lowerPrompt.Contains("khỏi sprint") || lowerPrompt.Contains("ra khỏi sprint") ||
            lowerPrompt.Contains("trong sprint") || lowerPrompt.Contains("ở sprint") ||
            lowerPrompt.Contains("của sprint") || lowerPrompt.Contains("về backlog") ||
            lowerPrompt.Contains("về kho backlog") || lowerPrompt.Contains("trong backlog") ||
            lowerPrompt.Contains("trong kho backlog") || lowerPrompt.Contains("kho backlog") ||
            lowerPrompt.Contains("product backlog") || lowerPrompt.Contains("trong dự án") ||
            lowerPrompt.Contains("của dự án");

        bool hasExistingDraftTasks = existingProposal?.Tasks != null && existingProposal.Tasks.Count > 0;
        bool targetsRealProjectTasks =
            !mentionsDraftProposal &&
            (hasRealIssueKey || mentionsAppliedOrCommittedAction || mentionsRealProjectStorage);

        // Nếu đang mở bảng đề xuất nháp và người dùng không chỉ đích danh task thật/đã áp dụng -> để refine_proposal xử lý xóa dòng nháp
        if (hasExistingDraftTasks && !targetsRealProjectTasks)
        {
            return null;
        }

        return "remove_tasks";
    }

    private static string? TryClassifyExportIntent(
        string exportCandidate,
        AiSprintProposalDto? existingProposal,
        bool isPureInquiryQuestion,
        bool isSoftwareOrScrumContext)
    {
        bool mentionsExcelFormat = exportCandidate.Contains("excel") || exportCandidate.Contains(".xlsx") ||
                                   exportCandidate.Contains("xlsx") || exportCandidate.Contains("bảng tính");
        bool mentionsGenericFile = exportCandidate.Contains("ra file") || exportCandidate.Contains("thành file") ||
                                   exportCandidate.Contains("xuất file") || exportCandidate.Contains("tải file") ||
                                   exportCandidate.Contains("lấy file") || exportCandidate.Contains("cho tôi file") ||
                                   exportCandidate.Contains("export");
        bool mentionsExportVerb = ContainsTrueExportVerb(exportCandidate);
        bool mentionsProjectExportTarget =
            exportCandidate.Contains("sprint") || exportCandidate.Contains("backlog") || exportCandidate.Contains("hàng chờ") ||
            exportCandidate.Contains("task") || exportCandidate.Contains("công việc") || exportCandidate.Contains("việc") ||
            exportCandidate.Contains("chi tiết") || exportCandidate.Contains("nội dung") || exportCandidate.Contains("đề xuất") ||
            exportCandidate.Contains("dự án") || exportCandidate.Contains("bug") || exportCandidate.Contains("story") ||
            exportCandidate.Contains("epic") || IssueKeyTokenRegex().IsMatch(exportCandidate);
        bool mentionsUnsupportedFormat =
            !mentionsExcelFormat &&
            (exportCandidate.Contains(".pdf") || UnsupportedExportFormatRegex().IsMatch(exportCandidate));
        bool isHowToOrCapabilityQuestion =
            exportCandidate.Contains("làm sao") || exportCandidate.Contains("cách nào") ||
            exportCandidate.Contains("hướng dẫn") || exportCandidate.Contains("làm thế nào") ||
            exportCandidate.Contains("có hỗ trợ") || exportCandidate.Contains("tại sao") ||
            exportCandidate.Contains("vì sao") || exportCandidate.Contains("hỗ trợ những") ||
            (isPureInquiryQuestion && !mentionsExcelFormat && !mentionsGenericFile);
        bool asksToEditOrCritique =
            exportCandidate.Contains("đổi sang") || exportCandidate.Contains("chuyển sang") ||
            exportCandidate.Contains("nhận xét") || exportCandidate.Contains("đánh giá");

        var lastAssistantTurn = existingProposal?.ConversationHistory?
            .LastOrDefault(t => string.Equals(t.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        bool previousTurnWasExport = lastAssistantTurn != null &&
            (string.Equals(lastAssistantTurn.Intent, "export_excel", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(lastAssistantTurn.ActionType, "export_excel", StringComparison.OrdinalIgnoreCase));
        bool mentionsCustomFileNaming =
            exportCandidate.Contains("đặt tên") || exportCandidate.Contains("tên file") ||
            exportCandidate.Contains("tên tệp") || exportCandidate.Contains("lưu tên") ||
            exportCandidate.Contains("lưu thành");
        bool followUpRenameExport = previousTurnWasExport && (mentionsExportVerb || mentionsCustomFileNaming);
        bool explicitNamedFileExport = mentionsExportVerb && mentionsCustomFileNaming &&
                                       (previousTurnWasExport || isSoftwareOrScrumContext || exportCandidate.Contains("nội dung trên"));

        bool asksToBuildFeatureOrTask = ExplicitPlanningActionRegex().IsMatch(exportCandidate) ||
                                        BuildFeatureOrApiRegex().IsMatch(exportCandidate) ||
                                        IsBacklogTargetRequested(exportCandidate);
        if (asksToBuildFeatureOrTask || asksToEditOrCritique)
        {
            return null;
        }

        // Chặn yêu cầu xuất dữ liệu không thuộc dự án (VD: xuất excel bảng điểm học sinh / hóa đơn VAT / bảng lương)
        bool explicitNonProjectExportTarget =
            exportCandidate.Contains("bảng điểm") || exportCandidate.Contains("học sinh") || exportCandidate.Contains("sinh viên") ||
            exportCandidate.Contains("hóa đơn") || exportCandidate.Contains("vat") || exportCandidate.Contains("bảng lương") ||
            exportCandidate.Contains("bán hàng") || exportCandidate.Contains("tồn kho") || exportCandidate.Contains("chi tiêu cá nhân") ||
            exportCandidate.Contains("giá vàng") || exportCandidate.Contains("chứng khoán");
        if (explicitNonProjectExportTarget && !mentionsProjectExportTarget)
        {
            return "out_of_scope_refusal";
        }

        if (mentionsUnsupportedFormat && mentionsExportVerb && (isSoftwareOrScrumContext || mentionsProjectExportTarget))
        {
            return "project_qa";
        }

        if (!isHowToOrCapabilityQuestion && !mentionsUnsupportedFormat &&
            ((mentionsExcelFormat && (mentionsExportVerb || exportCandidate.Length < 45)) ||
             (mentionsGenericFile && mentionsExportVerb) ||
             (mentionsExportVerb && mentionsProjectExportTarget) ||
             followUpRenameExport ||
             explicitNamedFileExport))
        {
            return "export_excel";
        }

        return null;
    }

    private static bool IsDirectCodingOrTechExecutionRequest(string lower, string? rawText)
    {
        if (string.IsNullOrWhiteSpace(lower)) return false;

        bool hasScrumManagementAction = ExplicitPlanningActionRegex().IsMatch(lower) ||
                                        lower.Contains("xuất excel") ||
                                        lower.Contains("đưa vào sprint") ||
                                        lower.Contains("đẩy vào backlog") ||
                                        lower.Contains("sửa tiêu đề") ||
                                        lower.Contains("đổi tiêu đề") ||
                                        lower.Contains("sửa mô tả") ||
                                        lower.Contains("đổi mô tả") ||
                                        lower.Contains("xóa task") ||
                                        lower.Contains("xoá task") ||
                                        lower.Contains("xóa việc") ||
                                        lower.Contains("xóa công việc") ||
                                        lower.Contains("gỡ task") ||
                                        lower.Contains("gỡ việc") ||
                                        lower.Contains("gỡ các việc") ||
                                        lower.Contains("gỡ công việc") ||
                                        lower.Contains("giao cho") ||
                                        lower.Contains("gán cho") ||
                                        lower.Contains("ai đang làm") ||
                                        lower.Contains("ai phụ trách") ||
                                        lower.Contains("trong sprint") ||
                                        lower.Contains("trong backlog");

        // Ngay cả khi nhắc "task" hay IssueKey (VD: "viết code cho task đầu tiên", "code cho QLDA-18"),
        // nếu người dùng trực tiếp yêu cầu AI viết/sinh/sửa mã nguồn -> vẫn NGOÀI PHẠM VI.
        bool directAskForSourceCode =
            StartsWithCodeCommandRegex().IsMatch(lower) ||
            DirectCodeArtifactGenerationRegex().IsMatch(lower) ||
            DirectCodeHelpRegex().IsMatch(lower) ||
            lower.Contains("code cho tôi") ||
            lower.Contains("code giúp tôi") ||
            lower.Contains("code giúp mình") ||
            lower.Contains("code hộ") ||
            lower.Contains("viết code cho") ||
            lower.Contains("sinh code cho") ||
            lower.Contains("triển khai code cho");

        if (directAskForSourceCode)
        {
            // Chỉ cho phép khi nhắc đến việc tạo/sửa đầu việc Scrum mà KHÔNG bảo AI viết mã nguồn (VD: "tạo task viết unit test cho AuthService")
            bool explicitScrumTaskEnvelope = ExplicitPlanningActionRegex().IsMatch(lower) &&
                                             (lower.Contains("task") || lower.Contains("subtask") || lower.Contains("công việc") ||
                                              lower.Contains("việc con") || lower.Contains("sprint") || lower.Contains("backlog"));
            bool pureTaskLabeling = (hasScrumManagementAction || explicitScrumTaskEnvelope) &&
                                    !StartsWithCodeCommandRegex().IsMatch(lower) &&
                                    !lower.Contains("code cho tôi") &&
                                    !lower.Contains("code giúp") &&
                                    !lower.Contains("code hộ") &&
                                    !lower.Contains("viết code") &&
                                    !lower.Contains("sinh code") &&
                                    !lower.Contains("sửa lỗi code") &&
                                    !lower.Contains("fix code") &&
                                    !lower.Contains("debug code") &&
                                    !lower.Contains("giải thích đoạn code") &&
                                    !lower.Contains("giúp tôi") &&
                                    !lower.Contains("hộ tôi");
            if (!pureTaskLabeling)
            {
                return true;
            }
        }

        if (rawText != null && rawText.Contains("```", StringComparison.Ordinal) &&
            (lower.Contains("sửa") || lower.Contains("fix") || lower.Contains("debug") || lower.Contains("giải thích") || lower.Contains("tối ưu")))
        {
            return true;
        }

        if (LanguageSpecificCodeTaskRegex().IsMatch(lower) && !hasScrumManagementAction)
        {
            return true;
        }

        return false;
    }

    private static bool IsExplicitOffTopicOrTrap(string lower, bool hasAttachedDoc)
    {
        if (string.IsNullOrWhiteSpace(lower)) return false;

        bool hasSoftwarePlatformArtifact = lower.Contains("phần mềm") ||
                                           lower.Contains("ứng dụng") ||
                                           lower.Contains("website") ||
                                           lower.Contains("hệ thống") ||
                                           lower.Contains("phân hệ") ||
                                           lower.Contains("module") ||
                                           lower.Contains("user story") ||
                                           lower.Contains("spec hub");

        bool hasScrumSoftwarePlanningFrame =
            !lower.Contains("chạy bộ") &&
            !lower.Contains("đi chợ") &&
            !lower.Contains("nấu") &&
            !lower.Contains("giảm cân") &&
            !lower.Contains("quần áo") &&
            !lower.Contains("thời trang") &&
            !lower.Contains("bài thơ") &&
            !lower.Contains("truyện") &&
            !lower.Contains("kể chuyện") &&
            (!lower.Contains("du lịch") || hasSoftwarePlatformArtifact) &&
            (ExplicitPlanningActionRegex().IsMatch(lower) ||
             hasSoftwarePlatformArtifact ||
             lower.Contains("epic")) &&
            (HasSoftwareEngineeringContext(lower) ||
             lower.Contains("hệ thống") ||
             lower.Contains("sprint") ||
             lower.Contains("tài liệu"));

        // Kiểm tra các từ khóa ngoài phạm vi tuyệt đối (thơ, thời tiết, nấu ăn, toán học, tài chính, y tế...)
        foreach (var kw in OffTopicLifeAndAcademicKeywords)
        {
            if (lower.Contains(kw, StringComparison.Ordinal))
            {
                // Chỉ ngoại lệ nếu là lập kế hoạch phần mềm thực thụ cho domain đó (VD: "Phân rã hệ thống đặt vé máy bay vào Sprint")
                bool neverAllowedEvenWithPm =
                    kw is "làm thơ" or "viết thơ" or "bài thơ" or "sáng tác thơ" or "thơ tình"
                        or "lời bài hát" or "sáng tác nhạc" or "kể chuyện cười" or "truyện cổ tích"
                        or "tử vi" or "bói toán" or "cung hoàng đạo" or "xổ số" or "lô đề" or "vietlott"
                        or "công thức nấu" or "nấu phở" or "nấu ăn" or "nấu cơm" or "thời tiết" or "dự báo thời tiết"
                        or "tư vấn tình cảm" or "thất tình" or "giải phương trình" or "đạo hàm" or "tích phân"
                        or "bạn ăn cơm chưa" or "bạn bao nhiêu tuổi" or "ai sinh ra bạn" or "giả vờ bạn là"
                        or "đổi mật khẩu" or "tạo tài khoản user mới" or "xóa tài khoản" or "xóa sprint khỏi"
                        or "xóa vĩnh viễn dự án" or "xóa sạch database" or "chơi nối chữ";

                if (kw == "hack " && (lower.Contains("chống hack") || lower.Contains("phòng chống hack")))
                {
                    continue;
                }

                if (neverAllowedEvenWithPm || !hasScrumSoftwarePlanningFrame)
                {
                    return true;
                }
            }
        }

        // Bẫy từ đồng âm: dùng "kế hoạch / thiết kế / nhóm / test / lỗi / story / sprint / dự án" trong ngữ cảnh đời thường
        foreach (var trap in PolysemyLifeContextTraps)
        {
            if (lower.Contains(trap, StringComparison.Ordinal) && !hasScrumSoftwarePlanningFrame && !hasAttachedDoc)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPureOffTopicDocument(string docText)
    {
        if (string.IsNullOrWhiteSpace(docText)) return false;
        var lower = docText.ToLowerInvariant();
        if (HasStrictPmDomain(lower, docText) || HasSoftwareEngineeringContext(lower))
        {
            return false;
        }

        return IsDirectCodingOrTechExecutionRequest(lower, docText) ||
               IsExplicitOffTopicOrTrap(lower, hasAttachedDoc: false);
    }

    private static bool HasStrictPmDomain(string lower, string rawText)
    {
        if (string.IsNullOrWhiteSpace(lower)) return false;
        if (AiIndexedMemoryEngine.MentionsSpecHub(lower)) return true;
        if (StrictScrumTokenRegex().IsMatch(lower)) return true;
        if (IssueKeyTokenRegex().IsMatch(rawText)) return true;
        if (DraftTaskIdRegex().IsMatch(lower)) return true;
        if (MemoryRefRegex().IsMatch(lower)) return true;

        if (BugOrStoryTokenRegex().IsMatch(lower) &&
            !lower.Contains("instagram") &&
            !lower.Contains("facebook") &&
            !lower.Contains("bọ"))
        {
            return true;
        }

        foreach (var kw in StrictProjectManagementKeywords)
        {
            if (lower.Contains(kw, StringComparison.Ordinal))
            {
                return true;
            }
        }

        bool mentionsTechRole = lower.Contains("backend") || lower.Contains("frontend") ||
                                lower.Contains("fullstack") || lower.Contains("devops") ||
                                lower.Contains("tester") || lower.Contains("scrum master") ||
                                lower.Contains("product owner") || lower.Contains(" ui") ||
                                lower.Contains("ui/ux") || lower.Contains(" qa");
        if (mentionsTechRole &&
            (lower.Contains("giao") || lower.Contains("gán") || lower.Contains("chuyên môn") ||
             lower.Contains("năng lực") || lower.Contains("phân công") || lower.Contains("bảng") ||
             TeamRoleInquiryRegex().IsMatch(lower)))
        {
            return true;
        }

        if (TeamRoleInquiryRegex().IsMatch(lower) &&
            (lower.Contains("rảnh") || lower.Contains("những ai") || lower.Contains("gồm ai") ||
             lower.Contains("gồm những ai")) &&
            !lower.Contains("team building") && !lower.Contains("du lịch") && !lower.Contains("bóng đá"))
        {
            return true;
        }

        return false;
    }

    private static bool HasSoftwareEngineeringContext(string lower)
    {
        foreach (var term in SoftwareEngineeringTerms)
        {
            if (lower.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasGenuineSoftwarePlanningInput(string lower, bool hasStrictPm)
    {
        int softwareHits = 0;
        foreach (var term in SoftwareEngineeringTerms)
        {
            if (lower.Contains(term, StringComparison.Ordinal))
            {
                softwareHits++;
                if (softwareHits >= 2) break;
            }
        }

        bool hasMeetingTranscriptInput =
            (lower.Contains("biên bản họp") || lower.Contains("biên bản cuộc họp") || lower.Contains("nội dung cuộc họp")) &&
            !lower.Contains("đọc giúp") &&
            !lower.Contains("tóm tắt") &&
            !lower.Contains("nội dung chính") &&
            !lower.Contains('?');

        bool hasDeliveryIntentVerb =
            lower.Contains("xây dựng") || lower.Contains("triển khai") || lower.Contains("phát triển") ||
            lower.Contains("cuộc họp") || lower.Contains("họp") || lower.Contains("chốt") ||
            lower.Contains("lên kế hoạch") || lower.Contains("lập kế hoạch") || lower.Contains("phân rã") ||
            lower.Contains("hoàn thiện") || lower.Contains("tích hợp") || lower.Contains("yêu cầu") ||
            lower.Contains("tạo ") || lower.Contains("lập ") || lower.Contains("chuẩn bị") ||
            lower.Contains("đề xuất") || lower.Contains("nâng cấp") || lower.Contains("rà soát") ||
            lower.Contains("tối ưu") || lower.Contains("bóc tách") || lower.Contains("soạn");

        return (hasStrictPm && hasDeliveryIntentVerb) || (softwareHits >= 2 && hasDeliveryIntentVerb) || hasMeetingTranscriptInput;
    }

    private static bool IsPureInquiryQuestion(string lower) =>
        lower.Contains("tại sao") || lower.Contains("vì sao") ||
        lower.Contains("có nên") || lower.Contains("lý do") ||
        lower.Contains("là gì") || lower.Contains("nghĩa là") ||
        lower.Contains("bao nhiêu") || lower.Contains("những gì") ||
        lower.Contains("gồm gì") || lower.Contains("gồm những") ||
        lower.Contains("như thế nào") || lower.Contains("thế nào") ||
        lower.Contains("ra sao") || lower.Contains("khi nào") ||
        lower.Contains("ở đâu") || lower.Contains("ai sẽ") ||
        lower.Contains("ai đang") || lower.Contains("ai làm") ||
        lower.Contains("ai là người") || lower.Contains("ai xử lý") ||
        lower.Contains("ai phụ trách") || lower.Contains("người phụ trách là ai") ||
        lower.Contains("thuộc sprint nào") || lower.Contains("trong sprint nào") ||
        lower.Contains("nội dung của") || lower.Contains("chi tiết của") ||
        (lower.Contains('?') && Regex.IsMatch(lower, @"(?i)\b(?:ai|gì|nào|mấy|bao\s+nhiêu)\b"));

    private static bool HasMemoryRecallReference(string lower) =>
        lower.Contains("#mem-") || lower.Contains("mem-") ||
        lower.Contains("vừa nãy") || lower.Contains("lúc nãy") ||
        lower.Contains("hồi nãy") || lower.Contains("vừa rồi") ||
        lower.Contains("nhắc lại") || lower.Contains("trí nhớ") ||
        lower.Contains("trên kia") || lower.Contains("đã bàn") ||
        lower.Contains("trước đó");

    private static bool HasAnchoredFollowUpReference(string lower) =>
        lower.Contains("ý đầu tiên") || lower.Contains("ý thứ hai") || lower.Contains("ý thứ ba") ||
        lower.Contains("ý này") || lower.Contains("ý đó") ||
        lower.Contains("mục đầu") || lower.Contains("mục thứ") ||
        lower.Contains("cái đầu tiên") || lower.Contains("cái thứ") ||
        lower.Contains("cái này") || lower.Contains("cái đó") ||
        lower.Contains("việc đầu tiên") || lower.Contains("việc thứ") ||
        lower.Contains("công việc đầu tiên") || lower.Contains("công việc thứ") ||
        lower.Contains("việc này") || lower.Contains("việc đó") ||
        lower.Contains("phần này") || lower.Contains("phần đó") ||
        lower.Contains("phần trên") || lower.Contains("câu trên") ||
        lower.Contains("bảng trên") || lower.Contains("ở trên") || lower.Contains("như trên") ||
        lower.Contains("phương án này") || lower.Contains("phương án đó") ||
        lower.Contains("giải thích rõ hơn") || lower.Contains("chi tiết hơn") ||
        lower.Contains("cụ thể hơn") || lower.Contains("nói ngắn gọn") ||
        lower.Contains("tóm tắt lại") || lower.Contains("còn thiếu") ||
        lower.Contains("ổn chưa") || lower.Contains("hợp lý chưa") ||
        lower.Contains("tại sao vậy") || lower.Contains("vì sao vậy") || lower.Contains("thế còn") ||
        lower.Contains("tại sao chia") || lower.Contains("tại sao gán") ||
        lower.Contains("tại sao lại vậy") || lower.Contains("vì sao lại vậy") ||
        lower.Contains("cho nó") || lower.Contains("của nó") || lower.Contains("với nó");

    private static bool IsNonPmTutorialQuestion(string lower) =>
        lower.Contains("hướng dẫn nấu") || lower.Contains("hướng dẫn cài") ||
        lower.Contains("hướng dẫn làm bánh") || lower.Contains("hướng dẫn tập") ||
        lower.Contains("hướng dẫn kiếm tiền") || lower.Contains("hướng dẫn chơi game") ||
        lower.Contains("hướng dẫn code") || lower.Contains("hướng dẫn lập trình");
}
