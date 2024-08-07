using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Models
{
    internal class FptConfig
    {
        public string Url { get; set; }
        public string ApiKey { get; set; }
        public string Speed { get; set; }
        public string Voice { get; set; }
        public string CallbackUrl { get; set; }
        public int SleepTime { get; set; }
        public int MaxThreads { get; set; }

    }
}