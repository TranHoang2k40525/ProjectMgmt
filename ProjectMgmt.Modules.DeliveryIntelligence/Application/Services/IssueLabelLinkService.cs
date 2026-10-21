using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using FluentValidation;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class IssueLabelLinkService : IIssueLabelLinkService
{
    private readonly IIssueRepository _issues;
    private readonly ILabelLinkRepository _labels;
    private readonly IValidator<SaveLabelDto> _labelValidator;
    private readonly TimeProvider _clock;

    public IssueLabelLinkService(
        IIssueRepository issues,
        ILabelLinkRepository labels,
        IValidator<SaveLabelDto> labelValidator,
        TimeProvider clock)
    {
        _issues = issues;
        _labels = labels;
        _labelValidator = labelValidator;
        _clock = clock;
    }

    // ---------- Labels ----------

    public async Task<IReadOnlyList<LabelDto>> GetProjectLabelsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await _labels.GetProjectLabelsAsync(projectId, cancellationToken)).Select(ToDto).ToList();

    public async Task<Result<LabelDto>> CreateLabelAsync(Guid projectId, SaveLabelDto request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result<LabelDto>.Failure(invalid);
        }

        var name = request.Name.Trim();
        if (await _labels.LabelNameExistsAsync(projectId, name, null, cancellationToken))
        {
            return Result<LabelDto>.Failure(LabelLinkErrors.LabelDuplicate);
        }

        var label = new Label
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            ColorHex = (request.ColorHex ?? LabelRules.DefaultColor).ToUpperInvariant()
        };
        await _labels.AddLabelAsync(label, cancellationToken);
        await _labels.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(label));
    }

    public async Task<Result<LabelDto>> UpdateLabelAsync(Guid labelId, SaveLabelDto request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result<LabelDto>.Failure(invalid);
        }

        var label = await _labels.GetLabelAsync(labelId, cancellationToken);
        if (label is null)
        {
            return Result<LabelDto>.Failure(LabelLinkErrors.LabelNotFound);
        }

        var name = request.Name.Trim();
        if (await _labels.LabelNameExistsAsync(label.ProjectId, name, label.Id, cancellationToken))
        {
            return Result<LabelDto>.Failure(LabelLinkErrors.LabelDuplicate);
        }

        label.Name = name;
        label.ColorHex = (request.ColorHex ?? label.ColorHex).ToUpperInvariant();
        await _labels.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(label));
    }

    public async Task<Result> DeleteLabelAsync(Guid labelId, CancellationToken cancellationToken = default)
    {
        var label = await _labels.GetLabelAsync(labelId, cancellationToken);
        if (label is null)
        {
            return Result.Failure(LabelLinkErrors.LabelNotFound);
        }

        // FK_IssueLabel_Label ON DELETE CASCADE gỡ nhãn khỏi mọi Issue.
        _labels.RemoveLabel(label);
        await _labels.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> GetLabelProjectIdAsync(Guid labelId, CancellationToken cancellationToken = default)
    {
        var label = await _labels.GetLabelAsync(labelId, cancellationToken);
        return label is null ? Result<Guid>.Failure(LabelLinkErrors.LabelNotFound) : Result.Success(label.ProjectId);
    }

    public async Task<Result<IReadOnlyList<LabelDto>>> GetIssueLabelsAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<LabelDto>>.Failure(IssueErrors.NotFound);
        }

        return Result.Success(await IssueLabelsAsync(issueId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<LabelDto>>> AddLabelToIssueAsync(
        Guid issueId,
        Guid labelId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return Result<IReadOnlyList<LabelDto>>.Failure(IssueErrors.NotFound);
        }

        var label = await _labels.GetLabelAsync(labelId, cancellationToken);
        if (label is null || label.ProjectId != issue.ProjectId)
        {
            return Result<IReadOnlyList<LabelDto>>.Failure(LabelLinkErrors.LabelNotInProject);
        }

        if (await _labels.GetIssueLabelAsync(issueId, labelId, cancellationToken) is null)
        {
            if ((await _labels.GetIssueLabelsAsync(issueId, cancellationToken)).Count >= LabelLinkErrors.MaxLabelsPerIssue)
            {
                return Result<IReadOnlyList<LabelDto>>.Failure(LabelLinkErrors.TooManyLabels);
            }

            await _labels.AddIssueLabelAsync(new IssueLabel { IssueId = issueId, LabelId = labelId }, cancellationToken);
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.LabelAdded, Now(), "Label", newValue: label.Name),
                cancellationToken);
            await _labels.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await IssueLabelsAsync(issueId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<LabelDto>>> RemoveLabelFromIssueAsync(
        Guid issueId,
        Guid labelId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<LabelDto>>.Failure(IssueErrors.NotFound);
        }

        var link = await _labels.GetIssueLabelAsync(issueId, labelId, cancellationToken);
        if (link is not null)
        {
            var label = await _labels.GetLabelAsync(labelId, cancellationToken);
            _labels.RemoveIssueLabel(link);
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.LabelRemoved, Now(), "Label", oldValue: label?.Name),
                cancellationToken);
            await _labels.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await IssueLabelsAsync(issueId, cancellationToken));
    }

    // ---------- Links ----------

    public async Task<Result<IReadOnlyList<IssueLinkDto>>> GetLinksAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        if (await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken) is null)
        {
            return Result<IReadOnlyList<IssueLinkDto>>.Failure(IssueErrors.NotFound);
        }

        var links = await _labels.GetOutgoingLinksAsync(issueId, cancellationToken);
        var targets = (await _issues.GetByIdsAsync(links.Select(link => link.TargetIssueId), cancellationToken)).ToDictionary(issue => issue.Id);
        IReadOnlyList<IssueLinkDto> result = links
            .Where(link => targets.ContainsKey(link.TargetIssueId))
            .Select(link => ToDto(link, targets[link.TargetIssueId]))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<IssueLinkDto>> CreateLinkAsync(
        Guid issueId,
        CreateIssueLinkDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var linkType = IssueLinkTypes.Normalize(request.LinkType);
        if (linkType is null)
        {
            return Result<IssueLinkDto>.Failure(LabelLinkErrors.LinkTypeInvalid);
        }

        if (request.TargetIssueId == issueId)
        {
            return Result<IssueLinkDto>.Failure(LabelLinkErrors.LinkSelf);
        }

        var source = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (source is null)
        {
            return Result<IssueLinkDto>.Failure(IssueErrors.NotFound);
        }

        var target = await _issues.GetByIdAsync(request.TargetIssueId, cancellationToken: cancellationToken);
        if (target is null || target.ProjectId != source.ProjectId)
        {
            return Result<IssueLinkDto>.Failure(LabelLinkErrors.LinkTargetInvalid);
        }

        if (await _labels.FindLinkAsync(source.Id, target.Id, linkType, cancellationToken) is not null)
        {
            return Result<IssueLinkDto>.Failure(LabelLinkErrors.LinkDuplicate);
        }

        var now = Now();
        var link = new IssueLink { Id = Guid.NewGuid(), SourceIssueId = source.Id, TargetIssueId = target.Id, LinkType = linkType, CreatedBy = actorUserId, CreatedAt = now };
        var inverse = new IssueLink { Id = Guid.NewGuid(), SourceIssueId = target.Id, TargetIssueId = source.Id, LinkType = IssueLinkTypes.InverseOf(linkType), CreatedBy = actorUserId, CreatedAt = now };
        await _labels.AddLinkAsync(link, cancellationToken);
        if (await _labels.FindLinkAsync(inverse.SourceIssueId, inverse.TargetIssueId, inverse.LinkType, cancellationToken) is null)
        {
            await _labels.AddLinkAsync(inverse, cancellationToken);
        }

        await _issues.AddActivityAsync(
            IssueActivities.Create(source.Id, actorUserId, IssueActivities.LinkAdded, now, linkType, newValue: target.Id, detail: new { linkId = link.Id }),
            cancellationToken);
        await _labels.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(link, target));
    }

    public async Task<Result> DeleteLinkAsync(Guid linkId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var link = await _labels.GetLinkAsync(linkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(LabelLinkErrors.LinkNotFound);
        }

        _labels.RemoveLink(link);
        var inverse = await _labels.FindLinkAsync(link.TargetIssueId, link.SourceIssueId, IssueLinkTypes.InverseOf(link.LinkType), cancellationToken);
        if (inverse is not null)
        {
            _labels.RemoveLink(inverse);
        }

        await _issues.AddActivityAsync(
            IssueActivities.Create(link.SourceIssueId, actorUserId, IssueActivities.LinkRemoved, Now(), link.LinkType, oldValue: link.TargetIssueId),
            cancellationToken);
        await _labels.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> GetLinkSourceIssueIdAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var link = await _labels.GetLinkAsync(linkId, cancellationToken);
        return link is null ? Result<Guid>.Failure(LabelLinkErrors.LinkNotFound) : Result.Success(link.SourceIssueId);
    }

    private Error? Validate(SaveLabelDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _labelValidator.Validate(request);
        return validation.IsValid ? null : IssueTrackingService.ToError(validation);
    }

    private async Task<IReadOnlyList<LabelDto>> IssueLabelsAsync(Guid issueId, CancellationToken cancellationToken) =>
        (await _labels.GetIssueLabelsAsync(issueId, cancellationToken)).Select(ToDto).ToList();

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private static LabelDto ToDto(Label label) => new() { Id = label.Id, ProjectId = label.ProjectId, Name = label.Name, ColorHex = label.ColorHex };

    private static IssueLinkDto ToDto(IssueLink link, Issue target) => new()
    {
        Id = link.Id,
        LinkType = link.LinkType,
        TargetIssueId = target.Id,
        TargetIssueNumber = target.IssueNumber,
        TargetTitle = target.Title,
        TargetStatusId = target.StatusId,
        CreatedAt = link.CreatedAt
    };
}

