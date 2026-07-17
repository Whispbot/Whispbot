using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Languages;
using Whispbot.Tools;

namespace Whispbot.Extensions
{
    public static class Tools
    {
        public static string Translate(this string content, Language language = 0, params string[] args)
        {
            return Translator.Get(language, content, args);
        }

        public static long ToLong(this string str)
        {
            return long.Parse(str);
        }
    }
}
