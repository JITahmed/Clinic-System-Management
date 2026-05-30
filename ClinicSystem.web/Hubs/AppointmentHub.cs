using Microsoft.AspNetCore.SignalR;

namespace ClinicSystem.web.Hubs
{
    /// signalR hub to broadcast appointment status updates
    public class AppointmentHub : Hub
    {
        // clients connect here automatically when the liveboard page is loaded
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }
    }
}
