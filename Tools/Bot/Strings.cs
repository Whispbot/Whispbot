using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;

namespace Whispbot.Tools
{
    public static class Strings
    {
        public static Dictionary<string, Emote> Emojis = [];
        public static Dictionary<Language, Dictionary<string, string>> LanguageStrings = [];

        private static readonly HttpClient _client = new();

        public static string Process(string content, Language language = 0, Dictionary<string, string>? arguments = null, bool hasUserInput = false)
        {
            MatchCollection matches = Regex.Matches(content, @"\{((emoji|string|dt)(?:[^{}]|\{[^{}]*\})*)\}");
            Dictionary<string, string>? thisLanguage = LanguageStrings.GetValueOrDefault(language);
            Dictionary<string, string>? defaultLanguage = LanguageStrings.GetValueOrDefault(Language.EnglishUK);

            arguments ??= [];

            List<string> missingStrings = [];

            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string type = match.Groups[2].Value.ToLower();
                if (type == "emoji")
                {
                    string emojiName = key.Replace("emoji.", "").ToLower();
                    Emote? emoji = Emojis.GetValueOrDefault(emojiName);
                    if (emoji is not null)
                    {
                        content = content.Replace(match.Value, emoji.ToString());
                    }
                }
                else if (type == "dt")
                {
                    double s = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    content = content.Replace(match.Value, $"<t:{s}:d> <t:{s}:T>");
                }
                else if (type == "string")
                {
                    string languageKey = key.ReplaceFirst("string.", "");

                    string[] split = languageKey.Split(':');
                    languageKey = split[0].ToLower();
                    if (split.Length > 1)
                    {
                        var argMatches = Regex.Matches(split[1], @"([^=,\s]+)=((?:(?!,[^=,\s]+=).)*)");
                        foreach (Match m in argMatches)
                        {
                            var k = m.Groups[1].Value;
                            var v = m.Groups[2].Value;
                            arguments[k] = v;
                        }
                    }

                    string? value = thisLanguage?.GetValueOrDefault(languageKey) ?? defaultLanguage?.GetValueOrDefault(languageKey);
                    if (value is not null)
                    {
                        var args = Regex.Matches(value, @"\{([^{}]+)\}");
                        foreach (Match arg in args)
                        {
                            string? argReplace = arguments.GetValueOrDefault(arg.Groups[1].Value);
                            if (argReplace is not null)
                            {
                                value = value.Replace(arg.Value, argReplace.Process(language, arguments, hasUserInput));
                            }
                        }

                        content = content.Replace(match.Value, value);
                    }
                    else
                    {
                        missingStrings.Add(languageKey);
                    }
                }
            }

            if (!hasUserInput && missingStrings.Count > 0)
            {
                Task.Run(
                    () =>
                    {
                        var distinctMissingStrings = missingStrings.Distinct().ToList();
                        var args = new List<object> { (int)language };
                        args.AddRange(distinctMissingStrings);

                        Postgres.Execute(
                            "INSERT INTO languages (key, language) VALUES " +
                            string.Join(", ", missingStrings.Distinct().Select((_, i) => $"(@{i + 2}, @1)")) +
                            " ON CONFLICT (key, language) DO NOTHING;",
                            args
                        );
                    }
                );
            }

            return content.Replace("\\n", "\n");
        }

        // Source - https://stackoverflow.com/a/141076
        // Posted by VVS, modified by community. See post 'Timeline' for change history
        // Retrieved 2026-02-15, License - CC BY-SA 2.5
        private static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public static async Task GetEmojis()
        {
            string? token = Environment.GetEnvironmentVariable("CLIENT_TOKEN");
            if (token is null) return;

            var client = new DiscordRestClient();
            await client.LoginAsync(TokenType.Bot, token);

            var emotes = await client.GetApplicationEmotesAsync();

            Emojis = emotes.ToDictionary(e => e.Name.ToLower(), e => e);
        }

        public static readonly Emoji FALLBACK_EMOJI = new("\u274c"); // ❌
        public static IEmote GetEmoji(string name)
        {
            var emoji = Emojis.GetValueOrDefault(name.ToLower());

            if (emoji is not null) return emoji;
            else return FALLBACK_EMOJI; // ❌ if not found
        }

        public static void GetLanguages()
        {
            while (!Postgres.IsConnected()) Thread.Sleep(100);
            List<DBLanguage>? languages = Postgres.Select<DBLanguage>($"SELECT * FROM languages WHERE content IS NOT NULL;");
            if (languages is null || languages.Count == 0) return;
            LanguageStrings = languages.GroupBy(l => l.language).ToDictionary(g => g.Key, g => g.ToDictionary(l => l.key, l => l.content));
        }

        public class DBLanguage
        {
            public string key = "";
            public Language language;
            public string content = "";
        }

        public struct EmojisResponse
        {
            public List<Emoji> items;
        }

        public static Dictionary<Language, (string, string, string)> Languages = new()
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
}
