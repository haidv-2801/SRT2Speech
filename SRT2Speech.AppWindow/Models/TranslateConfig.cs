using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Models
{
    public class TranslateConfig
    {
        public string ApiKey { get; set; }
        public string Url { get; set; }
        public string Model { get; set; }
        public int MaxThreads { get; set; }
        public int SleepTime { get; set; }
        public int MaxItems { get; set; } = 50;
    }
}
