﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow.Models
{
    public class GoogleConfig
    {
        public string ApiKey { get; set; }
        public string Url { get; set; }
        public string AudioEncoding { get; set; }
        public string VoiceName { get; set; }
        public double SpeedRate { get; set; }
        public int SleepTime { get; set; }
        public int MaxThreads { get; set; }
    }
}
