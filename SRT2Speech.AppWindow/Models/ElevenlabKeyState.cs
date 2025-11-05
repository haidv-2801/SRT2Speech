using System;
using System.Collections.Generic;
using SRT2Speech.AppWindow.Services;

namespace SRT2Speech.AppWindow.Models
{
    public class ElevenlabKeyState
    {
        public List<ApiKeyInfo> ApiKeys { get; set; } = new List<ApiKeyInfo>();
        public DateTime? LastSaved { get; set; }
        public KeySelectionAlgorithm KeySelectionAlgorithm { get; set; } = KeySelectionAlgorithm.RoundRobin;
    }
}