using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Models
{
    public class VbeeConfig
    {
        public string Token { get; set; }
        public string Url { get; set; }
        public string AppId { get; set; }
        public string SpeedRate { get; set; }
        public string VoiceCode { get; set; }
        public string CallbackUrl { get; set; }
        public int SleepTime { get; set; }
        public int MaxThreads { get; set; }
    }
}
