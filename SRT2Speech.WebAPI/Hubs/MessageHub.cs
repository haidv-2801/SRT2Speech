using Microsoft.AspNetCore.SignalR;

namespace SRT2Speech.WebAPI.Hubs
{
    public class MessageHub : Hub
    {
        public async Task SendMessage(string message) 
            => await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
