using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Socket.Hubs
{
    public interface IMessageHubClient
    {
        Task SendLog(string message);
    }
}
