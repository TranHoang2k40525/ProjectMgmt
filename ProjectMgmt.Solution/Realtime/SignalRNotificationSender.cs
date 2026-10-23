using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.SignalR;

namespace ProjectMgmt.Solution.Realtime;

public class SignalRNotificationSender : INotificationRealtimeSender
{
    private static readonly Action<ILogger, Guid, Exception?> LogRealtimeDeliveryFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2010, "NotificationRealtimeDeliveryFailed"),
            "Không thể đẩy thông báo realtime tới người dùng {UserId}.");

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationSender> _logger;

    public SignalRNotificationSender(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<bool> SendToUserAsync(Guid userId, NotificationDto notification)
    {
        try
        {
            await _hubContext.Clients
                .Group(NotificationHub.GroupName(userId))
                .SendAsync("notificationReceived", notification);
            return true;
        }
        catch (Exception exception)
        {
            LogRealtimeDeliveryFailure(_logger, userId, exception);
            return false;
        }
    }
}
