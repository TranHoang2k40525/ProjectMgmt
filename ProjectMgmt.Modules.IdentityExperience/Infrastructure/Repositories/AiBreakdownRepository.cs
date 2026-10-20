using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace IdentityExperience.Infrastructure.Repository;

public class AiBreakdownRepository : IAiBreakdownRepository
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogGenerationWriteFailure =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(1020, "AiGenerationWriteFailed"),
            "Không thể ghi AI generation {GenerationId} tại bước {Step}.");

    private readonly IdentityExperienceDbContext _context;
    private readonly ILogger<AiBreakdownRepository> _logger;

    public AiBreakdownRepository(
        IdentityExperienceDbContext context,
        ILogger<AiBreakdownRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<AiModel?> GetActiveModelAsync(string taskType)
    {
        return _context.AiModels
            .AsNoTracking()
            .Where(model => model.TaskType == taskType && model.IsActive)
            .OrderByDescending(model => model.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<AiPromptTemplate?> GetActivePromptAsync(string taskType)
    {
        return _context.AiPromptTemplates
            .AsNoTracking()
            .Where(prompt => prompt.TaskType == taskType && prompt.IsActive)
            .OrderByDescending(prompt => prompt.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AddGenerationAsync(AiGenerationLog generation)
    {
        try
        {
            await _context.AiGenerationLogs.AddAsync(generation);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception exception)
        {
            LogGenerationWriteFailure(_logger, generation.Id, "create", exception);
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> CompleteGenerationAsync(
        Guid generationId,
        string rawResponseJson,
        string parsedJson,
        List<AiSuggestedTask> suggestions,
        int latencyMs,
        DateTime completedAtUtc)
    {
        try
        {
            var generation = await _context.AiGenerationLogs
                .FirstOrDefaultAsync(candidate => candidate.Id == generationId);
            if (generation is null || generation.Status is not ("Pending" or "Processing"))
            {
                return false;
            }

            generation.RawResponseJson = rawResponseJson;
            generation.ParsedJson = parsedJson;
            generation.Status = "Completed";
            generation.LatencyMs = latencyMs;
            generation.CompletedAt = completedAtUtc;
            await _context.AiSuggestedTasks.AddRangeAsync(suggestions);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception exception)
        {
            LogGenerationWriteFailure(_logger, generationId, "complete", exception);
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task MarkGenerationFailedAsync(
        Guid generationId,
        string errorCode,
        string errorMessage,
        DateTime completedAtUtc)
    {
        try
        {
            var generation = await _context.AiGenerationLogs
                .FirstOrDefaultAsync(candidate => candidate.Id == generationId);
            if (generation is null)
            {
                return;
            }

            generation.Status = "Failed";
            generation.ErrorCode = errorCode;
            generation.ErrorMessage = errorMessage.Length <= 1000
                ? errorMessage
                : errorMessage[..1000];
            generation.CompletedAt = completedAtUtc;
            await _context.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            LogGenerationWriteFailure(_logger, generationId, "fail", exception);
            _context.ChangeTracker.Clear();
        }
    }

    public async Task<AiGenerationDetails?> GetGenerationAsync(Guid generationId)
    {
        var generation = await _context.AiGenerationLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == generationId);
        if (generation is null)
        {
            return null;
        }

        return new AiGenerationDetails
        {
            Generation = generation,
            Model = generation.ModelId.HasValue
                ? await _context.AiModels.AsNoTracking()
                    .FirstOrDefaultAsync(model => model.Id == generation.ModelId.Value)
                : null,
            PromptTemplate = generation.PromptTemplateId.HasValue
                ? await _context.AiPromptTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(prompt => prompt.Id == generation.PromptTemplateId.Value)
                : null,
            Suggestions = await _context.AiSuggestedTasks
                .AsNoTracking()
                .Where(suggestion => suggestion.AiGenerationLogId == generationId)
                .OrderBy(suggestion => suggestion.OrderIndex)
                .ToListAsync()
        };
    }

    public async Task<AiFeedbackWriteStatus> SaveFeedbackAsync(
        Guid generationId,
        Guid suggestedTaskId,
        Guid reviewedBy,
        string userAction,
        string? finalSummary,
        string? finalDescription,
        string? finalAcceptanceCriteria,
        decimal? finalEstimatePoints,
        decimal editDistanceRatio,
        string? rejectReason,
        DateTime reviewedAtUtc)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var generations = await _context.AiGenerationLogs
                .FromSqlInterpolated($"SELECT * FROM `AiGenerationLog` WHERE `Id` = {generationId} FOR UPDATE")
                .ToListAsync();
            var generation = generations.SingleOrDefault();
            if (generation is null)
            {
                await transaction.RollbackAsync();
                return AiFeedbackWriteStatus.GenerationNotFound;
            }

            if (generation.AppliedAt.HasValue)
            {
                await transaction.RollbackAsync();
                return AiFeedbackWriteStatus.GenerationAlreadyApplied;
            }

            var suggestions = await _context.AiSuggestedTasks
                .FromSqlInterpolated($"SELECT * FROM `AiSuggestedTask` WHERE `Id` = {suggestedTaskId} AND `AiGenerationLogId` = {generationId} FOR UPDATE")
                .ToListAsync();
            var suggestion = suggestions.SingleOrDefault();
            if (suggestion is null)
            {
                await transaction.RollbackAsync();
                return AiFeedbackWriteStatus.SuggestionNotFound;
            }

            suggestion.UserAction = userAction;
            suggestion.FinalSummary = finalSummary;
            suggestion.FinalDescription = finalDescription;
            suggestion.FinalAcceptanceCriteria = finalAcceptanceCriteria;
            suggestion.FinalEstimatePoints = finalEstimatePoints;
            suggestion.EditDistanceRatio = editDistanceRatio;
            suggestion.RejectReason = rejectReason;
            suggestion.ReviewedBy = reviewedBy;
            suggestion.ReviewedAt = reviewedAtUtc;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return AiFeedbackWriteStatus.Updated;
        });
    }

    public Task<List<AiPromptTemplate>> GetPromptTemplatesAsync(string? taskType)
    {
        var query = _context.AiPromptTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(taskType))
        {
            query = query.Where(prompt => prompt.TaskType == taskType);
        }

        return query
            .OrderBy(prompt => prompt.TaskType)
            .ThenBy(prompt => prompt.Code)
            .ThenByDescending(prompt => prompt.Version)
            .ToListAsync();
    }

    public async Task<AiPromptWriteResult> CreatePromptTemplateAsync(AiPromptTemplate promptTemplate)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                var versions = await _context.AiPromptTemplates
                    .FromSqlInterpolated($"SELECT * FROM `AiPromptTemplate` WHERE `Code` = {promptTemplate.Code} ORDER BY `Version` DESC FOR UPDATE")
                    .ToListAsync();
                promptTemplate.Version = (versions.FirstOrDefault()?.Version ?? 0) + 1;
                await _context.AiPromptTemplates.AddAsync(promptTemplate);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new AiPromptWriteResult
                {
                    Status = AiPromptWriteStatus.Created,
                    PromptTemplate = promptTemplate
                };
            });
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: 1062 })
        {
            _context.ChangeTracker.Clear();
            return new AiPromptWriteResult { Status = AiPromptWriteStatus.Conflict };
        }
    }

    public async Task<AiPromptWriteResult> ActivatePromptTemplateAsync(Guid promptId)
    {
        var targetInfo = await _context.AiPromptTemplates
            .AsNoTracking()
            .Where(prompt => prompt.Id == promptId)
            .Select(prompt => new { prompt.Id, prompt.TaskType })
            .FirstOrDefaultAsync();
        if (targetInfo is null)
        {
            return new AiPromptWriteResult { Status = AiPromptWriteStatus.NotFound };
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var taskPrompts = await _context.AiPromptTemplates
                .FromSqlInterpolated($"SELECT * FROM `AiPromptTemplate` WHERE `TaskType` = {targetInfo.TaskType} ORDER BY `Id` FOR UPDATE")
                .ToListAsync();
            var target = taskPrompts.SingleOrDefault(prompt => prompt.Id == promptId);
            if (target is null)
            {
                await transaction.RollbackAsync();
                return new AiPromptWriteResult { Status = AiPromptWriteStatus.NotFound };
            }

            foreach (var prompt in taskPrompts.Where(prompt => prompt.IsActive))
            {
                prompt.IsActive = false;
            }

            target.IsActive = true;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new AiPromptWriteResult
            {
                Status = AiPromptWriteStatus.Activated,
                PromptTemplate = target
            };
        });
    }
}
