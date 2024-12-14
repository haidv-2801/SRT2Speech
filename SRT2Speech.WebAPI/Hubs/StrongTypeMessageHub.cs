using Microsoft.AspNetCore.SignalR;

namespace SRT2Speech.WebAPI.Hubs
{
    public class StrongTypeMessageHub : Hub<IMessageHubClient>
    {
        public async Task SendMessage(string message)
        {
            await Clients.All.SendMessage(message);
        }
    }
}
