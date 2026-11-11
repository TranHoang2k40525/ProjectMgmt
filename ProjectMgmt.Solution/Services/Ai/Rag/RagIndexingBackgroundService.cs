namespace ProjectMgmt.Solution.Services.Ai.Rag;

using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.Extensions.Options;

/// <summary>Định kỳ đối chiếu Spec Hub của mọi dự án với chỉ mục RAG (chu kỳ Rag:IndexIntervalSeconds, 0 = tắt).</summary>
public sealed class RagIndexingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RagOptions> options,
    ILogger<RagIndexingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsSearchReady || settings.IndexIntervalSeconds <= 0)
        {
            logger.LogInformation("[RAG] Dịch vụ đánh chỉ mục nền không chạy (Rag:Enabled, Rag:Embedding hoặc Rag:IndexIntervalSeconds chưa cấu hình).");
            return;
        }

        logger.LogInformation("[RAG] Đánh chỉ mục Spec Hub mỗi {Seconds} giây bằng {Provider}/{Model}.",
            settings.IndexIntervalSeconds, settings.Embedding.Provider, settings.Embedding.Model);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IndexIntervalSeconds));
            do
            {
                await SyncAllAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SyncAllAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var indexer = scope.ServiceProvider.GetRequiredService<SpecDocumentIndexer>();
            foreach (var projectId in await indexer.ProjectsWithDocumentsAsync(ct))
            {
                var result = await indexer.SyncProjectAsync(projectId, ct);
                // Lỗi API (key, hạn mức) sẽ lặp lại ở mọi dự án: chờ lượt sau.
                if (result.Errors.Count > 0 && result.Indexed == 0) break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[RAG] Lượt đánh chỉ mục nền thất bại.");
        }
    }
}
