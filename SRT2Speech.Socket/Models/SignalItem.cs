using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Socket.Models
{
    public class SignalItem
    {
        public string Id { get; set; }
        public object Data { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
