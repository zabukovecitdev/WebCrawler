using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SamoBot.Api.Hubs;

[Authorize]
public class CrawlJobHub : Hub
{
    public Task JoinJob(int crawlJobId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, JobGroupName(crawlJobId));

    public Task LeaveJob(int crawlJobId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, JobGroupName(crawlJobId));

    public static string JobGroupName(int crawlJobId) => $"crawlJob:{crawlJobId}";
}
