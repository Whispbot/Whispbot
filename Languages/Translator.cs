using Discord.Commands;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Extensions;

namespace Whispbot.Languages
{
    public static class Translator
    {
        public static readonly Dictionary<Language, Dictionary<string, string>> LanguageStrings = [];

        public static Dictionary<string, string>? GetLanguageStrings(Language language)
        {
            if (!LanguageStrings.TryGetValue(language, out Dictionary<string, string>? value))
            {
                value = Load.Language(language);
                if (value is not null) LanguageStrings[language] = value;
                else LanguageStrings[language] = [];
            }

            if (value is null || value.Count == 0) return null;
            return value;
        }

        public static Dictionary<string, string>? GetLanguageStringsOrDefault(Language language)
        {
            return GetLanguageStrings(language) ?? GetLanguageStrings(Language.EnglishUK);
        }

        private static readonly List<string> WarnedAboutStrings = [];
        private static void WarnAboutMissingString(string name, Language language, string[] args)
        {
            var key = $"{language}:{name}";
            if (!WarnedAboutStrings.Contains(key))
            {
                WarnedAboutStrings.Add(key);
                Log.Warning($"Missing translation for '{name}' ({args.Join(", ")}) in {language}");
            }
        }
        public static string Get(Language language, string name, params string[] args)
        {
            var lang = GetLanguageStringsOrDefault(language);
            if (lang is null) return name;

            if (!lang.TryGetValue(name, out string? value)) {
                WarnAboutMissingString(name, language, args);
                return name;
            }

            for (int i = 0; i < args.Length; i++)
            {
                value = value.Replace($"{{{i}}}", args[i]);
            }

            return value;
        }

        public static readonly Dictionary<Language, (string, string, string)> LanguageInfo = new()
        {
            { Language.EnglishUK, ("en-GB", "English, UK", "English UK") },
            { Language.EnglishUS, ("en-US", "English, US", "English US") },
            { Language.French, ("fr-FR", "French", "Français") },
            { Language.German, ("de", "German", "Deutsch") },
            { Language.Spanish, ("es-ES", "Spanish", "Español") },
            { Language.SpanishLatinAmerican, ("es-419", "Spanish, LATAM", "Español, LATAM") },
            { Language.Italian, ("it", "Italian", "Italiano") },
            { Language.Thai, ("th", "Thai", "ไทย") },
            { Language.Dutch, ("nl", "Dutch", "Nederlands") },
            { Language.Polish, ("pl", "Polish", "Polski") },
            { Language.Indonesian, ("id", "Indonesian", "Bahasa Indonesia") },
            { Language.Danish, ("da", "Danish", "Dansk") },
            { Language.Croatian, ("hr", "Croatian", "Hrvatski") },
            { Language.Lithuanian, ("lt", "Lithuanian", "Lietuviškai") },
            { Language.Hungarian, ("hu", "Hungarian", "Magyar") },
            { Language.Norwegian, ("no", "Norwegian", "Norsk") },
            { Language.PortugueseBrazilian, ("pt-BR", "Portuguese, Brazilian", "Português do Brasil") },
            { Language.RomanianRomania, ("ro", "Romanian, Romania", "Română") },
            { Language.Finnish, ("fi", "Finnish", "Suomi") },
            { Language.Swedish, ("sv-SE", "Swedish", "Svenska") },
            { Language.Vietnamese, ("vi", "Vietnamese", "Tiếng Việt") },
            { Language.Turkish, ("tr", "Turkish", "Türkçe") },
            { Language.Czech, ("cs", "Czech", "Čeština") },
            { Language.Greek, ("el", "Greek", "Ελληνικά") },
            { Language.Bulgarian, ("bg", "Bulgarian", "български") },
            { Language.Russian, ("ru", "Russian", "Pусский") },
            { Language.Ukrainian, ("uk", "Ukrainian", "Українська") },
            { Language.Hindi, ("hi", "Hindi", "हिन्दी") },
            { Language.ChineseChina, ("zh-CN", "Chinese, China", "中文") },
            { Language.ChineseTaiwan, ("zh-TW", "Chinese, Taiwan", "繁體中文") },
            { Language.Japanese, ("ja", "Japanese", "日本語") },
            { Language.Korean, ("ko", "Korean", "한국어") },
        };

    }
    public enum Language
    {
        EnglishUK,
        EnglishUS,
        French,
        German,
        Spanish,
        SpanishLatinAmerican,
        Italian,
        Thai,
        Dutch,
        Polish,
        Indonesian,
        Danish,
        Croatian,
        Lithuanian,
        Hungarian,
        Norwegian,
        PortugueseBrazilian,
        RomanianRomania,
        Finnish,
        Swedish,
        Vietnamese,
        Turkish,
        Czech,
        Greek,
        Bulgarian,
        Russian,
        Ukrainian,
        Hindi,
        ChineseChina,
        ChineseTaiwan,
        Japanese,
        Korean
    }
}
