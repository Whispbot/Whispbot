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
    }
}
