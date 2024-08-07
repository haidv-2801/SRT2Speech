using Microsoft.AspNetCore.SignalR;
using SRT2Speech.Socket.Methods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Socket.Hubs
{
    public class MessageHub : Hub
    {
        public async Task SendLog(string message)
        {
            await Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, message);
        }
    }
}
