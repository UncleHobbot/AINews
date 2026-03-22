using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AINews.Api.Hubs;

[Authorize]
public class ScanProgressHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "scan-watchers");
        await base.OnConnectedAsync();
    }
}
