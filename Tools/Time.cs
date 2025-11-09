using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools
{
    public static partial class Time
    {
        public static Dictionary<double, List<string>> TimeValues = new()
        {
            { 1, ["ms", "millisecond", "milliseconds","milli", "millisec", "millisecs", "mili", "milisecs", "milisec", "miliseconds"] },
            { 1000, ["s", "second", "seconds", "sec", "secs", "secodns"] },
            { 60_000, ["m", "minute", "minutes", "mins", "min"] },
            { 3_600_000, ["h", "hour", "hours"] },
            { 86_400_000, ["d", "day", "days"] },
            { 604_800_000, ["w", "week", "weeks", "wk", "wks"] },
            { 2_592_000_000, ["mo", "month", "months", "mos"] },
            { 31_471_200_000, ["yr", "year", "years", "y", "yrs"] }
        };

        /// <summary>
        /// Converts a string into a length of time.<br/><br/>
        /// E.G. 1 minute, 9 seconds -> 69,000
        /// </summary>
        /// <param name="str">The string to convert into milliseconds.</param>
        /// <returns>[double] The number of milliseconds relating to the string given.</returns>
        public static double ConvertStringToMilliseconds(string str)
        {
            str = str.Replace(" ", "");
            str = str.Replace(",", "");
            str = str.Replace(" and ", "");

            List<string> values = [""];

            while (str.Length > 0)
            {
                char thisChar = str.First();
                char? nextChar = str.Length > 1 ? str[1] : null;
                values[^1] += thisChar;
                if (!int.TryParse($"{thisChar}", out int _) && nextChar is not null && int.TryParse($"{nextChar}", out int _))
                {
                    values.Add("");
                }
                str = str[1..];
            }

            double Length = 0;

            foreach (string value in values)
            {
                string temp = value;
                string amount = "";
                string suffix = "";
                while (temp.Length > 0)
                {
                    char thisChar = temp.First();
                    if (int.TryParse($"{thisChar}", out int _))
                    {
                        amount += thisChar;
                    }
                    else
                    {
                        suffix += thisChar;
                    }
                    temp = temp[1..];
                }

                if (suffix != "")
                {
                    KeyValuePair<double, List<string>>? TimeValue = TimeValues.Where(kvp => kvp.Value.Contains(suffix.ToLower())).FirstOrDefault();
                    if (TimeValue is not null)
                    {
                        Length += (TimeValue?.Key ?? 0) * double.Parse(amount);
                    }
                }
            }

            return Length;
        }

        private static readonly List<string> TimeParserIgnoreWords = ["and", "for"];

        /// <summary>
        /// Converts a string into a length of time and returns the remaining part of the string that was not used.<br/><br/>
        /// E.G. "1 minute, 9 seconds for test" -> returns: 69,000, remaining: "for test"
        /// </summary>
        /// <param name="str">The string to convert into milliseconds.</param>
        /// <param name="remaining">[Out] The remaining section of string left over.</param>
        /// <returns>[double] The number of milliseconds relating to the string given.</returns>
        public static double ConvertMessageToMilliseconds(string str, out string remaining)
        {
            if (str.Trim() == "")
            {
                remaining = "";
                return 0;
            }
            string toUse = "";
            remaining = "";
            string origional = str;
            str = str.Replace(",", "");

            int i = 0;
            foreach (string st in str.Split(" "))
            {
                if (TimeParserIgnoreWords.Contains(st.ToLower()))
                {
                    i++;
                    continue;
                }
                if (int.TryParse($"{st[0]}", out int _) || TimeValues.Any(t => t.Value.Contains(st.ToLower())))
                {
                    toUse += " " + st;
                }
                else
                {
                    if (i == 0)
                    {
                        remaining = origional;
                    }
                    else
                    {
                        remaining = string.Join(" ", origional.Split(" ")[i..]);
                    }
                    break;
                }
                i++;
            }

            return ConvertStringToMilliseconds(toUse);
        }

        /// <summary>
        /// Convert a number of milliseconds into a readable format.<br/><br/>E.G. 69000 -> 1 minute, 9 seconds
        /// </summary>
        /// <param name="Length">The number of milliseconds to convert.</param>
        /// <param name="Seperator">The seperator that should be put between the different lengths of time.<br/><br/>Default: ", "</param>
        /// <param name="Small">Should the format returned be in small mode?<br/><br/>E.G. 69000<br/>true -> 1m, 9s<br/>false -> 1 minute, 9 seconds</param>
        /// <returns>[string] A string in the specified format.</returns>
        public static string ConvertMillisecondsToString(double Length, string Seperator = ", ", bool Small = false, double RoundTo = 1000)
        {
            if (Length <= 0)
            {
                return "0 seconds";
            }

            Length = Math.Ceiling(Length / RoundTo) * RoundTo;

            List<string> strings = [];

            while (Length > 0)
            {
                KeyValuePair<double, List<string>>? Biggest = null;
                foreach (KeyValuePair<double, List<string>> kvp in TimeValues)
                {
                    if (Biggest is null)
                    {
                        if (Length >= kvp.Key)
                        {
                            Biggest = kvp;
                        }
                    }
                    else
                    {
                        if (kvp.Key > Biggest?.Key && kvp.Key <= Length)
                        {
                            Biggest = kvp;
                        }
                    }
                }
                if (Biggest is null) break;

                double ThisLength = Math.Floor(Length / (Biggest?.Key ?? 1));
                strings.Add($"{ThisLength}{(Small ? Biggest?.Value[0] : $" {(ThisLength > 1 ? Biggest?.Value[2] : Biggest?.Value[1])}")}");
                Length -= ThisLength * (Biggest?.Key ?? 1);
            }
            return string.Join(Seperator, strings);
        }

        public static string ConvertMillisecondsToRelativeString(double ms, bool fromunix = false, string splitter = ", ", bool small = false, double roundto = 1)
        {
            if (fromunix)
            {
                ms -= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (Math.Abs(ms) < 1000) return "just now";
            if (ms < 0) return $"{ConvertMillisecondsToString(-ms, splitter, small, roundto)} ago";
            if (ms > 0) return $"in {ConvertMillisecondsToString(ms, splitter, small, roundto)}";
            return "just now";
        }
    }
}
