using SRT2Speech.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public static List<SubtitleEntry> ParseSubtitleString(string subtitleString)
        {
            var entries = new List<SubtitleEntry>();
            var lines = subtitleString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i += 3)
            {
                var index = int.Parse(lines[i]);
                var timecodes = Regex.Match(lines[i + 1], @"(\d\d):(\d\d):(\d\d),(\d\d\d) --> (\d\d):(\d\d):(\d\d),(\d\d\d)");
                var startHours = int.Parse(timecodes.Groups[1].Value);
                var startMinutes = int.Parse(timecodes.Groups[2].Value);
                var startSeconds = int.Parse(timecodes.Groups[3].Value);
                var startMilliseconds = int.Parse(timecodes.Groups[4].Value);
                var endHours = int.Parse(timecodes.Groups[5].Value);
                var endMinutes = int.Parse(timecodes.Groups[6].Value);
                var endSeconds = int.Parse(timecodes.Groups[7].Value);
                var endMilliseconds = int.Parse(timecodes.Groups[8].Value);

                var entry = new SubtitleEntry
                {
                    Index = index,
                    StartTime = new TimeSpan(0, startHours, startMinutes, startSeconds, startMilliseconds),
                    EndTime = new TimeSpan(0, endHours, endMinutes, endSeconds, endMilliseconds),
                    Text = lines[i + 2]
                };

                entries.Add(entry);
            }

            return entries;
        }
    }
}
