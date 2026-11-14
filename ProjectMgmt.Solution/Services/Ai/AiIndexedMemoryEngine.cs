namespace ProjectMgmt.Solution.Services.Ai;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMgmt.Solution.Dtos.Ai;

public sealed record AiContextExecutionPlan(
    string Intent,
    bool IsGreetingOrHelp,
    bool UseMemoryOnly,
    bool NeedsSpecHub,
    string SpecSearchKeyword,
    int MaxSpecDocsToRead,
    bool NeedsBacklog,
    bool NeedsSprintHistory,
    bool NeedsTeamCapacity,
    bool NeedsActiveConfig,
    List<AiMemoryAnchorDto> RecalledMemories);

/// <summary>
/// Động cơ quản lý Chỉ mục Trí nhớ Phiên (#mem-X) và Hoạch định Công cụ MCP Có Chọn Lọc.
/// Giúp Agent trỏ đúng nút trí nhớ liên quan (tránh nạp tràn toàn bộ lịch sử cũ)
/// và chỉ gọi MCP Tools khi thực sự cần thiết theo ngữ cảnh đầu vào.
/// </summary>
public static partial class AiIndexedMemoryEngine
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "của", "và", "là", "cho", "với", "các", "những", "này", "đó", "nào", "gì", "sao",
        "tôi", "bạn", "mình", "chúng", "ta", "giúp", "hãy", "về", "trong", "ngoài", "trên",
        "dưới", "theo", "từ", "đến", "như", "thế", "không", "có", "được", "làm", "xem",
        "cho", "hỏi", "biết", "đang", "đã", "sẽ", "cần", "muốn", "phải", "nên", "hay",
        "hoặc", "nhưng", "mà", "thì", "lúc", "nãy", "vừa", "rồi", "để", "vào", "ra",
        "the", "and", "for", "with", "that", "this", "from", "what", "how", "can", "you"
    };

    [GeneratedRegex(@"#?mem-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MemoryAnchorRegex();

    [GeneratedRegex(@"\b[A-Z]{2,10}-\d+\b", RegexOptions.Compiled)]
    private static partial Regex IssueKeyRegex();

    [GeneratedRegex(@"[\p{L}\p{N}_\-]{2,}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    public static AiContextExecutionPlan PlanContextExecution(
        string intent,
        string effectiveUserText,
        string docContent,
        bool hasAttachedDoc,
        AiSprintProposalDto? existingProposal,
        IReadOnlyList<AiMemoryAnchorDto> allMemories,
        IReadOnlyList<AiConversationTurnDto>? conversationHistory = null)
    {
        var lower = (effectiveUserText ?? string.Empty).Trim().ToLowerInvariant();
        var combinedLower = $"{lower}\n{(docContent ?? string.Empty).ToLowerInvariant()}";
        var historyWindow = conversationHistory ?? existingProposal?.ConversationHistory;
        var isFollowUp = IsConversationalFollowUp(lower) && (historyWindow?.Count > 0 || allMemories.Count > 0);
        var topicHint = isFollowUp ? BuildRecentTopicHint(historyWindow) : string.Empty;
        var recalled = SelectRelevantMemories(effectiveUserText, docContent, allMemories, maxMatches: 4, historyWindow);

        // 1. Guardrail & Direct Commands: Không gọi thừa các MCP tools không liên quan
        if (intent is "out_of_scope_refusal" or "security_refusal" or "push_backlog" or "apply_sprint" or "export_excel")
        {
            return new AiContextExecutionPlan(
                Intent: intent,
                IsGreetingOrHelp: false,
                UseMemoryOnly: false,
                NeedsSpecHub: false,
                SpecSearchKeyword: string.Empty,
                MaxSpecDocsToRead: 0,
                NeedsBacklog: false,
                NeedsSprintHistory: false,
                NeedsTeamCapacity: false,
                NeedsActiveConfig: intent == "export_excel",
                RecalledMemories: recalled);
        }

        // 2. Chào hỏi / Hỏi khả năng của Trợ Lý AI -> 0 MCP calls
        bool isGreetingOrHelp = IsGreetingOrCapabilityQuestion(lower) && !ContainsProjectDataKeyword(lower);
        if (intent == "project_qa" && isGreetingOrHelp)
        {
            return new AiContextExecutionPlan(
                Intent: intent,
                IsGreetingOrHelp: true,
                UseMemoryOnly: false,
                NeedsSpecHub: false,
                SpecSearchKeyword: string.Empty,
                MaxSpecDocsToRead: 0,
                NeedsBacklog: false,
                NeedsSprintHistory: false,
                NeedsTeamCapacity: false,
                NeedsActiveConfig: false,
                RecalledMemories: []);
        }

        // 3. Hỏi đáp dựa trên mạch hội thoại / đề xuất / trí nhớ đã đánh chỉ mục (#mem-X) mà không đòi hỏi truy vấn DB mới
        bool explicitMemoryRef = MemoryAnchorRegex().IsMatch(lower) ||
                                 lower.Contains("lúc nãy") || lower.Contains("vừa nãy") ||
                                 lower.Contains("hồi nãy") || lower.Contains("vừa rồi") ||
                                 lower.Contains("đề xuất hiện tại") || lower.Contains("đoạn chat") ||
                                 lower.Contains("chúng ta đã") || lower.Contains("nhắc lại") ||
                                 lower.Contains("phần trên") || lower.Contains("ở trên") ||
                                 lower.Contains("vừa bàn") || lower.Contains("trong phiên");
        bool asksLiveDbData = MentionsSpecHub(combinedLower) || MentionsBacklog(combinedLower) ||
                              MentionsSprint(combinedLower) || MentionsTeam(combinedLower) ||
                              MentionsConfig(combinedLower) || lower.Contains("dự án này") ||
                              lower.Contains("hệ thống") || lower.Contains("dữ liệu thật");
        bool asksDeeperDbFetch = asksLiveDbData &&
                                 (lower.Contains("kiểm tra lại") || lower.Contains("đọc lại") ||
                                  lower.Contains("đọc sâu") || lower.Contains("còn ") ||
                                  lower.Contains("dữ liệu thật") || lower.Contains("trong db") ||
                                  lower.Contains("mới nhất"));

        if (intent == "project_qa" &&
            !asksDeeperDbFetch &&
            (historyWindow?.Count > 1 || allMemories.Count > 0 || existingProposal?.Tasks?.Count > 0) &&
            (explicitMemoryRef ||
             (isFollowUp && !asksLiveDbData && (recalled.Count > 0 || (historyWindow?.Count ?? 0) > 1)) ||
             (recalled.Count > 0 && !asksLiveDbData)))
        {
            bool hasProposalMatch = existingProposal?.Tasks?.Count > 0 &&
                                    (MentionsProposalOrWorkItem(lower) || isFollowUp);
            if (explicitMemoryRef || hasProposalMatch || isFollowUp || (recalled.Count > 0 && !asksLiveDbData))
            {
                return new AiContextExecutionPlan(
                    Intent: intent,
                    IsGreetingOrHelp: false,
                    UseMemoryOnly: true,
                    NeedsSpecHub: false,
                    SpecSearchKeyword: string.Empty,
                    MaxSpecDocsToRead: 0,
                    NeedsBacklog: false,
                    NeedsSprintHistory: false,
                    NeedsTeamCapacity: false,
                    NeedsActiveConfig: false,
                    RecalledMemories: recalled);
            }
        }

        // 4. Hỏi đáp dự án (project_qa): CHỈ chọn đúng các MCP Tools người dùng thực sự hỏi (có kế thừa chủ đề hội thoại gần nhất nếu là câu hỏi nối tiếp)
        if (intent == "project_qa")
        {
            var topicAwareLower = string.IsNullOrWhiteSpace(topicHint)
                ? combinedLower
                : $"{combinedLower}\n{topicHint.ToLowerInvariant()}";

            bool asksOverview = lower.Contains("tổng quan") || lower.Contains("tóm tắt dự án") ||
                                lower.Contains("dự án có gì") || lower.Contains("dự án này") ||
                                lower.Contains("tình hình dự án");
            bool asksVerification = lower.Contains("có thật") || lower.Contains("đúng không") ||
                                    lower.Contains("phải không") || lower.Contains("nghe nói") ||
                                    lower.Contains("thực hư") || lower.Contains("đã hủy") ||
                                    lower.Contains("đã bỏ");

            bool needsSpec = asksOverview || MentionsSpecHub(combinedLower) || (isFollowUp && MentionsSpecHub(topicAwareLower) && !asksLiveDbData);
            bool needsBacklog = asksOverview || MentionsBacklog(combinedLower) || (isFollowUp && MentionsBacklog(topicAwareLower) && lower.Contains("còn "));
            bool needsSprint = asksOverview || asksVerification || MentionsSprint(combinedLower) || (isFollowUp && MentionsSprint(topicAwareLower) && lower.Contains("còn "));
            bool needsTeam = asksOverview || asksVerification || MentionsTeam(combinedLower);
            bool needsConfig = asksOverview || asksVerification || MentionsConfig(combinedLower);

            // Nếu người dùng đã đính kèm tài liệu nhưng câu hỏi không nhắc Spec Hub và chỉ hỏi tài liệu vừa gửi
            if (hasAttachedDoc && !lower.Contains("spec"))
            {
                needsSpec = false;
            }

            // Nếu câu hỏi dự án ngắn không khớp từ khóa cụ thể nào -> tận dụng trí nhớ & mạch hội thoại hiện tại thay vì gọi thừa 5 MCP tool
            if (!needsSpec && !needsBacklog && !needsSprint && !needsTeam && !needsConfig)
            {
                if (recalled.Count > 0 || (historyWindow?.Count ?? 0) > 1 || (existingProposal?.Tasks?.Count ?? 0) > 0)
                {
                    return new AiContextExecutionPlan(
                        Intent: intent,
                        IsGreetingOrHelp: false,
                        UseMemoryOnly: true,
                        NeedsSpecHub: false,
                        SpecSearchKeyword: string.Empty,
                        MaxSpecDocsToRead: 0,
                        NeedsBacklog: false,
                        NeedsSprintHistory: false,
                        NeedsTeamCapacity: false,
                        NeedsActiveConfig: false,
                        RecalledMemories: recalled);
                }

                needsSpec = true;
                needsConfig = true;
            }

            var specQuery = isFollowUp && !string.IsNullOrWhiteSpace(topicHint)
                ? BuildSpecSearchQuery($"{effectiveUserText} {topicHint}")
                : BuildSpecSearchQuery(effectiveUserText);

            return new AiContextExecutionPlan(
                Intent: intent,
                IsGreetingOrHelp: false,
                UseMemoryOnly: false,
                NeedsSpecHub: needsSpec,
                SpecSearchKeyword: specQuery,
                MaxSpecDocsToRead: needsSpec ? 2 : 0,
                NeedsBacklog: needsBacklog,
                NeedsSprintHistory: needsSprint,
                NeedsTeamCapacity: needsTeam,
                NeedsActiveConfig: needsConfig,
                RecalledMemories: recalled);
        }

        // 5. Tinh chỉnh đề xuất đang có (refine_proposal): không gọi lại toàn bộ SpecHub/Sprint trừ khi được nhắc
        if (intent == "refine_proposal")
        {
            bool needsConfig = (existingProposal?.AppliedWorkflowStatuses?.Count ?? 0) == 0 || MentionsConfig(combinedLower);
            bool needsBacklog = MentionsBacklog(combinedLower);
            bool needsTeam = MentionsTeam(combinedLower) || lower.Contains("giao") || lower.Contains("assign") ||
                             lower.Contains("người làm") || lower.Contains("thêm ");
            bool needsSpec = MentionsSpecHub(combinedLower);

            return new AiContextExecutionPlan(
                Intent: intent,
                IsGreetingOrHelp: false,
                UseMemoryOnly: !needsConfig && !needsBacklog && !needsTeam && !needsSpec,
                NeedsSpecHub: needsSpec,
                SpecSearchKeyword: BuildSpecSearchQuery(effectiveUserText),
                MaxSpecDocsToRead: needsSpec ? 2 : 0,
                NeedsBacklog: needsBacklog,
                NeedsSprintHistory: MentionsSprint(combinedLower) && lower.Contains("rollover"),
                NeedsTeamCapacity: needsTeam,
                NeedsActiveConfig: needsConfig,
                RecalledMemories: recalled);
        }

        // 6. Lập kế hoạch cho Kho Backlog (backlog_plan): cần cấu hình + Backlog hiện có (tránh trùng) + Team + Spec nếu không chỉ dùng file đính kèm
        if (intent == "backlog_plan")
        {
            bool skipSpec = (hasAttachedDoc || docContent.Length > 400) &&
                            (lower.Contains("chỉ dựa") || lower.Contains("theo tài liệu này") || lower.Contains("biên bản này"));
            bool needsSpec = !skipSpec || MentionsSpecHub(combinedLower);

            return new AiContextExecutionPlan(
                Intent: intent,
                IsGreetingOrHelp: false,
                UseMemoryOnly: false,
                NeedsSpecHub: needsSpec,
                SpecSearchKeyword: BuildSpecSearchQuery(effectiveUserText, docContent),
                MaxSpecDocsToRead: needsSpec ? 2 : 0,
                NeedsBacklog: true,
                NeedsSprintHistory: MentionsSprint(combinedLower),
                NeedsTeamCapacity: true,
                NeedsActiveConfig: true,
                RecalledMemories: recalled);
        }

        // 7. Lập kế hoạch Sprint (sprint_plan): lấy cấu hình áp dụng, Backlog (để chuyển việc không trùng), Sprint History (rollover/velocity), Team & Spec chọn lọc
        bool userOnlyInlineDoc = (hasAttachedDoc || docContent.Length > 500) &&
                                 (lower.Contains("chỉ dựa vào tài liệu") || lower.Contains("không cần spec"));

        return new AiContextExecutionPlan(
            Intent: intent,
            IsGreetingOrHelp: false,
            UseMemoryOnly: false,
            NeedsSpecHub: !userOnlyInlineDoc,
            SpecSearchKeyword: BuildSpecSearchQuery(effectiveUserText, docContent),
            MaxSpecDocsToRead: !userOnlyInlineDoc ? 3 : 0,
            NeedsBacklog: true,
            NeedsSprintHistory: true,
            NeedsTeamCapacity: true,
            NeedsActiveConfig: true,
            RecalledMemories: recalled);
    }

    public static void UpdateIndexedMemories(
        AiSprintProposalDto proposal,
        string? userPrompt,
        string? documentContent,
        string assistantReply,
        string intent)
    {
        proposal.IndexedMemories ??= [];
        var turnIndex = Math.Max(1, (proposal.ConversationHistory?.Count ?? 2) / 2);

        // 1. Lập chỉ mục tài liệu / yêu cầu dài đầu vào
        var cleanDoc = (documentContent ?? string.Empty).Trim();
        if (cleanDoc.Length > 80)
        {
            var docSnippet = cleanDoc.Length > 180 ? cleanDoc[..180] + "..." : cleanDoc;
            bool alreadyIndexed = proposal.IndexedMemories.Any(m =>
                m.Category == "requirement" &&
                m.Summary.StartsWith(docSnippet[..Math.Min(50, docSnippet.Length)], StringComparison.OrdinalIgnoreCase));

            if (!alreadyIndexed)
            {
                AddAnchor(
                    proposal.IndexedMemories,
                    turnIndex,
                    category: "requirement",
                    title: string.IsNullOrWhiteSpace(userPrompt)
                        ? "Tài liệu / Biên bản họp đầu vào"
                        : TruncateSingleLine(userPrompt, 80),
                    keywords: ExtractKeywords($"{userPrompt} {cleanDoc}"),
                    summary: SanitizeSingleLine(docSnippet, 220),
                    detailContent: cleanDoc.Length > 4000 ? cleanDoc[..4000] + "\n...[Đã lược bớt phần cuối]" : cleanDoc);
            }
        }

        // 2. Lập chỉ mục ảnh chụp kế hoạch công việc (khi vừa sinh hoặc chỉnh sửa danh sách Task)
        if (intent is "sprint_plan" or "backlog_plan" or "refine_proposal" && proposal.Tasks.Count > 0)
        {
            var taskDigest = new StringBuilder();
            taskDigest.AppendLine($"Mục tiêu: {proposal.SprintGoal} | Đích: {proposal.RecommendedTarget} | Tổng: {proposal.Tasks.Count} việc ({proposal.TotalStoryPoints} SP, {proposal.TotalEstimatedHours}h)");
            foreach (var t in proposal.Tasks)
            {
                var backlogTag = !string.IsNullOrWhiteSpace(t.SourceBacklogIssueKey) ? $" [Backlog:{t.SourceBacklogIssueKey}]" : string.Empty;
                taskDigest.AppendLine($"- [{t.Id}]{backlogTag} ({t.Type}|{t.Priority}|{t.Status}|{t.StoryPoints}SP|{t.EstimatedHours}h|Giao:{t.AssigneeName ?? "Chưa gán"}): {t.Title}");
                if (t.Subtasks != null)
                {
                    foreach (var s in t.Subtasks)
                    {
                        taskDigest.AppendLine($"  * [{s.Id}] ({s.Role}|{s.Priority}|{s.Status}|{s.EstimatedHours}h|{s.AssigneeName ?? "Chưa gán"}): {s.Title}");
                    }
                }
            }

            var shortTitles = string.Join("; ", proposal.Tasks.Take(4).Select(t => TruncateSingleLine(t.Title, 45)));
            var snapSummary = $"{proposal.Tasks.Count} việc ({proposal.TotalStoryPoints} SP): {shortTitles}";
            var snapKeywords = ExtractKeywords(
                $"{proposal.SprintGoal} {userPrompt} {string.Join(" ", proposal.Tasks.Select(t => $"{t.Id} {t.SourceBacklogIssueKey} {t.EpicTitle} {t.Title} {t.Priority} {t.AssigneeName}"))}");

            var existingSnap = proposal.IndexedMemories.LastOrDefault(m => m.Category == "proposal_snapshot");
            if (existingSnap != null && intent == "refine_proposal")
            {
                // Lưu riêng quyết định chỉnh sửa gần nhất trước khi cập nhật snapshot hiện hành
                AddAnchor(
                    proposal.IndexedMemories,
                    turnIndex,
                    category: "decision",
                    title: $"Điều chỉnh: {TruncateSingleLine(userPrompt ?? "Cập nhật kế hoạch", 80)}",
                    keywords: ExtractKeywords($"{userPrompt} {assistantReply}"),
                    summary: SanitizeSingleLine(assistantReply, 220),
                    detailContent: $"Yêu cầu: {userPrompt}\nKết quả: {assistantReply}");

                existingSnap.TurnIndex = turnIndex;
                existingSnap.Title = $"Kế hoạch hiện tại ({proposal.Tasks.Count} việc · {proposal.TotalStoryPoints} SP): {TruncateSingleLine(proposal.SprintGoal, 70)}";
                existingSnap.Keywords = snapKeywords;
                existingSnap.Summary = SanitizeSingleLine(snapSummary, 240);
                existingSnap.DetailContent = taskDigest.ToString().Trim();
                existingSnap.CreatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                AddAnchor(
                    proposal.IndexedMemories,
                    turnIndex,
                    category: "proposal_snapshot",
                    title: $"Kế hoạch ({proposal.Tasks.Count} việc · {proposal.TotalStoryPoints} SP): {TruncateSingleLine(proposal.SprintGoal, 70)}",
                    keywords: snapKeywords,
                    summary: SanitizeSingleLine(snapSummary, 240),
                    detailContent: taskDigest.ToString().Trim());
            }

            return;
        }

        // 3. Lập chỉ mục thông tin Q&A hoặc thao tác hệ thống (trừ lời chào đơn giản / từ chối ngoài luồng)
        if (intent is "project_qa" or "push_backlog" or "apply_sprint" or "export_excel")
        {
            var q = (userPrompt ?? string.Empty).Trim();
            if (q.Length < 4 || IsGreetingOrCapabilityQuestion(q.ToLowerInvariant()))
            {
                return;
            }

            var category = intent is "push_backlog" or "apply_sprint" ? "action_commit" : "qa_fact";
            AddAnchor(
                proposal.IndexedMemories,
                turnIndex,
                category: category,
                title: TruncateSingleLine(q, 90),
                keywords: ExtractKeywords($"{q} {assistantReply}"),
                summary: SanitizeSingleLine(assistantReply, 220),
                detailContent: $"Hỏi/Lệnh: {q}\nTrả lời: {(assistantReply.Length > 1800 ? assistantReply[..1800] + "..." : assistantReply)}");
        }
    }

    public static List<AiMemoryAnchorDto> SelectRelevantMemories(
        string? userQuery,
        string? docContent,
        IReadOnlyList<AiMemoryAnchorDto> anchors,
        int maxMatches = 3,
        IReadOnlyList<AiConversationTurnDto>? conversationHistory = null)
    {
        if (anchors == null || anchors.Count == 0)
        {
            return [];
        }

        var query = $"{userQuery} {docContent}".Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return anchors.TakeLast(1).ToList();
        }

        var explicitNums = MemoryAnchorRegex()
            .Matches(query)
            .Select(m => $"#mem-{m.Groups[1].Value}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queryTokens = TokenizeNormalized(query);
        var lowerQuery = query.ToLowerInvariant();
        var isFollowUp = IsConversationalFollowUp(lowerQuery) || queryTokens.Count <= 4;
        var contextTopicTokens = isFollowUp && conversationHistory != null && conversationHistory.Count > 0
            ? TokenizeNormalized(BuildRecentTopicHint(conversationHistory))
            : [];

        bool mentionsPlanOrEdit = MentionsProposalOrWorkItem(lowerQuery) || isFollowUp;
        var maxTurn = anchors.Max(a => a.TurnIndex);

        var scored = new List<(AiMemoryAnchorDto Anchor, int Score)>();
        foreach (var anchor in anchors)
        {
            int score = 0;
            if (explicitNums.Contains(anchor.AnchorKey))
            {
                score += 120;
            }

            if (mentionsPlanOrEdit && anchor.Category == "proposal_snapshot")
            {
                score += 22;
            }

            var keyNorm = NormalizeForMatch($"{anchor.Title} {anchor.Keywords}");
            var summaryNorm = NormalizeForMatch(anchor.Summary);
            var detailNorm = NormalizeForMatch(anchor.DetailContent);

            foreach (var tok in queryTokens)
            {
                if (keyNorm.Contains(tok, StringComparison.Ordinal)) score += 9;
                else if (summaryNorm.Contains(tok, StringComparison.Ordinal)) score += 6;
                else if (detailNorm.Contains(tok, StringComparison.Ordinal)) score += 3;
            }

            // Nếu là câu hỏi nối tiếp / đại từ ám chỉ ("nó", "phần trên", "chi tiết hơn"):
            // dùng từ khóa chủ đề của lượt thoại liền trước để neo đúng mảnh trí nhớ đang bàn
            if (score == 0 && contextTopicTokens.Count > 0)
            {
                foreach (var ctxTok in contextTopicTokens.Take(10))
                {
                    if (keyNorm.Contains(ctxTok, StringComparison.Ordinal)) score += 4;
                    else if (summaryNorm.Contains(ctxTok, StringComparison.Ordinal)) score += 2;
                }
            }

            // Ưu tiên nhẹ các mảnh trí nhớ gần nhất khi điểm tương đồng gần nhau
            if (score > 0 && anchor.TurnIndex == maxTurn)
            {
                score += 2;
            }

            if (score > 0)
            {
                scored.Add((anchor, score));
            }
        }

        if (scored.Count == 0 && isFollowUp)
        {
            return anchors.TakeLast(Math.Min(2, maxMatches)).ToList();
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Anchor.TurnIndex)
            .Take(maxMatches)
            .Select(x => x.Anchor)
            .ToList();
    }

    public static string FormatIndexedContextForPrompt(
        IReadOnlyList<AiConversationTurnDto> history,
        IReadOnlyList<AiMemoryAnchorDto> allMemories,
        IReadOnlyList<AiMemoryAnchorDto> recalledMemories,
        AiSprintProposalDto? existingProposal,
        bool includeProposalTaskDetails = true)
    {
        var sb = new StringBuilder();

        if (allMemories.Count > 0)
        {
            sb.AppendLine("### CHỈ MỤC TRÍ NHỚ ĐƯỢC ĐÁNH DẤU (#mem) CỦA PHIÊN:");
            foreach (var mem in allMemories.TakeLast(14))
            {
                sb.AppendLine($"- [{mem.AnchorKey} | {mem.Category}] {mem.Title} -> {mem.Summary}");
            }
        }

        if (recalledMemories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### CHI TIẾT TRÍ NHỚ ĐƯỢC TRỎ (MEMORY RECALL - DÙNG TRỰC TIẾP ĐỂ GIẢI ĐÁP & SUY LUẬN):");
            foreach (var mem in recalledMemories)
            {
                sb.AppendLine($"#### {mem.AnchorKey} ({mem.Title})");
                sb.AppendLine(mem.DetailContent);
            }
        }

        var priorTurns = history != null && history.Count > 1
            ? history.Take(history.Count - 1).ToList()
            : [];

        var activeDirectives = ExtractActiveSessionDirectives(priorTurns);
        if (activeDirectives.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### RÀNG BUỘC & THỎA THUẬN XUYÊN SUỐT PHIÊN TỪ NGƯỜI DÙNG:");
            foreach (var d in activeDirectives)
            {
                sb.AppendLine($"- {d}");
            }
        }

        if (priorTurns.Count > 0)
        {
            var window = priorTurns.TakeLast(8).ToList();
            sb.AppendLine();
            sb.AppendLine($"### LỊCH SỬ ĐOẠN HỘI THOẠI TRƯỚC ĐÓ ({window.Count} LƯỢT GẦN NHẤT):");
            for (int i = 0; i < window.Count; i++)
            {
                var turn = window[i];
                bool isVeryRecent = i >= window.Count - 4;
                int maxChars = isVeryRecent ? 1200 : 480;
                var meta = turn.Role == "assistant" && (!string.IsNullOrWhiteSpace(turn.Intent) || (turn.TaskCount ?? 0) > 0)
                    ? $" | {turn.Intent ?? "chat"}{(turn.TaskCount > 0 ? $" | {turn.TaskCount} việc" : string.Empty)}"
                    : string.Empty;
                var speaker = turn.Role == "user" ? "Người dùng (Tôi)" : $"Trợ Lý AI{meta}";
                var compactContent = turn.Content.Length > maxChars ? turn.Content[..maxChars] + "..." : turn.Content;
                sb.AppendLine($"- [{speaker}]: {compactContent}");
            }
        }

        if (includeProposalTaskDetails && existingProposal?.Tasks != null && existingProposal.Tasks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"### DANH SÁCH CÔNG VIỆC ĐANG LƯU TRONG PHIÊN HIỆN TẠI (Mục tiêu: {existingProposal.SprintGoal} | Đích đề xuất: {existingProposal.RecommendedTarget}):");
            var tasks = existingProposal.Tasks;
            for (int tIdx = 0; tIdx < tasks.Count; tIdx++)
            {
                var t = tasks[tIdx];
                bool includeRich = tIdx < 16;
                var backlogRef = !string.IsNullOrWhiteSpace(t.SourceBacklogIssueKey)
                    ? $" | Nguồn Backlog: {t.SourceBacklogIssueKey} ({t.SourceBacklogIssueId})"
                    : string.Empty;
                sb.AppendLine($"- [{t.Id}] ({t.Type} | {t.Priority} | {t.StoryPoints} SP | {t.EstimatedHours}h | {t.Status} | Epic: {t.EpicTitle ?? "Chung"} | Người xử lý: {t.AssigneeName ?? "Chưa gán"}{backlogRef}): {t.Title}");

                if (includeRich)
                {
                    if (!string.IsNullOrWhiteSpace(t.Description) && !t.Description.Equals(t.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"  Mô tả: {TruncateSingleLine(t.Description, 200)}");
                    }
                    if (t.AcceptanceCriteria != null && t.AcceptanceCriteria.Count > 0)
                    {
                        sb.AppendLine($"  Nghiệm thu: {string.Join("; ", t.AcceptanceCriteria.Take(3))}");
                    }
                    if (t.DocumentCitations != null && t.DocumentCitations.Count > 0)
                    {
                        sb.AppendLine($"  Trích dẫn: {string.Join(", ", t.DocumentCitations.Take(3))}");
                    }
                }

                if (t.Subtasks != null && t.Subtasks.Count > 0)
                {
                    foreach (var s in t.Subtasks)
                    {
                        sb.AppendLine($"  + [{s.Id}] ({s.Role} | {s.EstimatedHours}h | {s.Priority} | {s.Status} | {s.AssigneeName ?? "Chưa gán"}): {s.Title}");
                    }
                }
            }
        }

        return sb.ToString().Trim();
    }

    public static string BuildSessionTitle(AiSprintProposalDto proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.SprintGoal) &&
            proposal.SprintGoal.Length >= 6 &&
            !proposal.SprintGoal.StartsWith("Trao đổi", StringComparison.OrdinalIgnoreCase))
        {
            return TruncateSingleLine(proposal.SprintGoal, 140);
        }

        var firstUser = proposal.ConversationHistory?
            .FirstOrDefault(t => t.Role == "user" && !string.IsNullOrWhiteSpace(t.Content));
        if (firstUser != null)
        {
            return TruncateSingleLine(firstUser.Content, 120);
        }

        return "Cuộc hội thoại Trợ Lý AI";
    }

    private static void AddAnchor(
        List<AiMemoryAnchorDto> list,
        int turnIndex,
        string category,
        string title,
        string keywords,
        string summary,
        string detailContent)
    {
        var nextNum = list.Count + 1;
        while (list.Any(m => m.AnchorKey.Equals($"#mem-{nextNum}", StringComparison.OrdinalIgnoreCase)))
        {
            nextNum++;
        }

        list.Add(new AiMemoryAnchorDto
        {
            AnchorKey = $"#mem-{nextNum}",
            TurnIndex = turnIndex,
            Category = category,
            Title = TruncateSingleLine(title, 200),
            Keywords = TruncateSingleLine(keywords, 480),
            Summary = TruncateSingleLine(summary, 580),
            DetailContent = detailContent,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static bool IsGreetingOrCapabilityQuestion(string lower)
    {
        if (lower.Length > 120) return false;
        return lower is "hi" or "hello" or "hey" or "chào" or "xin chào" or "alo" ||
               lower.StartsWith("chào ") ||
               lower.StartsWith("xin chào") ||
               lower.Contains("bạn là ai") ||
               lower.Contains("bạn làm được gì") ||
               lower.Contains("bạn hỗ trợ") ||
               lower.Contains("giúp tôi gì") ||
               lower.Contains("hướng dẫn sử dụng");
    }

    private static bool ContainsProjectDataKeyword(string lower) =>
        MentionsSpecHub(lower) || MentionsBacklog(lower) || MentionsSprint(lower) ||
        MentionsTeam(lower) || MentionsConfig(lower);

    public static bool IsConversationalFollowUp(string lower)
    {
        if (string.IsNullOrWhiteSpace(lower)) return false;
        return lower.Contains("cái đó") || lower.Contains("cái này") ||
               lower.Contains("việc đó") || lower.Contains("việc này") ||
               lower.Contains("phần đó") || lower.Contains("phần này") ||
               lower.Contains("mục đó") || lower.Contains("mục này") ||
               lower.Contains("ý đó") || lower.Contains("ý này") ||
               lower.Contains("câu trên") || lower.Contains("phần trên") ||
               lower.Contains("ở trên") || lower.Contains("như trên") ||
               lower.Contains("vừa nãy") || lower.Contains("lúc nãy") ||
               lower.Contains("hồi nãy") || lower.Contains("vừa rồi") ||
               lower.Contains("như đã") || lower.Contains("vừa bàn") ||
               lower.Contains("như vừa") || lower.Contains("thứ hai") ||
               lower.Contains("thứ ba") || lower.Contains("đầu tiên") ||
               lower.Contains("cuối cùng") || lower.Contains("ý tôi là") ||
               lower.Contains("chi tiết hơn") || lower.Contains("rõ hơn") ||
               lower.Contains("kỹ hơn") || lower.Contains("cụ thể hơn") ||
               lower.Contains("ngắn gọn") || lower.Contains("tại sao") ||
               lower.Contains("vì sao") || lower.Contains("thế còn") ||
               lower.Contains("tiếp đi") || lower.Contains("nói tiếp") ||
               lower.Contains("giải thích thêm") || lower.Contains("bổ sung thêm") ||
               lower.Contains("#mem-");
    }

    public static bool MentionsProposalOrWorkItem(string lower) =>
        lower.Contains("task") || lower.Contains("story") || lower.Contains("subtask") ||
        lower.Contains("sub-task") || lower.Contains("bug") || lower.Contains("công việc") ||
        lower.Contains("đầu việc") || lower.Contains("đề xuất") || lower.Contains("kế hoạch") ||
        lower.Contains("sprint goal") || lower.Contains("sp") || lower.Contains("ưu tiên") ||
        lower.Contains("nghiệm thu") || lower.Contains("mô tả") || lower.Contains("trích dẫn") ||
        lower.Contains("phụ trách") || lower.Contains("gán cho") || lower.Contains("giờ công") ||
        lower.Contains("sửa") || lower.Contains("đổi") || lower.Contains("thêm") || lower.Contains("xóa");

    public static string BuildRecentTopicHint(IReadOnlyList<AiConversationTurnDto>? history)
    {
        if (history == null || history.Count == 0) return string.Empty;
        var prior = history.Where(t => !string.IsNullOrWhiteSpace(t.Content))
                           .TakeLast(4)
                           .Select(t => TruncateSingleLine(t.Content, 160));
        return string.Join(" | ", prior);
    }

    private static List<string> ExtractActiveSessionDirectives(IReadOnlyList<AiConversationTurnDto> priorTurns)
    {
        if (priorTurns == null || priorTurns.Count == 0) return [];
        var directives = new List<string>();
        foreach (var turn in priorTurns)
        {
            if (turn.Role != "user" || string.IsNullOrWhiteSpace(turn.Content) || turn.Content.Length > 320)
                continue;
            var lower = turn.Content.ToLowerInvariant();
            bool hasDirective = lower.Contains("chỉ giao") || lower.Contains("không giao") ||
                                lower.Contains("không vượt") || lower.Contains("tối đa") ||
                                lower.Contains("chỉ dùng") || lower.Contains("chỉ tạo") ||
                                lower.Contains("ưu tiên") || lower.Contains("ngắn gọn") ||
                                lower.Contains("tập trung") || lower.Contains("vào backlog") ||
                                lower.Contains("kho backlog") || lower.Contains("vào sprint") ||
                                lower.Contains("bắt buộc");
            if (hasDirective)
            {
                directives.Add(TruncateSingleLine(turn.Content, 180));
            }
        }
        return directives.Distinct(StringComparer.OrdinalIgnoreCase).TakeLast(5).ToList();
    }

    private static bool MentionsSpecHub(string text) =>
        text.Contains("spec") || text.Contains("đặc tả") || text.Contains("tài liệu") ||
        text.Contains("srs") || text.Contains("brd") || text.Contains("prd") ||
        text.Contains("kiến trúc") || text.Contains("nghiệp vụ") || text.Contains("chức năng") ||
        text.Contains("yêu cầu");

    private static bool MentionsBacklog(string text) =>
        text.Contains("backlog") || text.Contains("user story") || text.Contains("story") ||
        text.Contains("kho việc") || text.Contains("tồn đọng") || text.Contains("chưa vào sprint");

    private static bool MentionsSprint(string text) =>
        text.Contains("sprint") || text.Contains("vận tốc") || text.Contains("velocity") ||
        text.Contains("rollover") || text.Contains("quá hạn") || text.Contains("tiến độ") ||
        text.Contains("bug tồn") || text.Contains("bug đang");

    private static bool MentionsTeam(string text) =>
        text.Contains("thành viên") || text.Contains("nhân sự") || text.Contains("ai làm") ||
        text.Contains("đội ngũ") || text.Contains("dev") || text.Contains("qa") ||
        text.Contains("tester") || text.Contains("năng lực") || text.Contains("capacity") ||
        text.Contains("rảnh") || text.Contains("giờ làm");

    private static bool MentionsConfig(string text) =>
        text.Contains("workflow") || text.Contains("quy trình") || text.Contains("trạng thái") ||
        text.Contains("status") || text.Contains("ưu tiên") || text.Contains("priority") ||
        text.Contains("loại việc") || text.Contains("issue type") || text.Contains("nhãn") ||
        text.Contains("label") || text.Contains("cấu hình") || text.Contains("story point");

    private static string BuildSpecSearchQuery(string? userPrompt, string? docContent = null)
    {
        var raw = string.IsNullOrWhiteSpace(userPrompt) ? (docContent ?? string.Empty) : userPrompt;
        if (string.IsNullOrWhiteSpace(raw)) return "Sprint SRS BRD Requirements";
        var cleaned = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length > 160 ? cleaned[..160] : cleaned;
    }

    private static string ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in IssueKeyRegex().Matches(text))
        {
            if (seen.Add(m.Value)) results.Add(m.Value);
        }

        foreach (Match m in TokenRegex().Matches(text))
        {
            var tok = m.Value.Trim('-');
            if (tok.Length < 3 || StopWords.Contains(tok)) continue;
            if (seen.Add(tok))
            {
                results.Add(tok);
                if (results.Count >= 18) break;
            }
        }

        return string.Join(", ", results);
    }

    private static List<string> TokenizeNormalized(string text)
    {
        var tokens = new List<string>();
        foreach (Match m in TokenRegex().Matches(text))
        {
            var raw = m.Value.Trim('-');
            if (raw.Length < 2 || StopWords.Contains(raw)) continue;
            var norm = NormalizeForMatch(raw);
            if (norm.Length >= 2 && !tokens.Contains(norm))
            {
                tokens.Add(norm);
            }
        }
        return tokens;
    }

    private static string NormalizeForMatch(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch == 'đ' ? 'd' : ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string TruncateSingleLine(string text, int maxLen)
    {
        var clean = SanitizeSingleLine(text, maxLen);
        return clean;
    }

    private static string SanitizeSingleLine(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var single = Regex.Replace(text.Trim(), @"\s+", " ");
        return single.Length <= maxLen ? single : single[..Math.Max(0, maxLen - 3)] + "...";
    }
}
