namespace ProjectMgmt.Solution.Services.Ai;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;

public sealed record AiExcelExportResolution(
    bool ShouldTriggerExport,
    AiExcelExportSpecDto? ExportSpec,
    string ReplyMessage,
    string TraceSummary);

/// <summary>
/// Bộ phân giải thông minh yêu cầu xuất tệp Excel (.xlsx) theo đúng ngữ cảnh:
/// Sprint đơn lẻ, nhiều Sprint theo trạng thái, chỉ kho Product Backlog, 1 công việc,
/// nội dung chi tiết đầy đủ của 1 công việc, bảng đề xuất AI hoặc lọc đa tiêu chí.
/// Khi thực thể không tồn tại hoặc không có dữ liệu khớp, từ chối xuất khống và phản hồi trung thực.
/// </summary>
public static partial class AiExcelExportResolver
{
    [GeneratedRegex(@"\b([A-Za-z][A-Za-z0-9]{1,15}-\d+)\b", RegexOptions.Compiled)]
    private static partial Regex IssueKeyRegex();

    [GeneratedRegex(@"(?i)\bsprint\s*(?:số\s*|#\s*)?(\d+)\b", RegexOptions.Compiled)]
    private static partial Regex SprintNumberRegex();

    [GeneratedRegex(@"(?i)(?:\b(?:task|công việc|việc)\s*(?:số\s*|thứ\s*|#\s*)?(\d+)\b|\b(?:task|công việc|việc)\s+(đầu tiên|cuối cùng)\b)", RegexOptions.Compiled)]
    private static partial Regex TaskOrdinalRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    public static AiExcelExportResolution Resolve(
        string? userPrompt,
        ProjectExportMetadataResult metadata,
        ProjectActiveConfig activeConfig,
        IProjectScopeContext scope,
        AiSprintProposalDto? existingProposal,
        IReadOnlyList<AiConversationTurnDto>? history,
        IReadOnlyList<AiMemoryAnchorDto>? memories)
    {
        var prompt = (userPrompt ?? string.Empty).Trim();
        var lower = prompt.ToLowerInvariant();
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var safeKey = SanitizeFileToken(string.IsNullOrWhiteSpace(metadata.ProjectKey) ? "ProjectMgmt" : metadata.ProjectKey);

        bool wantsDetail =
            lower.Contains("nội dung") || lower.Contains("chi tiết") || lower.Contains("đầy đủ") ||
            lower.Contains("toàn bộ thông tin") || lower.Contains("thông tin của task") ||
            lower.Contains("mô tả") || lower.Contains("tiêu chí") || lower.Contains("acceptance") ||
            lower.Contains("nghiệm thu") || lower.Contains("bình luận") || lower.Contains("comment");

        bool includeSubtasks = !(lower.Contains("không kèm việc con") ||
                                 lower.Contains("không bao gồm việc con") ||
                                 lower.Contains("không lấy subtask") ||
                                 lower.Contains("chỉ việc cha") ||
                                 lower.Contains("bỏ subtask"));

        // -------------------------------------------------------------------------
        // 1. Trường hợp yêu cầu xuất Bảng Đề Xuất AI chưa lưu trong phiên hội thoại
        // -------------------------------------------------------------------------
        bool asksAiProposal =
            lower.Contains("đề xuất") || lower.Contains("vừa phân rã") || lower.Contains("vừa sinh") ||
            lower.Contains("vừa lập") || lower.Contains("kế hoạch ai") || lower.Contains("bảng tạm") ||
            lower.Contains("chưa lưu") || lower.Contains("chưa áp dụng");

        if (asksAiProposal)
        {
            return ResolveProposalExport(prompt, lower, dateStamp, safeKey, metadata, existingProposal, wantsDetail, includeSubtasks);
        }

        // -------------------------------------------------------------------------
        // 2. Xác định công việc cụ thể (IssueKey tường minh, quy chiếu "task đó", hoặc tên việc)
        // -------------------------------------------------------------------------
        var issueByUpperKey = metadata.Issues
            .GroupBy(i => i.IssueKey.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var explicitKeyMatches = IssueKeyRegex().Matches(prompt)
            .Select(m => m.Groups[1].Value.ToUpperInvariant())
            .Distinct()
            .ToList();

        var missingKeys = explicitKeyMatches.Where(k => !issueByUpperKey.ContainsKey(k)).ToList();
        if (missingKeys.Count > 0)
        {
            var sampleKeys = metadata.Issues
                .Where(i => !i.IsEpic)
                .OrderBy(i => i.IssueNumber)
                .Take(6)
                .Select(i => i.IssueKey)
                .ToList();
            var sampleHint = sampleKeys.Count > 0
                ? $" Một số mã công việc đang có: {string.Join(", ", sampleKeys)}."
                : string.Empty;
            var msg = $"Không tìm thấy công việc mã {string.Join(", ", missingKeys)} trong dự án {metadata.ProjectName} (Tổng cộng hiện có {metadata.Issues.Count(i => !i.IsEpic)} công việc).{sampleHint} Vui lòng kiểm tra lại mã công việc cần xuất.";
            return new AiExcelExportResolution(false, null, msg, $"Không tìm thấy mã {string.Join(", ", missingKeys)}");
        }

        var matchedIssues = explicitKeyMatches.Select(k => issueByUpperKey[k]).ToList();

        // Giải quyết quy chiếu đại từ ("task đó", "công việc đó", "nội dung của nó", "#mem-X")
        if (matchedIssues.Count == 0 && (IsDeicticTaskReference(lower) || (wantsDetail && !lower.Contains("sprint") && !lower.Contains("backlog") && !lower.Contains("dự án"))))
        {
            var resolvedKey = ResolveIssueKeyFromContext(prompt, issueByUpperKey, history, memories);
            if (!string.IsNullOrWhiteSpace(resolvedKey) && issueByUpperKey.TryGetValue(resolvedKey, out var contextIssue))
            {
                matchedIssues.Add(contextIssue);
            }
            else if (existingProposal?.Tasks is { Count: > 0 } && lower.Contains("trong đề xuất"))
            {
                return ResolveProposalExport(prompt, lower, dateStamp, safeKey, metadata, existingProposal, wantsDetail, includeSubtasks);
            }
        }

        // Tìm theo tiêu đề chính xác/đặc trưng khi người dùng gõ "xuất nội dung task Xây dựng Dashboard"
        if (matchedIssues.Count == 0 &&
            (lower.Contains("task ") || lower.Contains("công việc ") || lower.Contains("việc ")) &&
            !lower.Contains("tất cả") && !lower.Contains("toàn bộ") && !lower.Contains("danh sách") &&
            !lower.Contains("các task") && !lower.Contains("nhiều task"))
        {
            var byTitle = TryMatchSingleIssueByTitle(prompt, metadata.Issues);
            if (byTitle != null)
            {
                matchedIssues.Add(byTitle);
            }
        }

        if (matchedIssues.Count == 1)
        {
            var target = matchedIssues[0];
            var subtasks = metadata.Issues.Where(i => i.ParentId == target.Id).ToList();
            var scopeMode = wantsDetail ? "task_detail" : "single_task";
            var count = scopeMode == "task_detail" ? 1 : (includeSubtasks ? 1 + subtasks.Count : 1);
            var singleFileName = scopeMode == "task_detail"
                ? $"{SanitizeFileToken(target.IssueKey)}_Chi_Tiet_{dateStamp}.xlsx"
                : $"{SanitizeFileToken(target.IssueKey)}_{dateStamp}.xlsx";

            var spec = new AiExcelExportSpecDto
            {
                Scope = scopeMode,
                Title = scopeMode == "task_detail"
                    ? $"HỒ SƠ CHI TIẾT CÔNG VIỆC {target.IssueKey}"
                    : $"CÔNG VIỆC {target.IssueKey} - {target.Title}",
                Subtitle = $"Dự án: {metadata.ProjectName} | Trạng thái: {target.StatusName} | Ưu tiên: {target.Priority}",
                SuggestedFileName = singleFileName,
                IssueIds = [target.Id],
                IssueKeys = [target.IssueKey],
                IncludeSubtasks = includeSubtasks,
                IncludeLegend = true,
                MatchedItemCount = count,
                SummaryDescription = scopeMode == "task_detail"
                    ? $"Chi tiết đầy đủ công việc {target.IssueKey} (thuộc tính, mô tả, tiêu chí nghiệm thu, {subtasks.Count} việc con, tài liệu & bình luận)"
                    : $"{target.IssueKey}: {target.Title}{(includeSubtasks && subtasks.Count > 0 ? $" và {subtasks.Count} việc con" : string.Empty)}"
            };

            var singleReply = scopeMode == "task_detail"
                ? $"Tôi đã chuẩn bị tệp Excel hồ sơ chi tiết cho công việc **{target.IssueKey} — {target.Title}** (bao gồm bảng thông tin thuộc tính, mô tả đầy đủ, tiêu chí nghiệm thu, {subtasks.Count} việc con, tài liệu căn cứ/trao đổi và bảng chú thích trạng thái). Bạn có thể tải xuống ngay bên dưới."
                : $"Tôi đã trích xuất riêng công việc **{target.IssueKey} — {target.Title}**{(includeSubtasks && subtasks.Count > 0 ? $" cùng {subtasks.Count} việc con trực thuộc" : string.Empty)} ra bảng Excel chuẩn ProjectMgmt (kèm chú thích trạng thái).";

            return new AiExcelExportResolution(true, spec, singleReply, $"Khớp 1 công việc {target.IssueKey} (chế độ {scopeMode})");
        }

        if (matchedIssues.Count > 1)
        {
            var ids = matchedIssues.Select(i => i.Id).ToList();
            var keys = matchedIssues.Select(i => i.IssueKey).ToList();
            var childCount = includeSubtasks
                ? metadata.Issues.Count(i => i.ParentId.HasValue && ids.Contains(i.ParentId.Value))
                : 0;
            var spec = new AiExcelExportSpecDto
            {
                Scope = "filtered_tasks",
                Title = $"DANH SÁCH CÔNG VIỆC ĐÃ CHỌN ({string.Join(", ", keys.Take(4))}{(keys.Count > 4 ? "..." : string.Empty)})",
                Subtitle = $"Dự án: {metadata.ProjectName} | Số công việc được chọn: {keys.Count}",
                SuggestedFileName = $"{safeKey}_Cong_Viec_Chon_{dateStamp}.xlsx",
                IssueIds = ids,
                IssueKeys = keys,
                IncludeSubtasks = includeSubtasks,
                IncludeLegend = true,
                MatchedItemCount = matchedIssues.Count + childCount,
                SummaryDescription = $"Xuất {matchedIssues.Count} công việc chỉ định ({string.Join(", ", keys)})"
            };
            var multiReply = $"Tôi đã chuẩn bị tệp Excel chỉ gồm **{matchedIssues.Count} công việc** bạn yêu cầu ({string.Join(", ", keys)}){(childCount > 0 ? $" và {childCount} việc con liên quan" : string.Empty)} dưới dạng bảng có màu trạng thái và chú thích.";
            return new AiExcelExportResolution(true, spec, multiReply, $"Khớp {matchedIssues.Count} mã công việc");
        }

        // -------------------------------------------------------------------------
        // 3. Phân tích phạm vi Sprint / Backlog / Toàn dự án & Bộ lọc thuộc tính
        // -------------------------------------------------------------------------
        bool explicitlyWantsAllProject =
            lower.Contains("toàn bộ dự án") ||
            lower.Contains("tất cả dự án") ||
            lower.Contains("cả dự án") ||
            ((lower.Contains("sprint") && lower.Contains("backlog")) &&
             (lower.Contains("tất cả") || lower.Contains("toàn bộ") || lower.Contains("và") || lower.Contains("cả hai")));

        var matchedSprints = new List<ExportSprintMetadata>();
        bool askedSprintScope = false;

        if (!explicitlyWantsAllProject)
        {
            var sprintNums = SprintNumberRegex().Matches(prompt)
                .Select(m => int.TryParse(m.Groups[1].Value, out var n) ? n : -1)
                .Where(n => n > 0)
                .Distinct()
                .ToList();

            if (sprintNums.Count > 0)
            {
                askedSprintScope = true;
                var missingSprintNums = new List<int>();
                foreach (var num in sprintNums)
                {
                    var found = MatchSprintByNumber(metadata.Sprints, num);
                    if (found != null)
                    {
                        if (matchedSprints.All(s => s.Id != found.Id))
                        {
                            matchedSprints.Add(found);
                        }
                    }
                    else
                    {
                        missingSprintNums.Add(num);
                    }
                }

                if (missingSprintNums.Count > 0)
                {
                    var sprintInventory = BuildSprintInventoryText(metadata.Sprints);
                    var msg = $"Không tìm thấy {string.Join(", ", missingSprintNums.Select(n => $"Sprint {n}"))} trong dự án {metadata.ProjectName}. {sprintInventory} Bạn vui lòng chọn đúng Sprint cần xuất.";
                    return new AiExcelExportResolution(false, null, msg, $"Không tìm thấy Sprint {string.Join(", ", missingSprintNums)}");
                }
            }
            else if (lower.Contains("sprint đang chạy") || lower.Contains("sprint hiện tại") ||
                     lower.Contains("active sprint") || lower.Contains("sprint đang mở") ||
                     lower.Contains("sprint này"))
            {
                askedSprintScope = true;
                matchedSprints = metadata.Sprints.Where(s => s.Status == "active").ToList();
                if (matchedSprints.Count == 0)
                {
                    var msg = $"Dự án {metadata.ProjectName} hiện chưa có Sprint nào ở trạng thái Đang chạy (Active). {BuildSprintInventoryText(metadata.Sprints)}";
                    return new AiExcelExportResolution(false, null, msg, "Không có Sprint Active");
                }
            }
            else if (lower.Contains("sprint đã hoàn thành") || lower.Contains("sprint hoàn thành") ||
                     lower.Contains("completed sprint") || lower.Contains("sprint đã đóng") ||
                     lower.Contains("sprint đã xong") || lower.Contains("các sprint trước"))
            {
                askedSprintScope = true;
                matchedSprints = metadata.Sprints.Where(s => s.Status == "completed").ToList();
                if (matchedSprints.Count == 0)
                {
                    var msg = $"Dự án {metadata.ProjectName} hiện chưa có Sprint nào Đã hoàn thành (Completed). {BuildSprintInventoryText(metadata.Sprints)}";
                    return new AiExcelExportResolution(false, null, msg, "Không có Sprint Completed");
                }
            }
            else if (lower.Contains("sprint sắp tới") || lower.Contains("sprint tương lai") ||
                     lower.Contains("future sprint") || lower.Contains("sprint kế tiếp") ||
                     lower.Contains("sprint chưa chạy"))
            {
                askedSprintScope = true;
                matchedSprints = metadata.Sprints.Where(s => s.Status == "future").ToList();
                if (matchedSprints.Count == 0)
                {
                    var msg = $"Dự án {metadata.ProjectName} hiện chưa có Sprint nào ở trạng thái Sắp tới (Future). {BuildSprintInventoryText(metadata.Sprints)}";
                    return new AiExcelExportResolution(false, null, msg, "Không có Sprint Future");
                }
            }
            else if (lower.Contains("sprint đó") || lower.Contains("sprint vừa nhắc") || lower.Contains("sprint vừa rồi") || lower.Contains("sprint trên"))
            {
                askedSprintScope = true;
                var fromCtx = ResolveSprintFromContext(metadata.Sprints, history, memories);
                if (fromCtx != null)
                {
                    matchedSprints.Add(fromCtx);
                }
                else
                {
                    var msg = $"Tôi chưa xác định được Sprint cụ thể từ ngữ cảnh trước đó. {BuildSprintInventoryText(metadata.Sprints)} Hãy nêu rõ tên hoặc số Sprint cần xuất (ví dụ: \"Xuất Sprint 3 ra Excel\").";
                    return new AiExcelExportResolution(false, null, msg, "Chưa rõ Sprint mục tiêu");
                }
            }
            else if (lower.Contains("tất cả sprint") || lower.Contains("toàn bộ sprint") || lower.Contains("các sprint"))
            {
                askedSprintScope = true;
                matchedSprints = metadata.Sprints.ToList();
                if (matchedSprints.Count == 0)
                {
                    return new AiExcelExportResolution(false, null, $"Dự án {metadata.ProjectName} hiện chưa khởi tạo Sprint nào.", "Dự án chưa có Sprint");
                }
            }
            else if (lower.Contains("sprint"))
            {
                // Thử khớp tên Sprint cụ thể (không chứa chữ "Sprint <số>")
                foreach (var sp in metadata.Sprints)
                {
                    if (!string.IsNullOrWhiteSpace(sp.Name) &&
                        lower.Contains(sp.Name.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                    {
                        matchedSprints.Add(sp);
                        askedSprintScope = true;
                    }
                }
            }
        }

        bool isBacklogOnly = !explicitlyWantsAllProject &&
                             !askedSprintScope &&
                             (lower.Contains("backlog") || lower.Contains("hàng chờ"));

        // -------------------------------------------------------------------------
        // 4. Phân tích bộ lọc thuộc tính (Người xử lý, Trạng thái, Ưu tiên, Loại, Epic, Bị chặn)
        // -------------------------------------------------------------------------
        var cleanedForAttr = RemoveSprintStatePhrases(lower);
        var assigneeFilters = ResolveAssigneeFilters(prompt, cleanedForAttr, metadata.Members, scope.UserId, out var unknownAssigneeHint);
        if (!string.IsNullOrWhiteSpace(unknownAssigneeHint))
        {
            var memberList = metadata.Members.Count > 0
                ? string.Join(", ", metadata.Members.Select(m => m.DisplayName))
                : "Chưa có thành viên";
            var msg = $"Không tìm thấy thành viên \"{unknownAssigneeHint}\" trong dự án {metadata.ProjectName}. Thành viên hiện có: {memberList}.";
            return new AiExcelExportResolution(false, null, msg, $"Không tìm thấy thành viên {unknownAssigneeHint}");
        }

        var statusFilters = ResolveStatusFilters(cleanedForAttr, activeConfig);
        var priorityFilters = ResolvePriorityFilters(cleanedForAttr, activeConfig);
        var issueTypeFilters = ResolveIssueTypeFilters(cleanedForAttr, activeConfig);
        var epicFilters = ResolveEpicFilters(cleanedForAttr, metadata.Epics);
        bool blockedOnly = cleanedForAttr.Contains("bị chặn") || cleanedForAttr.Contains("blocked") || cleanedForAttr.Contains("đang block");

        bool hasAttributeFilters =
            assigneeFilters.Count > 0 || statusFilters.Count > 0 || priorityFilters.Count > 0 ||
            issueTypeFilters.Count > 0 || epicFilters.Count > 0 || blockedOnly;

        // Nếu chỉ viết chung chung "xuất nội dung 1 task" mà không nói mã nào
        if (!explicitlyWantsAllProject && !askedSprintScope && !isBacklogOnly && !hasAttributeFilters &&
            (lower.Contains("1 task") || lower.Contains("một task") || lower.Contains("duy nhất task") ||
             (wantsDetail && (lower.Contains("của task") || lower.Contains("của việc") || lower.Contains("1 công việc")))))
        {
            var sampleKeys = metadata.Issues.Where(i => !i.IsEpic).OrderBy(i => i.IssueNumber).Take(6).Select(i => i.IssueKey).ToList();
            var hint = sampleKeys.Count > 0 ? $" Ví dụ: \"Xuất chi tiết task {sampleKeys[0]}\"." : string.Empty;
            return new AiExcelExportResolution(
                false,
                null,
                $"Bạn muốn xuất công việc nào? Hãy cung cấp mã hoặc tên công việc cụ thể để tôi xuất chính xác phần đó.{hint}",
                "Thiếu mã công việc cần xuất");
        }

        // Xác định Scope cuối cùng
        string scopeName;
        if (matchedSprints.Count == 1 && !hasAttributeFilters)
        {
            scopeName = "single_sprint";
        }
        else if (matchedSprints.Count == 1 && hasAttributeFilters)
        {
            scopeName = "single_sprint";
        }
        else if (matchedSprints.Count > 1)
        {
            scopeName = "sprint_list";
        }
        else if (isBacklogOnly)
        {
            scopeName = "backlog_only";
        }
        else if (hasAttributeFilters)
        {
            scopeName = "filtered_tasks";
        }
        else
        {
            scopeName = "project_all";
        }

        // -------------------------------------------------------------------------
        // 5. Đếm & kiểm tra dữ liệu khớp thực tế trước khi cho phép tải tệp
        // -------------------------------------------------------------------------
        var matchedData = EvaluateMatchingIssues(
            metadata.Issues,
            scopeName,
            matchedSprints,
            assigneeFilters,
            statusFilters,
            priorityFilters,
            issueTypeFilters,
            epicFilters,
            blockedOnly,
            includeSubtasks);

        // Chỉ cho phép project_all đi qua khi dự án chưa có công việc (để vẫn xuất file khung/báo cáo tổng quan khi user yêu cầu tổng thể),
        // còn các phạm vi thu hẹp (Sprint cụ thể, Backlog, Bộ lọc...) nếu 0 công việc khớp thì báo rõ để không xuất file rỗng sai ngữ cảnh.
        if (matchedData.Count == 0 && scopeName != "project_all")
        {
            var emptyReason = scopeName switch
            {
                "single_sprint" => $"Sprint \"{matchedSprints[0].Name}\" hiện có **0 công việc** khớp điều kiện nên tôi không tạo tệp Excel trống.",
                "sprint_list" => $"Các Sprint đã chọn ({string.Join(", ", matchedSprints.Select(s => s.Name))}) hiện không có công việc nào khớp điều kiện.",
                "backlog_only" => "Kho Product Backlog hiện đang trống (0 công việc khớp điều kiện) nên chưa có dữ liệu để xuất Excel.",
                "filtered_tasks" => "Không có công việc nào trong dự án khớp bộ lọc bạn yêu cầu để xuất Excel.",
                _ => $"Dự án {metadata.ProjectName} hiện chưa có công việc nào được tạo trong Sprint hoặc kho Backlog để xuất báo cáo."
            };
            if (existingProposal?.Tasks is { Count: > 0 } && scopeName == "project_all")
            {
                emptyReason += $" Gợi ý: Bạn đang có {existingProposal.Tasks.Count} công việc ở bảng đề xuất AI — hãy gõ \"Xuất bảng đề xuất vừa tạo ra Excel\" nếu muốn xuất bảng tạm đó.";
            }

            return new AiExcelExportResolution(false, null, emptyReason, "0 công việc khớp phạm vi xuất");
        }

        // Xây dựng Spec & tên file chuẩn ProjectMgmt
        var filterParts = BuildFilterSummaryParts(
            matchedSprints,
            isBacklogOnly,
            assigneeFilters,
            statusFilters,
            priorityFilters,
            issueTypeFilters,
            epicFilters,
            blockedOnly);

        var (title, fileName, reply) = BuildSuccessMetadata(
            scopeName,
            metadata,
            matchedSprints,
            matchedData.Count,
            filterParts,
            safeKey,
            dateStamp,
            existingProposal);

        var exportSpec = new AiExcelExportSpecDto
        {
            Scope = scopeName,
            Title = title,
            Subtitle = filterParts.Count > 0
                ? $"Dự án: {metadata.ProjectName} | Phạm vi: {string.Join(" · ", filterParts)}"
                : $"Dự án: {metadata.ProjectName} | Toàn bộ Sprint & Backlog",
            SuggestedFileName = fileName,
            SprintIds = matchedSprints.Select(s => s.Id).ToList(),
            SprintNames = matchedSprints.Select(s => s.Name).ToList(),
            AssigneeNames = assigneeFilters,
            StatusNames = statusFilters,
            Priorities = priorityFilters,
            IssueTypes = issueTypeFilters,
            EpicNames = epicFilters,
            IncludeSubtasks = includeSubtasks,
            IncludeLegend = true,
            BlockedOnly = blockedOnly ? true : null,
            MatchedItemCount = matchedData.Count,
            SummaryDescription = filterParts.Count > 0 ? string.Join(", ", filterParts) : "Toàn bộ dự án"
        };

        return new AiExcelExportResolution(true, exportSpec, reply, $"Phạm vi={scopeName}, Khớp={matchedData.Count} mục");
    }

    private static AiExcelExportResolution ResolveProposalExport(
        string prompt,
        string lower,
        string dateStamp,
        string safeKey,
        ProjectExportMetadataResult metadata,
        AiSprintProposalDto? existingProposal,
        bool wantsDetail,
        bool includeSubtasks)
    {
        if (existingProposal == null || existingProposal.Tasks.Count == 0)
        {
            return new AiExcelExportResolution(
                false,
                null,
                "Phiên hội thoại hiện tại chưa có bảng đề xuất AI nào để xuất Excel. Bạn hãy yêu cầu lập kế hoạch trước, hoặc yêu cầu xuất dữ liệu thật đang có trong dự án (ví dụ: \"Xuất Sprint 3\" hoặc \"Xuất kho Backlog\").",
                "Không có bảng đề xuất AI trong phiên");
        }

        // Kiểm tra xem người dùng có chỉ định xuất 1 task cụ thể trong bảng đề xuất không (vd: "task đầu tiên", "task số 2")
        var ordMatch = TaskOrdinalRegex().Match(prompt);
        ProposedTaskDto? selectedProposalTask = null;
        if (ordMatch.Success)
        {
            int targetIdx = -1;
            if (ordMatch.Groups[1].Success && int.TryParse(ordMatch.Groups[1].Value, out var oneBased))
            {
                targetIdx = oneBased - 1;
            }
            else if (ordMatch.Groups[2].Value.Equals("đầu tiên", StringComparison.OrdinalIgnoreCase))
            {
                targetIdx = 0;
            }
            else if (ordMatch.Groups[2].Value.Equals("cuối cùng", StringComparison.OrdinalIgnoreCase))
            {
                targetIdx = existingProposal.Tasks.Count - 1;
            }

            if (targetIdx < 0 || targetIdx >= existingProposal.Tasks.Count)
            {
                return new AiExcelExportResolution(
                    false,
                    null,
                    $"Bảng đề xuất hiện tại chỉ có {existingProposal.Tasks.Count} công việc chính (từ #1 đến #{existingProposal.Tasks.Count}). Vui lòng chọn đúng thứ tự công việc.",
                    "Thứ tự task đề xuất vượt phạm vi");
            }

            selectedProposalTask = existingProposal.Tasks[targetIdx];
        }

        if (selectedProposalTask != null)
        {
            var subCount = selectedProposalTask.Subtasks?.Count ?? 0;
            var specSingle = new AiExcelExportSpecDto
            {
                Scope = wantsDetail ? "task_detail" : "ai_proposal",
                Title = wantsDetail
                    ? $"CHI TIẾT CÔNG VIỆC ĐỀ XUẤT - {selectedProposalTask.Title}"
                    : $"ĐỀ XUẤT CÔNG VIỆC - {selectedProposalTask.Title}",
                Subtitle = $"Dự án: {metadata.ProjectName} | Nguồn: Bảng đề xuất AI",
                SuggestedFileName = $"{safeKey}_DeXuat_{SanitizeFileToken(selectedProposalTask.Id)}_{dateStamp}.xlsx",
                IssueKeys = [selectedProposalTask.Id],
                SearchKeyword = "ai_proposal",
                IncludeSubtasks = includeSubtasks,
                IncludeLegend = true,
                MatchedItemCount = wantsDetail ? 1 : (1 + (includeSubtasks ? subCount : 0)),
                SummaryDescription = $"Đề xuất AI: {selectedProposalTask.Title}"
            };

            var replySingle = wantsDetail
                ? $"Tôi đã chuẩn bị tệp Excel hồ sơ chi tiết cho công việc đề xuất **{selectedProposalTask.Title}** (gồm mô tả, tiêu chí nghiệm thu, {subCount} việc con và trích dẫn đặc tả)."
                : $"Tôi đã xuất riêng hạng mục đề xuất **{selectedProposalTask.Title}**{(includeSubtasks && subCount > 0 ? $" và {subCount} việc con" : string.Empty)} ra bảng Excel.";

            return new AiExcelExportResolution(true, specSingle, replySingle, $"Xuất đề xuất AI mục {selectedProposalTask.Id}");
        }

        var totalSubs = existingProposal.Tasks.Sum(t => t.Subtasks?.Count ?? 0);
        var spec = new AiExcelExportSpecDto
        {
            Scope = "ai_proposal",
            Title = $"BẢNG ĐỀ XUẤT KẾ HOẠCH AI - {metadata.ProjectName}",
            Subtitle = string.IsNullOrWhiteSpace(existingProposal.SprintGoal)
                ? $"Dự án: {metadata.ProjectName} | {existingProposal.Tasks.Count} công việc · {existingProposal.TotalStoryPoints} SP"
                : $"Mục tiêu: {existingProposal.SprintGoal} | {existingProposal.Tasks.Count} công việc · {existingProposal.TotalStoryPoints} SP",
            SuggestedFileName = $"{safeKey}_De_Xuat_AI_{dateStamp}.xlsx",
            IncludeSubtasks = includeSubtasks,
            IncludeLegend = true,
            MatchedItemCount = existingProposal.Tasks.Count + (includeSubtasks ? totalSubs : 0),
            SummaryDescription = $"Bảng đề xuất AI ({existingProposal.Tasks.Count} việc chính, {totalSubs} việc con)"
        };

        var reply = $"Tôi đã kích hoạt xuất bảng Excel chứa toàn bộ **{existingProposal.Tasks.Count} công việc chính** và **{totalSubs} việc con** ({existingProposal.TotalStoryPoints} SP) từ **Bảng đề xuất AI** hiện tại (dạng bảng sạch chuẩn ProjectMgmt kèm màu trạng thái và chú thích).";
        return new AiExcelExportResolution(true, spec, reply, $"Xuất bảng đề xuất AI ({existingProposal.Tasks.Count} task)");
    }

    private static ExportSprintMetadata? MatchSprintByNumber(IReadOnlyList<ExportSprintMetadata> sprints, int num)
    {
        var explicitRegex = new Regex($@"(?i)\bsprint\s*(?:số\s*|#\s*)?{num}(?![0-9])");
        var exact = sprints.FirstOrDefault(s => explicitRegex.IsMatch(s.Name ?? string.Empty));
        if (exact != null) return exact;

        var tokenRegex = new Regex($@"(?<![A-Za-z0-9]){num}(?![0-9])");
        return sprints.FirstOrDefault(s => tokenRegex.IsMatch(s.Name ?? string.Empty));
    }

    private static string BuildSprintInventoryText(IReadOnlyList<ExportSprintMetadata> sprints)
    {
        if (sprints.Count == 0)
        {
            return "Dự án hiện chưa có Sprint nào được khởi tạo.";
        }

        var formatted = sprints.Select(s =>
        {
            var statusVi = s.Status switch
            {
                "active" => "Đang chạy",
                "completed" => "Đã hoàn thành",
                _ => "Sắp tới"
            };
            return $"\"{s.Name}\" ({statusVi}, {s.ParentItemCount} việc)";
        });
        return $"Các Sprint hiện có: {string.Join("; ", formatted)}.";
    }

    private static bool IsDeicticTaskReference(string lower) =>
        lower.Contains("task đó") || lower.Contains("task này") ||
        lower.Contains("việc đó") || lower.Contains("việc này") ||
        lower.Contains("công việc đó") || lower.Contains("công việc này") ||
        lower.Contains("task vừa rồi") || lower.Contains("task vừa nhắc") ||
        lower.Contains("công việc trên") || lower.Contains("nội dung của nó");

    private static string? ResolveIssueKeyFromContext(
        string prompt,
        IReadOnlyDictionary<string, ExportIssueMetadata> issueByUpperKey,
        IReadOnlyList<AiConversationTurnDto>? history,
        IReadOnlyList<AiMemoryAnchorDto>? memories)
    {
        if (memories != null)
        {
            var memMatch = Regex.Match(prompt, @"#mem-(\d+)", RegexOptions.IgnoreCase);
            if (memMatch.Success)
            {
                var anchorKey = $"#mem-{memMatch.Groups[1].Value}";
                var anchor = memories.FirstOrDefault(m => m.AnchorKey.Equals(anchorKey, StringComparison.OrdinalIgnoreCase));
                if (anchor != null)
                {
                    foreach (Match m in IssueKeyRegex().Matches($"{anchor.Title} {anchor.DetailContent}"))
                    {
                        var candidate = m.Groups[1].Value.ToUpperInvariant();
                        if (issueByUpperKey.ContainsKey(candidate)) return candidate;
                    }
                }
            }
        }

        if (history != null)
        {
            foreach (var turn in history.TakeLast(8).Reverse())
            {
                var text = $"{turn.Content}\n{turn.FullContent}";
                var matches = IssueKeyRegex().Matches(text);
                for (int i = matches.Count - 1; i >= 0; i--)
                {
                    var candidate = matches[i].Groups[1].Value.ToUpperInvariant();
                    if (issueByUpperKey.ContainsKey(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static ExportSprintMetadata? ResolveSprintFromContext(
        IReadOnlyList<ExportSprintMetadata> sprints,
        IReadOnlyList<AiConversationTurnDto>? history,
        IReadOnlyList<AiMemoryAnchorDto>? memories)
    {
        var texts = new List<string>();
        if (history != null)
        {
            texts.AddRange(history.TakeLast(6).Reverse().Select(t => t.Content ?? string.Empty));
        }
        if (memories != null)
        {
            texts.AddRange(memories.OrderByDescending(m => m.TurnIndex).Take(4).Select(m => $"{m.Title} {m.Summary}"));
        }

        foreach (var text in texts)
        {
            var m = SprintNumberRegex().Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var num))
            {
                var found = MatchSprintByNumber(sprints, num);
                if (found != null) return found;
            }
            var byName = sprints.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Name) && text.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;
        }

        return null;
    }

    private static ExportIssueMetadata? TryMatchSingleIssueByTitle(string prompt, IReadOnlyList<ExportIssueMetadata> issues)
    {
        var candidate = Regex.Replace(
            prompt,
            @"(?i)\b(xuất|tải|export|cho tôi|giúp tôi|làm ơn|nội dung|chi tiết|thông tin|đầy đủ|chỉ có|duy nhất|mỗi|của|task|công việc|việc|ra|file|tệp|excel|xlsx|bảng tính|thôi|nhé|ạ)\b",
            " ");
        candidate = Regex.Replace(candidate, @"[""'`:.,?!()\[\]]", " ");
        candidate = MultiSpaceRegex().Replace(candidate, " ").Trim().ToLowerInvariant();
        if (candidate.Length < 4) return null;

        var exact = issues.FirstOrDefault(i => i.Title.Trim().Equals(candidate, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var partial = issues.Where(i => i.Title.Contains(candidate, StringComparison.OrdinalIgnoreCase)).ToList();
        return partial.Count == 1 ? partial[0] : null;
    }

    private static string RemoveSprintStatePhrases(string lower)
    {
        return lower
            .Replace("sprint đã hoàn thành", " ", StringComparison.Ordinal)
            .Replace("sprint hoàn thành", " ", StringComparison.Ordinal)
            .Replace("completed sprint", " ", StringComparison.Ordinal)
            .Replace("sprint đang chạy", " ", StringComparison.Ordinal)
            .Replace("active sprint", " ", StringComparison.Ordinal);
    }

    private static List<string> ResolveAssigneeFilters(
        string prompt,
        string cleanedLower,
        IReadOnlyList<ExportMemberMetadata> members,
        Guid actorUserId,
        out string? unknownAssigneeHint)
    {
        unknownAssigneeHint = null;
        var result = new List<string>();

        if (cleanedLower.Contains("chưa gán") || cleanedLower.Contains("chưa phân công") || cleanedLower.Contains("unassigned"))
        {
            result.Add("__UNASSIGNED__");
        }

        if (cleanedLower.Contains("của tôi") || cleanedLower.Contains("cho tôi làm") || cleanedLower.Contains("việc tôi") || cleanedLower.Contains("của mình"))
        {
            var me = members.FirstOrDefault(m => m.UserId == actorUserId);
            if (me != null && !result.Contains(me.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(me.DisplayName);
            }
        }

        // Khớp tên thành viên thật (ưu tiên tên dài trước để tránh trùng cục bộ)
        foreach (var member in members.OrderByDescending(m => m.DisplayName.Length))
        {
            var nameLower = member.DisplayName.Trim().ToLowerInvariant();
            if (nameLower.Length >= 2 && ContainsWholePhrase(cleanedLower, nameLower))
            {
                if (!result.Contains(member.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(member.DisplayName);
                }
            }
        }

        // Khớp tên viết tắt cuối (nếu chỉ có duy nhất 1 người trùng phần tên cuối & có ngữ cảnh "của <tên>" / "giao cho <tên>")
        if (result.Count == 0 &&
            (cleanedLower.Contains("của ") || cleanedLower.Contains("giao cho ") || cleanedLower.Contains("cho bạn ")))
        {
            foreach (var member in members)
            {
                var parts = member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var lastWord = parts.Length > 0 ? parts[^1].ToLowerInvariant() : string.Empty;
                if (lastWord.Length >= 3 && Regex.IsMatch(cleanedLower, $@"(?i)\b(?:của|cho)\s+{Regex.Escape(lastWord)}\b"))
                {
                    var matchingMembers = members.Where(m => m.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Equals(parts[^1], StringComparison.OrdinalIgnoreCase) == true).ToList();
                    if (matchingMembers.Count == 1)
                    {
                        result.Add(member.DisplayName);
                    }
                }
            }
        }

        // Phát hiện trường hợp user yêu cầu rõ "nhân viên X / thành viên X / người xử lý X" mà không tồn tại
        if (result.Count == 0)
        {
            var unknownMatch = Regex.Match(prompt, @"(?i)(?:nhân viên|thành viên|người xử lý|dev)\s+([A-ZÀ-Ỹa-zà-ỹ0-9_ ]{2,30})(?=\s+(?:trong|ở|ra|sang|thành|với|$))");
            if (unknownMatch.Success)
            {
                var candidate = unknownMatch.Groups[1].Value.Trim();
                if (!candidate.Equals("nào", StringComparison.OrdinalIgnoreCase) &&
                    !candidate.Equals("dự án", StringComparison.OrdinalIgnoreCase))
                {
                    unknownAssigneeHint = candidate;
                }
            }
        }

        return result;
    }

    private static List<string> ResolveStatusFilters(string cleanedLower, ProjectActiveConfig activeConfig)
    {
        var result = new List<string>();
        foreach (var st in activeConfig.Statuses)
        {
            var sLower = st.Name.Trim().ToLowerInvariant();
            if (sLower.Length >= 2 && ContainsWholePhrase(cleanedLower, sLower))
            {
                result.Add(st.Name);
            }
        }

        if (result.Count == 0)
        {
            if (cleanedLower.Contains("chưa hoàn thành") || cleanedLower.Contains("chưa xong") || cleanedLower.Contains("còn tồn") || cleanedLower.Contains("dở dang"))
            {
                result.AddRange(activeConfig.Statuses.Where(s => !s.IsDone && !s.IsCanceled).Select(s => s.Name));
            }
            else if (cleanedLower.Contains("đang làm") || cleanedLower.Contains("đang thực hiện") || cleanedLower.Contains("in progress"))
            {
                result.AddRange(activeConfig.Statuses.Where(s => s.IsActiveWork || s.Category.Equals("InProgress", StringComparison.OrdinalIgnoreCase)).Select(s => s.Name));
            }
            else if (cleanedLower.Contains("chờ duyệt") || cleanedLower.Contains("chờ kiểm") || cleanedLower.Contains("review"))
            {
                result.AddRange(activeConfig.Statuses.Where(s => s.Name.Contains("Review", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Duyệt", StringComparison.OrdinalIgnoreCase)).Select(s => s.Name));
            }
            else if (cleanedLower.Contains("đã xong") || cleanedLower.Contains("đã hoàn thành"))
            {
                result.AddRange(activeConfig.Statuses.Where(s => s.IsDone || s.Category.Equals("Done", StringComparison.OrdinalIgnoreCase)).Select(s => s.Name));
            }
            else if (cleanedLower.Contains("cần làm") || cleanedLower.Contains("chưa làm") || cleanedLower.Contains("to do"))
            {
                result.AddRange(activeConfig.Statuses.Where(s => s.IsInitial || s.Category.Equals("ToDo", StringComparison.OrdinalIgnoreCase)).Select(s => s.Name));
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ResolvePriorityFilters(string cleanedLower, ProjectActiveConfig activeConfig)
    {
        var result = new List<string>();
        bool hasPrioCue = cleanedLower.Contains("ưu tiên") || cleanedLower.Contains("priority") ||
                          cleanedLower.Contains("mức độ") || cleanedLower.Contains("khẩn cấp") ||
                          Regex.IsMatch(cleanedLower, @"\b(urgent|highest|high|medium|low|blocker|critical|p0|p1|p2|p3)\b");
        if (!hasPrioCue) return result;

        foreach (var p in activeConfig.Priorities)
        {
            if (ContainsWholePhrase(cleanedLower, p.Name.ToLowerInvariant()))
            {
                result.Add(p.Name);
            }
        }

        if (result.Count == 0)
        {
            if (cleanedLower.Contains("khẩn cấp") || cleanedLower.Contains("cao nhất") || Regex.IsMatch(cleanedLower, @"\b(urgent|highest|critical|p0)\b"))
            {
                result.Add(activeConfig.ResolvePriorityName("Urgent"));
            }
            if (cleanedLower.Contains("ưu tiên cao") || Regex.IsMatch(cleanedLower, @"\b(high|p1)\b"))
            {
                result.Add(activeConfig.ResolvePriorityName("High"));
            }
            if (cleanedLower.Contains("trung bình") || Regex.IsMatch(cleanedLower, @"\b(medium|p2)\b"))
            {
                result.Add(activeConfig.ResolvePriorityName("Medium"));
            }
            if (cleanedLower.Contains("ưu tiên thấp") || Regex.IsMatch(cleanedLower, @"\b(low|p3)\b"))
            {
                result.Add(activeConfig.ResolvePriorityName("Low"));
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ResolveIssueTypeFilters(string cleanedLower, ProjectActiveConfig activeConfig)
    {
        var result = new List<string>();
        if (Regex.IsMatch(cleanedLower, @"\bbug\b") || cleanedLower.Contains("lỗi") || cleanedLower.Contains("hotfix"))
        {
            var bugType = activeConfig.IssueTypes.FirstOrDefault(t => t.Name.Contains("Bug", StringComparison.OrdinalIgnoreCase));
            result.Add(bugType?.Name ?? "Bug");
        }
        if (cleanedLower.Contains("user story") || Regex.IsMatch(cleanedLower, @"\b(story|stories)\b"))
        {
            var storyType = activeConfig.IssueTypes.FirstOrDefault(t => t.Name.Contains("Story", StringComparison.OrdinalIgnoreCase));
            result.Add(storyType?.Name ?? "Story");
        }
        if (cleanedLower.Contains("chỉ loại task") || cleanedLower.Contains("loại công việc task"))
        {
            result.Add("Task");
        }
        if (cleanedLower.Contains("technical debt") || cleanedLower.Contains("nợ kỹ thuật"))
        {
            result.Add("TechnicalDebt");
        }
        if (cleanedLower.Contains("chỉ epic") || cleanedLower.Contains("các epic") || cleanedLower.Contains("danh sách epic"))
        {
            result.Add("Epic");
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ResolveEpicFilters(string cleanedLower, IReadOnlyList<string> epics)
    {
        var result = new List<string>();
        foreach (var epic in epics)
        {
            var eLower = epic.Trim().ToLowerInvariant();
            if (eLower.Length >= 4 && cleanedLower.Contains(eLower, StringComparison.Ordinal))
            {
                result.Add(epic);
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<ExportIssueMetadata> EvaluateMatchingIssues(
        IReadOnlyList<ExportIssueMetadata> allIssues,
        string scopeName,
        IReadOnlyList<ExportSprintMetadata> matchedSprints,
        List<string> assigneeFilters,
        List<string> statusFilters,
        List<string> priorityFilters,
        List<string> issueTypeFilters,
        List<string> epicFilters,
        bool blockedOnly,
        bool includeSubtasks)
    {
        var sprintIdSet = matchedSprints.Select(s => s.Id).ToHashSet();
        bool includeEpicsExplicitly = issueTypeFilters.Any(t => t.Equals("Epic", StringComparison.OrdinalIgnoreCase));

        IEnumerable<ExportIssueMetadata> candidates = allIssues.Where(i => includeEpicsExplicitly || !i.IsEpic);

        if (scopeName is "single_sprint" or "sprint_list")
        {
            candidates = candidates.Where(i => i.SprintId.HasValue && sprintIdSet.Contains(i.SprintId.Value));
        }
        else if (scopeName == "backlog_only")
        {
            candidates = candidates.Where(i => i.SprintId == null);
        }

        if (!includeSubtasks)
        {
            candidates = candidates.Where(i => i.ParentId == null && !i.IsSubtask);
        }

        var list = candidates.ToList();

        bool hasAttrFilter = assigneeFilters.Count > 0 || statusFilters.Count > 0 || priorityFilters.Count > 0 ||
                             issueTypeFilters.Count > 0 || epicFilters.Count > 0 || blockedOnly;
        if (!hasAttrFilter)
        {
            return list;
        }

        bool MatchesAttrs(ExportIssueMetadata item)
        {
            if (assigneeFilters.Count > 0)
            {
                bool matchesUnassigned = assigneeFilters.Contains("__UNASSIGNED__") && string.IsNullOrWhiteSpace(item.AssigneeName);
                bool matchesNamed = !string.IsNullOrWhiteSpace(item.AssigneeName) &&
                                    assigneeFilters.Any(a => a != "__UNASSIGNED__" && item.AssigneeName.Equals(a, StringComparison.OrdinalIgnoreCase));
                if (!matchesUnassigned && !matchesNamed) return false;
            }

            if (statusFilters.Count > 0 && !statusFilters.Any(s => item.StatusName.Equals(s, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (priorityFilters.Count > 0 && !priorityFilters.Any(p => item.Priority.Equals(p, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (issueTypeFilters.Count > 0 && !issueTypeFilters.Any(t => item.IssueType.Equals(t, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (epicFilters.Count > 0 && (string.IsNullOrWhiteSpace(item.EpicTitle) || !epicFilters.Any(e => item.EpicTitle.Equals(e, StringComparison.OrdinalIgnoreCase))))
                return false;

            if (blockedOnly && !item.IsBlocked)
                return false;

            return true;
        }

        return list.Where(MatchesAttrs).ToList();
    }

    private static List<string> BuildFilterSummaryParts(
        List<ExportSprintMetadata> matchedSprints,
        bool isBacklogOnly,
        List<string> assigneeFilters,
        List<string> statusFilters,
        List<string> priorityFilters,
        List<string> issueTypeFilters,
        List<string> epicFilters,
        bool blockedOnly)
    {
        var parts = new List<string>();
        if (matchedSprints.Count == 1) parts.Add($"Sprint: {matchedSprints[0].Name}");
        else if (matchedSprints.Count > 1) parts.Add($"Sprints: {string.Join(", ", matchedSprints.Select(s => s.Name))}");
        if (isBacklogOnly) parts.Add("Chỉ Kho Product Backlog");
        if (issueTypeFilters.Count > 0) parts.Add($"Loại: {string.Join(", ", issueTypeFilters)}");
        if (statusFilters.Count > 0) parts.Add($"Trạng thái: {string.Join(", ", statusFilters)}");
        if (priorityFilters.Count > 0) parts.Add($"Ưu tiên: {string.Join(", ", priorityFilters)}");
        if (assigneeFilters.Count > 0)
        {
            var labels = assigneeFilters.Select(a => a == "__UNASSIGNED__" ? "Chưa gán" : a);
            parts.Add($"Phụ trách: {string.Join(", ", labels)}");
        }
        if (epicFilters.Count > 0) parts.Add($"Epic: {string.Join(", ", epicFilters)}");
        if (blockedOnly) parts.Add("Đang bị chặn (Blocked)");
        return parts;
    }

    private static (string Title, string FileName, string Reply) BuildSuccessMetadata(
        string scopeName,
        ProjectExportMetadataResult metadata,
        List<ExportSprintMetadata> matchedSprints,
        int matchedCount,
        List<string> filterParts,
        string safeKey,
        string dateStamp,
        AiSprintProposalDto? existingProposal)
    {
        var draftNotice = existingProposal?.Tasks is { Count: > 0 }
            ? $" (Lưu ý: {existingProposal.Tasks.Count} việc trong bảng đề xuất nháp chưa lưu vào dự án nên không gộp vào dữ liệu thực này.)"
            : string.Empty;

        switch (scopeName)
        {
            case "single_sprint":
            {
                var sp = matchedSprints[0];
                var spSlug = SanitizeFileToken(sp.Name, maxLen: 28);
                var title = $"BÁO CÁO CÔNG VIỆC - {sp.Name.ToUpperInvariant()}";
                var file = $"{safeKey}_{spSlug}_{dateStamp}.xlsx";
                var filterNote = filterParts.Count > 1 ? $" (đã áp dụng bộ lọc: {string.Join(", ", filterParts.Skip(1))})" : string.Empty;
                var reply = $"Tôi đã chuẩn bị tệp Excel chỉ chứa **{matchedCount} công việc thuộc {sp.Name}**{filterNote}, trình bày dạng bảng sạch kèm màu phân biệt trạng thái và bảng chú thích.{draftNotice}";
                return (title, file, reply);
            }
            case "sprint_list":
            {
                var names = string.Join(", ", matchedSprints.Select(s => s.Name));
                var title = $"BÁO CÁO CÁC SPRINT - {metadata.ProjectName.ToUpperInvariant()}";
                var file = $"{safeKey}_Cac_Sprint_{dateStamp}.xlsx";
                var reply = $"Tôi đã chuẩn bị tệp Excel tổng hợp **{matchedCount} công việc** thuộc {matchedSprints.Count} Sprint ({names}), kèm màu trạng thái và chú thích.{draftNotice}";
                return (title, file, reply);
            }
            case "backlog_only":
            {
                var title = $"KHO PRODUCT BACKLOG - {metadata.ProjectName.ToUpperInvariant()}";
                var file = $"{safeKey}_Kho_Backlog_{dateStamp}.xlsx";
                var reply = $"Tôi đã chuẩn bị tệp Excel chỉ gồm **{matchedCount} công việc trong kho Product Backlog** (không kèm các Sprint), dạng bảng sạch chuẩn ProjectMgmt kèm màu trạng thái và chú thích.{draftNotice}";
                return (title, file, reply);
            }
            case "filtered_tasks":
            {
                var summary = string.Join(" · ", filterParts);
                var title = $"DANH SÁCH CÔNG VIỆC THEO BỘ LỌC - {metadata.ProjectName.ToUpperInvariant()}";
                var file = $"{safeKey}_Danh_Sach_Loc_{dateStamp}.xlsx";
                var reply = $"Tôi đã lọc và xuất **{matchedCount} công việc** thỏa điều kiện (**{summary}**) dưới dạng bảng sạch chuẩn ProjectMgmt có màu trạng thái và bảng chú thích.{draftNotice}";
                return (title, file, reply);
            }
            default:
            {
                var title = $"BÁO CÁO TỔNG HỢP DỰ ÁN - {metadata.ProjectName.ToUpperInvariant()}";
                var file = $"{safeKey}_Bao_Cao_Du_An_{dateStamp}.xlsx";
                var reply = $"Tôi đã kích hoạt xuất tệp Excel tổng hợp toàn bộ **{matchedCount} công việc** trong tất cả Sprint và kho Product Backlog đang lưu trong dự án {metadata.ProjectName} (kèm màu trạng thái và chú thích).{draftNotice}";
                return (title, file, reply);
            }
        }
    }

    private static bool ContainsWholePhrase(string source, string phrase)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(phrase)) return false;
        return Regex.IsMatch(source, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }

    private static string SanitizeFileToken(string input, int maxLen = 32)
    {
        if (string.IsNullOrWhiteSpace(input)) return "ProjectMgmt";
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
            }
        }
        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        var cleaned = Regex.Replace(ascii, @"[^A-Za-z0-9\-_]+", "_").Trim('_');
        if (cleaned.Length > maxLen) cleaned = cleaned[..maxLen].TrimEnd('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "ProjectMgmt" : cleaned;
    }
}
