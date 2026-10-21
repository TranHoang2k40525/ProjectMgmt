using DeliveryIntelligence.Application.IServices;
using IdentityExperience.Application.IServices;

namespace ProjectMgmt.Solution.Services;

/// <summary>Đọc danh mục kỹ năng của IdentityExperience cho DeliveryIntelligence; cache trong phạm vi một request.</summary>
public class SkillCatalogAdapter : ISkillCatalogReader
{
    private readonly ISkillServices _skills;
    private IReadOnlyList<SkillCatalogItem>? _cache;

    public SkillCatalogAdapter(ISkillServices skills)
    {
        _skills = skills;
    }

    public async Task<IReadOnlyList<SkillCatalogItem>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var catalog = await _skills.GetCatalogAsync(null, null);
        _cache = (catalog.Items ?? [])
            .Where(item => item.SkillId.HasValue)
            .Select(item => new SkillCatalogItem(item.SkillId!.Value, item.Name ?? string.Empty, item.Category, item.IsActive ?? true))
            .ToList();
        return _cache;
    }
}