public static class LabelLinkErrors
{
    public const int MaxLabelsPerIssue = 30;

    public static readonly Error LabelNotFound = Error.NotFound("LABEL_NOT_FOUND", "Không tìm thấy nhãn.");
    public static readonly Error LabelDuplicate = Error.Conflict("LABEL_DUPLICATE", "Project đã có nhãn cùng tên.");
    public static readonly Error LabelNotInProject = Error.Validation("LABEL_NOT_IN_PROJECT", "Nhãn không thuộc Project của Issue.");
    public static readonly Error TooManyLabels = Error.Validation("LABEL_LIMIT_REACHED", $"Mỗi Issue tối đa {MaxLabelsPerIssue} nhãn.");
    public static readonly Error LinkTypeInvalid = Error.Validation("ISSUE_LINK_TYPE_INVALID", "Loại liên kết phải là Blocks, IsBlockedBy, Relates, Duplicates hoặc IsDuplicatedBy.");
    public static readonly Error LinkSelf = Error.Validation("ISSUE_LINK_SELF", "Không thể liên kết Issue với chính nó.");
    public static readonly Error LinkTargetInvalid = Error.Validation("ISSUE_LINK_TARGET_INVALID", "Issue đích không tồn tại hoặc khác Project.");
    public static readonly Error LinkDuplicate = Error.Conflict("ISSUE_LINK_DUPLICATE", "Liên kết này đã tồn tại.");
    public static readonly Error LinkNotFound = Error.NotFound("ISSUE_LINK_NOT_FOUND", "Không tìm thấy liên kết.");
}
