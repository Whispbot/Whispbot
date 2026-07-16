using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using static Whispbot.Tools.Strings;

namespace Whispbot.Extensions
{
    public static class Tools
    {
        public static string Process(this string content, Language language = 0, Dictionary<string, string>? arguments = null, bool hasUserInput = false)
        {
            return Strings.Process(content, language, arguments, hasUserInput);
        }

        public static T? ProcessObj<T>(this T? obj, Language language = 0) where T : class
        {
            if (obj is null) return null;
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj).Process(language)!);
        }

        public static long ToLong(this string str)
        {
            return long.Parse(str);
        }
    }
}
