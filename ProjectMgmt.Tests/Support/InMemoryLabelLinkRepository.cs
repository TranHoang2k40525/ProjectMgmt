using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

/// <summary>Fake ILabelLinkRepository; so tên nhãn không phân biệt hoa thường như collation MySQL.</summary>
public class InMemoryLabelLinkRepository : ILabelLinkRepository
{
    public List<Label> Labels { get; } = [];
    public List<IssueLabel> IssueLabels { get; } = [];
    public List<IssueLink> Links { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Label>> GetProjectLabelsAsync(Guid projectId)
    {
        IReadOnlyList<Label> result = Labels.Where(l => l.ProjectId == projectId).OrderBy(l => l.Name).ToList();
        return Task.FromResult(result);
    }

    public Task<Label?> GetLabelAsync(Guid labelId) =>
        Task.FromResult(Labels.SingleOrDefault(l => l.Id == labelId));

    public Task<bool> LabelNameExistsAsync(Guid projectId, string name, Guid? exceptLabelId) =>
        Task.FromResult(Labels.Any(l => l.ProjectId == projectId && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase) && l.Id != exceptLabelId));

    public Task AddLabelAsync(Label label)
    {
        Labels.Add(label);
        return Task.CompletedTask;
    }

    public void RemoveLabel(Label label)
    {
        Labels.Remove(label);
        IssueLabels.RemoveAll(link => link.LabelId == label.Id);
    }

    public Task<IReadOnlyList<Label>> GetIssueLabelsAsync(Guid issueId)
    {
        IReadOnlyList<Label> result = IssueLabels.Where(link => link.IssueId == issueId)
            .Join(Labels, link => link.LabelId, label => label.Id, (_, label) => label)
            .OrderBy(label => label.Name)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IssueLabel?> GetIssueLabelAsync(Guid issueId, Guid labelId) =>
        Task.FromResult(IssueLabels.SingleOrDefault(link => link.IssueId == issueId && link.LabelId == labelId));

    public Task AddIssueLabelAsync(IssueLabel issueLabel)
    {
        IssueLabels.Add(issueLabel);
        return Task.CompletedTask;
    }

    public void RemoveIssueLabel(IssueLabel issueLabel) => IssueLabels.Remove(issueLabel);

    public Task<IReadOnlyList<IssueLink>> GetOutgoingLinksAsync(Guid issueId)
    {
        IReadOnlyList<IssueLink> result = Links.Where(link => link.SourceIssueId == issueId).OrderBy(link => link.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<IssueLink?> GetLinkAsync(Guid linkId) =>
        Task.FromResult(Links.SingleOrDefault(link => link.Id == linkId));

    public Task<IssueLink?> FindLinkAsync(Guid sourceIssueId, Guid targetIssueId, string linkType) =>
        Task.FromResult(Links.SingleOrDefault(link => link.SourceIssueId == sourceIssueId && link.TargetIssueId == targetIssueId && link.LinkType == linkType));

    public Task AddLinkAsync(IssueLink link)
    {
        Links.Add(link);
        return Task.CompletedTask;
    }

    public void RemoveLink(IssueLink link) => Links.Remove(link);

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
