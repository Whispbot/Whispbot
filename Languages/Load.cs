using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Whispbot.Languages;

namespace Whispbot.Languages
{
    public static class Load
    {
        public static Dictionary<string, string>? Language(Language language)
        {
            var langName = Translator.LanguageInfo.GetValueOrDefault(language, ("en-GB", "", "")).Item1;
            var fileName = $"{langName}.json";

            var asm = Assembly.GetExecutingAssembly();
            var resourceName = asm.GetManifestResourceNames()
                .SingleOrDefault(n => n.EndsWith($".Languages.Translations.{fileName}", StringComparison.Ordinal));

            if (resourceName is null) return null;

            using var stream = asm.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream!);
            var content = reader.ReadToEnd();

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
        }
    }
}
