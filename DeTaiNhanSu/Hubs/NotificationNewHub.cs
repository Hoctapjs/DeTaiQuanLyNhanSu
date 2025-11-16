using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DeTaiNhanSu.Hubs
{
    [Authorize]
    public class NotificationNewHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.User.IsInRole("HR") || Context.User.IsInRole("Admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "HR_Admins");
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }
}
