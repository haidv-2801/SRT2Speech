using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoApp.Utility
{
    public static class SRTUtility
    {
        public  static List<string> ExtractSrt(string input)
        {
            List<string> extractedText = new List<string>();
            string[] lines = input.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!int.TryParse(lines[i], out _) && !lines[i].Contains("-->"))
                {
                    extractedText.Add(lines[i].Trim());
                }
            }
            return extractedText;
        }
    }
}
