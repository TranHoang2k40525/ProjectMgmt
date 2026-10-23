using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProjectMgmt.Solution.Realtime;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        await base.OnConnectedAsync();
    }

    public async Task JoinSprint(string sprintId)
    {
        if (!string.IsNullOrWhiteSpace(sprintId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SprintGroupName(sprintId));
        }
    }

    public async Task LeaveSprint(string sprintId)
    {
        if (!string.IsNullOrWhiteSpace(sprintId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SprintGroupName(sprintId));
        }
    }

    public static string GroupName(Guid userId)
    {
        return $"user:{userId:D}";
    }

    public static string SprintGroupName(string sprintId)
    {
        return $"sprint:{sprintId}";
    }
}
