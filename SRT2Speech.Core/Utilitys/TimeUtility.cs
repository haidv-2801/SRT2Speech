using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Core.Utilitys
{
    public class TimeUtility
    {
        public static double ConvertTimestampToMs(TimeSpan time)
        {
            var dt = DateTime.ParseExact(FormatTimeSpan(time), "HH:mm:ss,fff", System.Globalization.CultureInfo.InvariantCulture);
            return dt.Hour * 3600000 + dt.Minute * 60000 + dt.Second * 1000 + dt.Microsecond / 1000;
        }
        public static string FormatTimeSpan(TimeSpan timeSpan) => timeSpan.ToString(@"hh\:mm\:ss\,fff");
        public static string FormatTimeSpanShort(TimeSpan timeSpan) => timeSpan.ToString(@"hh\:mm\:ss");
    }
}
