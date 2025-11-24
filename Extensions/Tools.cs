using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using static Whispbot.Tools.Strings;

namespace Whispbot.Extensions
{
    public static class Tools
    {
        public static string Process(this string content, Language language = 0, Dictionary<string, string>? arguments = null, bool hasUserInput = false)
        {
            return Strings.Process(content, language, arguments, hasUserInput);
        }

        public static MessageBuilder Process(this MessageBuilder message, Language language = 0, Dictionary<string, string>? arguments = null, bool hasUserInput = false)
        {
            string value = JsonConvert.SerializeObject(message);
            value = Strings.Process(value, language, arguments, hasUserInput);
            return JsonConvert.DeserializeObject<MessageBuilder>(value) ?? new MessageBuilder { content = "Failed to parse language data :(" };
        }
    }
}
