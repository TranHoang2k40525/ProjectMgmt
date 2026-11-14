namespace ProjectMgmt.Solution.Services.Ai;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.McpServers;

/// <summary>
/// Bộ phân giải danh sách công việc thực tế trong dự án phục vụ thao tác Gỡ khỏi Sprint / Xóa công việc có xác nhận.
/// Luôn chỉ lọc trong phạm vi dự án hiện tại và trả về kế hoạch xác nhận trước khi tác động dữ liệu.
/// </summary>
internal static partial class AiTaskRemovalResolver
{
    public sealed record ResolveResult(
        bool HasCandidates,
        string ReplyMessage,
        string TraceSummary,
        AiTaskRemovalPlanDto? Plan);

    [GeneratedRegex(@"\b([a-z][a-z0-9]{1,9}-\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex IssueKeyRegex();

    [GeneratedRegex(@"\bsprint\s*[-#:]?\s*(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SprintNumberRegex();

    [GeneratedRegex(@"[""']([^""']{3,160})[""']")]
    private static partial Regex QuotedTitleRegex();

    public static ResolveResult Resolve(
        string userPrompt,
        ProjectExportMetadataResult meta,
        AiCommittedOriginSnapshot originSnapshot,
        AiSprintProposalDto? existingProposal,
        bool allowDelete = true,
        bool allowDetach = true)
    {
        var prompt = (userPrompt ?? string.Empty).Trim();
        var lower = prompt.ToLowerInvariant();
        var foldedPrompt = FoldVietnamese(prompt);

        var completedSprintIds = meta.Sprints
            .Where(s => string.Equals(s.Status, "completed", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .ToHashSet();

        var issueById = meta.Issues
            .Where(i => !i.IsEpic)
            .ToDictionary(i => i.Id);

        var parentByKey = meta.Issues
            .Where(i => !i.IsEpic)
            .GroupBy(i => i.IssueKey.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var childrenByParentId = meta.Issues
            .Where(i => !i.IsEpic && i.ParentId.HasValue)
            .GroupBy(i => i.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var parentIssues = meta.Issues
            .Where(i => !i.IsEpic && i.ParentId == null)
            .ToList();

        var sessionCommittedMovedIds = new HashSet<Guid>(originSnapshot.MovedFromBacklogIds);
        var matchedParents = new List<ExportIssueMetadata>();
        var matchedIds = new HashSet<Guid>();
        var scopeDesc = string.Empty;
        var hasAppliedMatch = false;

        // 1. Ưu tiên cao nhất: Mã công việc cụ thể (QLDA-18, QLDA-25...)
        var explicitKeys = IssueKeyRegex().Matches(prompt)
            .Select(m => m.Groups[1].Value.Trim().ToUpperInvariant())
            .Where(k => !k.Contains("-AI", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (explicitKeys.Count > 0)
        {
            var missingKeys = new List<string>();
            var completedSkippedKeys = new List<string>();

            foreach (var key in explicitKeys)
            {
                if (!parentByKey.TryGetValue(key, out var found))
                {
                    missingKeys.Add(key);
                    continue;
                }

                if (found.SprintId.HasValue && completedSprintIds.Contains(found.SprintId.Value))
                {
                    completedSkippedKeys.Add(found.IssueKey);
                    continue;
                }

                if (matchedIds.Add(found.Id))
                {
                    matchedParents.Add(found);
                }
            }

            if (matchedParents.Count == 0)
            {
                var detail = completedSkippedKeys.Count > 0
                    ? $"Công việc {string.Join(", ", completedSkippedKeys)} đang nằm trong Sprint đã hoàn thành nên không thể gỡ hoặc xóa."
                    : $"Không tìm thấy công việc {string.Join(", ", missingKeys)} trong dự án {meta.ProjectKey}. Vui lòng kiểm tra lại mã công việc.";
                return new ResolveResult(false, detail, $"Không tìm thấy mã công việc hợp lệ ({string.Join(", ", explicitKeys)})", null);
            }

            scopeDesc = $"Các công việc chỉ định: {string.Join(", ", matchedParents.Select(m => m.IssueKey))}";
        }

        // 2. Nhóm công việc do Trợ Lý AI vừa áp dụng / đẩy vào Backlog trong phiên (hoặc batch AI gần nhất)
        bool asksForAppliedBatch =
            lower.Contains("áp dụng") || lower.Contains("apply") || lower.Contains("commit") ||
            lower.Contains("vừa đẩy") || lower.Contains("mới đẩy") || lower.Contains("đã đẩy") || lower.Contains("bạn đẩy") ||
            lower.Contains("vừa tạo") || lower.Contains("mới tạo") || lower.Contains("bạn tạo") || lower.Contains("ai tạo") ||
            lower.Contains("vừa thêm") || lower.Contains("mới thêm") || lower.Contains("bạn thêm") ||
            lower.Contains("hoàn tác") || lower.Contains("rollback") || lower.Contains("undo") || lower.Contains("revert") ||
            ((lower.Contains("vừa rồi") || lower.Contains("lúc nãy") || lower.Contains("khi nãy") || lower.Contains("của bạn")) &&
             (lower.Contains("việc") || lower.Contains("task")));

        if (matchedParents.Count == 0 && asksForAppliedBatch)
        {
            bool wantsAllSessionBatches = lower.Contains("tất cả") || lower.Contains("toàn bộ các lần");
            bool prefersBacklogBatch = lower.Contains("backlog") && !lower.Contains("sprint");
            bool prefersSprintBatch = lower.Contains("sprint") && !lower.Contains("backlog");

            var sessionCommitTurns = (existingProposal?.ConversationHistory ?? [])
                .Where(t => t.Role == "assistant" &&
                            (t.ActionType is "apply_sprint_done" or "push_backlog_done") &&
                            t.CommittedIssues is { Count: > 0 })
                .ToList();

            var candidateCommitted = new List<AiCreatedIssueItemDto>();
            if (sessionCommitTurns.Count > 0)
            {
                IEnumerable<AiConversationTurnDto> selectedTurns = sessionCommitTurns;
                if (!wantsAllSessionBatches)
                {
                    var matchingKind = sessionCommitTurns
                        .LastOrDefault(t =>
                            (prefersBacklogBatch && t.ActionType == "push_backlog_done") ||
                            (prefersSprintBatch && t.ActionType == "apply_sprint_done"))
                        ?? sessionCommitTurns.Last();
                    selectedTurns = [matchingKind];
                }

                foreach (var turn in selectedTurns)
                {
                    foreach (var item in turn.CommittedIssues ?? [])
                    {
                        if (item.ParentIssueId == null)
                        {
                            candidateCommitted.Add(item);
                            if (item.MovedFromBacklog)
                            {
                                sessionCommittedMovedIds.Add(item.IssueId);
                            }
                        }
                    }
                }
            }

            foreach (var item in candidateCommitted)
            {
                if (issueById.TryGetValue(item.IssueId, out var liveIssue) &&
                    (!liveIssue.SprintId.HasValue || !completedSprintIds.Contains(liveIssue.SprintId.Value)) &&
                    matchedIds.Add(liveIssue.Id))
                {
                    matchedParents.Add(liveIssue);
                }
            }

            if (matchedParents.Count == 0 && originSnapshot.SessionCommittedIds.Count > 0)
            {
                foreach (var id in originSnapshot.SessionCommittedIds)
                {
                    if (issueById.TryGetValue(id, out var liveIssue) &&
                        liveIssue.ParentId == null &&
                        (!liveIssue.SprintId.HasValue || !completedSprintIds.Contains(liveIssue.SprintId.Value)) &&
                        matchedIds.Add(liveIssue.Id))
                    {
                        matchedParents.Add(liveIssue);
                    }
                }
            }

            if (matchedParents.Count == 0 && originSnapshot.LatestAiBatchIds.Count > 0)
            {
                foreach (var id in originSnapshot.LatestAiBatchIds)
                {
                    if (issueById.TryGetValue(id, out var liveIssue) &&
                        liveIssue.ParentId == null &&
                        (!liveIssue.SprintId.HasValue || !completedSprintIds.Contains(liveIssue.SprintId.Value)) &&
                        matchedIds.Add(liveIssue.Id))
                    {
                        matchedParents.Add(liveIssue);
                    }
                }
            }

            if (matchedParents.Count > 0)
            {
                hasAppliedMatch = true;
                scopeDesc = "Các công việc AI đã áp dụng / đẩy vào dự án trong phiên gần nhất";
            }
            else
            {
                return new ResolveResult(
                    false,
                    "Hiện không còn công việc nào từ lượt áp dụng trước cần gỡ hoặc xóa (có thể các công việc đó đã được gỡ/xóa trước đó hoặc chưa được lưu vào dự án). Nếu bạn muốn gỡ/xóa công việc khác, hãy nêu rõ mã công việc (ví dụ: `QLDA-18`) hoặc tên Sprint.",
                    "Không tìm thấy công việc đã áp dụng còn hiệu lực trong phiên",
                    null);
            }
        }

        // 3. Bộ lọc theo Sprint / Kho Backlog / Thành viên / Trạng thái / Tên công việc
        if (matchedParents.Count == 0)
        {
            var matchedSprint = ResolveTargetSprint(prompt, foldedPrompt, meta.Sprints);
            bool targetsBacklog = lower.Contains("kho backlog") || lower.Contains("product backlog") ||
                                  lower.Contains("trong backlog") || lower.Contains("ở backlog");
            var matchedMember = ResolveTargetMember(foldedPrompt, meta.Members);
            var matchedStatus = ResolveTargetStatus(foldedPrompt, parentIssues);
            var quotedTitle = QuotedTitleRegex().Match(prompt) is { Success: true } qm
                ? qm.Groups[1].Value.Trim()
                : null;
            bool aiGeneratedOnly = lower.Contains("do ai") || lower.Contains("ai tạo") || lower.Contains("ai sinh");

            if (matchedSprint != null && string.Equals(matchedSprint.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolveResult(
                    false,
                    $"Sprint **{matchedSprint.Name}** đã hoàn thành nên hệ thống khóa không cho phép gỡ hoặc xóa hàng loạt công việc bên trong.",
                    $"Từ chối thao tác trên Sprint đã hoàn thành ({matchedSprint.Name})",
                    null);
            }

            bool hasSpecificFilter =
                matchedSprint != null ||
                targetsBacklog ||
                matchedMember != null ||
                matchedStatus != null ||
                !string.IsNullOrWhiteSpace(quotedTitle) ||
                aiGeneratedOnly;

            if (!hasSpecificFilter)
            {
                return new ResolveResult(
                    false,
                    "Để đảm bảo an toàn dữ liệu, vui lòng chỉ rõ bạn muốn gỡ hoặc xóa công việc nào — ví dụ: `\"gỡ các việc bạn vừa áp dụng\"`, `\"xóa task QLDA-18\"`, hoặc `\"gỡ các task của [Thành viên] khỏi Sprint 4\"`. Tôi sẽ hiển thị danh sách xác nhận trước khi thực thi.",
                    "Thiếu tiêu chí xác định công việc mục tiêu",
                    null);
            }

            IEnumerable<ExportIssueMetadata> query = parentIssues
                .Where(i => !i.SprintId.HasValue || !completedSprintIds.Contains(i.SprintId.Value));

            var descParts = new List<string>();
            if (matchedSprint != null)
            {
                query = query.Where(i => i.SprintId == matchedSprint.Id);
                descParts.Add($"Sprint {matchedSprint.Name}");
            }
            else if (targetsBacklog)
            {
                query = query.Where(i => !i.SprintId.HasValue);
                descParts.Add("Kho Product Backlog");
            }

            if (matchedMember != null)
            {
                query = query.Where(i => i.AssigneeUserId == matchedMember.UserId);
                descParts.Add($"phụ trách: {matchedMember.DisplayName}");
            }

            if (!string.IsNullOrWhiteSpace(matchedStatus))
            {
                query = query.Where(i => string.Equals(i.StatusName, matchedStatus, StringComparison.OrdinalIgnoreCase));
                descParts.Add($"trạng thái: {matchedStatus}");
            }

            if (aiGeneratedOnly)
            {
                query = query.Where(i => originSnapshot.AiGeneratedIssueIds.Contains(i.Id));
                descParts.Add("do AI khởi tạo");
            }

            if (!string.IsNullOrWhiteSpace(quotedTitle))
            {
                var foldedQuote = FoldVietnamese(quotedTitle);
                query = query.Where(i => FoldVietnamese(i.Title).Contains(foldedQuote, StringComparison.Ordinal));
                descParts.Add($"tiêu đề chứa \"{quotedTitle}\"");
            }

            matchedParents = query.Take(50).ToList();
            if (matchedParents.Count == 0)
            {
                return new ResolveResult(
                    false,
                    "Không tìm thấy công việc nào khớp với tiêu chí bạn nêu trong dự án hiện tại. Hãy kiểm tra lại mã công việc (`QLDA-...`) hoặc tên Sprint/thành viên.",
                    "Không có công việc khớp bộ lọc gỡ/xóa",
                    null);
            }

            scopeDesc = descParts.Count > 0
                ? $"Công việc lọc theo {string.Join(", ", descParts)}"
                : "Công việc phù hợp trong dự án";
        }

        var candidates = matchedParents.Select(p =>
        {
            var children = childrenByParentId.GetValueOrDefault(p.Id) ?? [];
            return new AiRemovalCandidateIssueDto
            {
                IssueId = p.Id,
                IssueKey = p.IssueKey,
                Title = p.Title,
                IssueType = p.IssueType,
                SprintId = p.SprintId,
                SprintName = p.SprintName,
                StatusName = p.StatusName,
                Priority = p.Priority,
                StoryPoints = p.StoryPoints,
                AssigneeName = p.AssigneeName,
                SubtaskCount = children.Count,
                ChildIssueKeys = children.Take(6).Select(c => c.IssueKey).ToList(),
                WasMovedFromBacklog = sessionCommittedMovedIds.Contains(p.Id),
                IsAiGenerated = originSnapshot.AiGeneratedIssueIds.Contains(p.Id),
                SelectedByDefault = true
            };
        }).ToList();

        bool anyInSprint = candidates.Any(c => c.SprintId.HasValue);
        bool anyMovedFromBacklog = candidates.Any(c => c.WasMovedFromBacklog);
        bool explicitDeleteVerb =
            lower.Contains("xóa") || lower.Contains("xoá") || lower.Contains("delete") ||
            lower.Contains("hủy bỏ") || lower.Contains("loại bỏ hẳn");
        bool explicitDetachVerb =
            lower.Contains("khỏi sprint") || lower.Contains("về backlog") || lower.Contains("sang backlog") ||
            lower.Contains("trả lại backlog") || lower.Contains("rút khỏi");

        var requestedMode = explicitDetachVerb
            ? "detach_to_backlog"
            : explicitDeleteVerb
                ? "soft_delete"
                : (anyInSprint && anyMovedFromBacklog ? "detach_to_backlog" : (anyInSprint ? "detach_to_backlog" : "soft_delete"));

        if (requestedMode == "detach_to_backlog" && !anyInSprint)
        {
            requestedMode = "soft_delete";
        }

        var plan = new AiTaskRemovalPlanDto
        {
            PlanId = Guid.NewGuid().ToString("N"),
            RequestedMode = requestedMode,
            CanDetachToBacklog = allowDetach && anyInSprint,
            CanDelete = allowDelete,
            HasAppliedFromSessionMatch = hasAppliedMatch,
            TargetScopeDescription = scopeDesc,
            Status = "pending",
            Candidates = candidates
        };

        var totalSubtasks = candidates.Sum(c => c.SubtaskCount);
        var subText = totalSubtasks > 0 ? $" cùng **{totalSubtasks} việc con** trực thuộc" : string.Empty;
        var modeHint = plan.CanDetachToBacklog && plan.CanDelete
            ? "Bạn có thể chọn **Gỡ khỏi Sprint (Về Backlog)** để giữ lại công việc hoặc **Xác nhận Xóa** nếu muốn xóa mềm khỏi dự án."
            : plan.CanDetachToBacklog
                ? "Nhấn **Gỡ khỏi Sprint (Về Backlog)** ở thẻ xác nhận bên dưới để hoàn tất."
                : plan.CanDelete
                    ? "Nhấn **Xác nhận Xóa công việc** ở thẻ xác nhận bên dưới để hoàn tất."
                    : "Tài khoản hiện tại của bạn chỉ có quyền xem danh sách đối chiếu và chưa được cấp quyền gỡ/xóa công việc.";

        var backlogNotice = anyMovedFromBacklog
            ? " Lưu ý: Một số công việc được gắn nhãn **Chuyển từ Backlog** là việc đã có sẵn từ trước khi áp dụng Sprint."
            : string.Empty;

        var reply = $"Tôi đã tìm thấy **{candidates.Count} công việc**{subText} phù hợp ({scopeDesc}). Để đảm bảo an toàn, hệ thống chưa thực hiện thay đổi nào — vui lòng kiểm tra danh sách bên dưới và bấm nút xác nhận.{backlogNotice} {modeHint}";

        return new ResolveResult(
            true,
            reply,
            $"Chuẩn bị bảng xác nhận gỡ/xóa cho {candidates.Count} công việc ({candidates.Sum(c => c.StoryPoints):0.#} SP)",
            plan);
    }

    private static ExportSprintMetadata? ResolveTargetSprint(
        string prompt,
        string foldedPrompt,
        IReadOnlyList<ExportSprintMetadata> sprints)
    {
        if (sprints.Count == 0) return null;

        // Khớp tên Sprint đầy đủ trước
        var exactByName = sprints
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .OrderByDescending(s => s.Name.Length)
            .FirstOrDefault(s => foldedPrompt.Contains(FoldVietnamese(s.Name), StringComparison.Ordinal));
        if (exactByName != null) return exactByName;

        // Khớp theo "Sprint <số>"
        var numMatch = SprintNumberRegex().Match(prompt);
        if (numMatch.Success)
        {
            var numToken = $"sprint {numMatch.Groups[1].Value}";
            var byNum = sprints.FirstOrDefault(s => FoldVietnamese(s.Name).Contains(numToken, StringComparison.Ordinal));
            if (byNum != null) return byNum;
        }

        if (foldedPrompt.Contains("sprint hien tai") || foldedPrompt.Contains("sprint dang chay"))
        {
            return sprints.FirstOrDefault(s => s.Status == "active");
        }

        if (foldedPrompt.Contains("sprint tiep theo") || foldedPrompt.Contains("sprint toi") || foldedPrompt.Contains("sprint sap toi"))
        {
            return sprints.FirstOrDefault(s => s.Status == "future" || s.Status == "planned");
        }

        return null;
    }

    private static ExportMemberMetadata? ResolveTargetMember(
        string foldedPrompt,
        IReadOnlyList<ExportMemberMetadata> members)
    {
        if (members.Count == 0) return null;

        foreach (var member in members.OrderByDescending(m => m.DisplayName.Length))
        {
            var clean = StripTrailingParenthetical(member.DisplayName);
            var foldedFull = FoldVietnamese(clean);
            if (!string.IsNullOrWhiteSpace(foldedFull) &&
                foldedPrompt.Contains(foldedFull, StringComparison.Ordinal))
            {
                return member;
            }
        }

        // Khớp theo 2 từ cuối của tên (ví dụ "Đức Anh", "Hoàng Nam") nếu duy nhất
        var tailMatches = new List<ExportMemberMetadata>();
        foreach (var member in members)
        {
            var words = FoldVietnamese(StripTrailingParenthetical(member.DisplayName))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                var lastTwo = $"{words[^2]} {words[^1]}";
                if (foldedPrompt.Contains(lastTwo, StringComparison.Ordinal))
                {
                    tailMatches.Add(member);
                }
            }
        }

        return tailMatches.Count == 1 ? tailMatches[0] : null;
    }

    private static string? ResolveTargetStatus(
        string foldedPrompt,
        IReadOnlyList<ExportIssueMetadata> issues)
    {
        var distinctStatuses = issues
            .Select(i => i.StatusName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(s => s.Length)
            .ToList();

        foreach (var status in distinctStatuses)
        {
            var foldedStatus = FoldVietnamese(status);
            if (!string.IsNullOrWhiteSpace(foldedStatus) &&
                foldedPrompt.Contains(foldedStatus, StringComparison.Ordinal))
            {
                return status;
            }
        }

        return null;
    }

    private static string StripTrailingParenthetical(string text)
    {
        var idx = text.IndexOf('(');
        return idx > 0 ? text[..idx].Trim() : text.Trim();
    }

    private static string FoldVietnamese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'd' : char.ToLowerInvariant(ch));
            }
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
