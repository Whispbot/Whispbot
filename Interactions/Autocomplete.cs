using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Newtonsoft.Json;
using OpenAI.Realtime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;

namespace Whispbot.Interactions
{
    public static partial class Autocomplete
    {
        public static AutocompleteOption? GetOption(IAutocompleteInteractionData data, List<string> names, out List<string> outNames)
        {
            outNames = names;

            foreach (var option in data.Options)
            {
                if ((option.Type == ApplicationCommandOptionType.SubCommand || option.Type == ApplicationCommandOptionType.SubCommandGroup) && option is IAutocompleteInteractionData optionData)
                {
                    outNames.Add(option.Name);
                    var found = GetOption(optionData, outNames, out var finalNames);
                    outNames = finalNames;
                    if (found is not null) return found;
                }
                else if (option.Focused == true)
                {
                    return option;
                }
            }

            return null;
        }

        public static Command? GetCommand(List<string> names) =>
            CommandManager.commands?.Find(cmd =>
                cmd.SlashCommand is not null
                && cmd.SlashCommand.Count == names.Count
                && cmd.SlashCommand.SequenceEqual(names, StringComparer.OrdinalIgnoreCase));

        public static SlashCommandArg? GetArg(Command command, string name) => command.Arguments?.Find(arg => arg.name == name);
        public static CommandArgType? GetArgType(Command command, string name) => GetArg(command, name)?.type;

        public static async Task Handle(SocketInteraction interaction)
        {
            if (interaction.GuildId is null) return;
            if (interaction is not SocketAutocompleteInteraction autocomplete) return;

            var data = autocomplete.Data;
            var option = GetOption(data, [data.CommandName], out var names);
            if (option is null) return;

            var command = GetCommand(names);
            if (command is null) return;

            var type = GetArgType(command, option.Name);
            if (type is null) return;

            var value = option.Value;

            var config = await WhispCache.GuildConfig.Get(interaction.GuildId.Value);

            if (functions.TryGetValue(type.Value, out var func))
            {
                var choices = await func(autocomplete, value);
                await autocomplete.RespondAsync(choices.Take(25).ProcessObj((Strings.Language)(config?.default_language ?? 0)));
            }
            else
            {
                await autocomplete.RespondAsync();
            }
        }

        public static readonly Dictionary<CommandArgType, Func<SocketAutocompleteInteraction, dynamic, Task<IEnumerable<AutocompleteResult>>>> functions = new()
        {
            { CommandArgType.ShiftType,    ShiftType               },
            { CommandArgType.RobloxType,   RobloxModerationType    },
            { CommandArgType.ERLCServer,   ERLCServer              },
            { CommandArgType.Case,         DiscordCase             },
            { CommandArgType.RobloxCase,   RobloxCase              },
            { CommandArgType.RobloxUser,   RobloxUser              },
            { CommandArgType.Duration,     Duration                },
        };

        public static async Task<IEnumerable<AutocompleteResult>> ShiftType(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            var types = await WhispCache.ShiftTypes.Get(interaction.GuildId!.Value);
            var searchedTypes = String.IsNullOrWhiteSpace(text)
                ? types
                : types?.FindAll(
                    t =>
                    t.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || t.triggers.Any(tr => tr.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                )) ?? [];

            return searchedTypes?.Select(t => new AutocompleteResult { Name = t.name, Value = t.id.ToString() }) ?? [];
        }

        public static async Task<IEnumerable<AutocompleteResult>> RobloxModerationType(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            var types = await WhispCache.RobloxModerationTypes.Get(interaction.GuildId!.Value);
            var searchedTypes = String.IsNullOrWhiteSpace(text)
                ? types
                : types?.FindAll(
                    t =>
                    t.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || t.triggers.Any(tr => tr.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                )) ?? [];

            return searchedTypes?.Select(t => new AutocompleteResult { Name = t.name, Value = t.id.ToString() }) ?? [];
        }

        public static async Task<IEnumerable<AutocompleteResult>> ERLCServer(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            var servers = await WhispCache.ERLCServerConfigs.Get(interaction.GuildId!.Value);
            var searchedServers = String.IsNullOrWhiteSpace(text)
                ? servers
                : servers?.FindAll(
                    s =>
                    s.name is not null && s.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                ) ?? [];

            return searchedServers?.Select(s => new AutocompleteResult { Name = s.name ?? $"Server {s.id}", Value = s.id.ToString() }) ?? [];
        }

        public static async Task<IEnumerable<AutocompleteResult>> DiscordCase(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            if (text.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                var lastCase = Postgres.SelectFirst<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY created_at DESC LIMIT 1",
                    [interaction.GuildId!.Value, interaction.User.Id]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteResult { Name = $"Case #{lastCase.case_id} - '{lastCase.reason[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason.Length > 30 ? "..." : "")}'", Value = lastCase.case_id.ToString() }];
                }
                else return [];
            }
            else if (text.Equals("slast", StringComparison.OrdinalIgnoreCase))
            {
                var lastCase = Postgres.SelectFirst<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 ORDER BY created_at DESC LIMIT 1",
                    [interaction.GuildId!.Value]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteResult { Name = $"Case #{lastCase.case_id} - '{lastCase.reason[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason.Length > 30 ? "..." : "")}'", Value = lastCase.case_id.ToString() }];
                }
                else return [];
            }
            else if (long.TryParse(text, out var possibleId))
            {
                var cases = Postgres.Select<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND (moderator_id = @2 OR target_id = @2) ORDER BY created_at DESC LIMIT 25",
                    [interaction.GuildId!.Value, possibleId]
                );

                if (cases is not null)
                {
                    return cases.Select(
                        c => new AutocompleteResult
                        {
                            Name = $"Case #{c.case_id} - '{c.reason[..Math.Min(30, c.reason.Length)]}{(c.reason.Length > 30 ? "..." : "")}'",
                            Value = c.case_id.ToString()
                        }
                    );
                }
                else return [];
            }
            else
            {
                var cases = Postgres.Select<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND reason ILIKE @2 ORDER BY created_at DESC LIMIT 25",
                    [interaction.GuildId!.Value, $"%{text}%"]
                );

                var lastCases = String.IsNullOrWhiteSpace(text) ? new List<AutocompleteResult>() {
                    new() { Name = "My Last Case", Value = "last" },
                    new() { Name = "Server's Last Case", Value = "slast" }
                } : [];

                if (cases is not null)
                {
                    return [..lastCases, ..cases.Select(
                        c => new AutocompleteResult
                        {
                            Name = $"Case #{c.case_id} - '{c.reason[..Math.Min(30, c.reason.Length)]}{(c.reason.Length > 30 ? "..." : "")}'",
                            Value = c.case_id.ToString()
                        }
                    )];
                }
                else return lastCases;
            }
        }

        public static async Task<IEnumerable<AutocompleteResult>> RobloxCase(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            if (text.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                var lastCase = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 AND is_deleted = FALSE ORDER BY created_at DESC LIMIT 1",
                    [interaction.GuildId!.Value, interaction.User.Id]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteResult { Name = $"Case #{lastCase.@case} - '{lastCase.reason?[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason?.Length > 30 ? "..." : "")}'", Value = lastCase.@case.ToString() }];
                }
                else return [];
            }
            else if (text.Equals("slast", StringComparison.OrdinalIgnoreCase))
            {
                var lastCase = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND is_deleted = FALSE ORDER BY created_at DESC LIMIT 1",
                    [interaction.GuildId!.Value]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteResult { Name = $"Case #{lastCase.@case} - '{lastCase.reason?[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason?.Length > 30 ? "..." : "")}'", Value = lastCase.@case.ToString() }];
                }
                else return [];
            }
            else if (long.TryParse(text, out var possibleId))
            {
                var cases = Postgres.Select<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND (moderator_id = @2 OR target_id = @2) AND is_deleted = FALSE  ORDER BY created_at DESC LIMIT 25",
                    [interaction.GuildId!.Value, possibleId]
                );

                if (cases is not null)
                {
                    return cases.Select(
                        c => new AutocompleteResult
                        {
                            Name = $"Case #{c.@case} - '{c.reason?[..Math.Min(30, c.reason.Length)]}{(c.reason?.Length > 30 ? "..." : "")}'",
                            Value = c.@case.ToString()
                        }
                    );
                }
                else return [];
            }
            else
            {
                var cases = Postgres.Select<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND is_deleted = FALSE  AND reason ILIKE @2 ORDER BY created_at DESC LIMIT 25",
                    [interaction.GuildId!.Value, $"%{text}%"]
                );

                var lastCases = String.IsNullOrWhiteSpace(text) ? new List<AutocompleteResult>() {
                    new() { Name = "My Last Case", Value = "last" },
                    new() { Name = "Server's Last Case", Value = "slast" }
                } : [];

                if (cases is not null)
                {
                    return [..lastCases, ..cases.Select(
                        c => new AutocompleteResult
                        {
                            Name = $"Case #{c.@case} - '{c.reason?[..Math.Min(30, c.reason.Length)]}{(c.reason?.Length > 30 ? "..." : "")}'",
                            Value = c.@case.ToString()
                        }
                    )];
                }
                else return lastCases;
            }
        }

        public static async Task<IEnumerable<AutocompleteResult>> RobloxUser(SocketAutocompleteInteraction interaction, dynamic value)
        {
            if (value is not string text) return [];

            if (String.IsNullOrWhiteSpace(text) || text.Length <= 3) return [];

            var exactMatch = await Roblox.GetUserByUsername(text);
            var partialMatches = Roblox.Users.FindMany((u, id) => (u.name.Contains(text, StringComparison.OrdinalIgnoreCase) || (u.displayName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)) && id != exactMatch?.id);

            List<Roblox.RobloxUser> choices = [
                ..(
                exactMatch is not null 
                    ? new List<Roblox.RobloxUser> { exactMatch } 
                    : []
                ),
                .. partialMatches
            ];

            return [..choices.Select(u => new AutocompleteResult { Name = $"@{u.name} ({u.id})", Value = u.id })];
        }

        public static readonly List<double> durationSuggestions = [
            600_000,        // 10 Minutes
            1_800_000,      // 30 Minutes
            3_600_000,      // 1 Hour
            21_600_000,     // 6 Hours
            86_400_000,     // 1 Day
            259_200_000,    // 3 Days
            604_800_000,    // 1 Week
            2_592_000_000,  // 1 Month (30 days)
            31_471_200_000  // 1 Year  (364.25 days)
        ];

        public static async Task<IEnumerable<AutocompleteResult>> Duration(SocketAutocompleteInteraction _, dynamic value)
        {
            if (value is not string text) return [];

            if (String.IsNullOrWhiteSpace(text)) return [..
                durationSuggestions.Select(
                    v => new AutocompleteResult { Name = Time.ConvertMillisecondsToString(v), Value = value }
                )
            ];

            double duration = Time.ConvertStringToMilliseconds(text);

            double extra = duration % 1000;
            if (extra != 0 || DurationRegex().IsMatch(text))
            {
                duration -= extra;

                var highestUnit = Time.timeValues.Keys.LastOrDefault(v => duration / v >= 1);

                List<AutocompleteResult> choices = [];

                if (extra == 0)
                {
                    if (duration > 0)
                    {
                        choices.Add(new AutocompleteResult { Name = Time.ConvertMillisecondsToString(duration), Value = duration.ToString() });
                    }

                    choices.AddRange(
                        durationSuggestions.Where(v => highestUnit > v).Reverse().Select(v =>
                        {
                            var value = duration + v;

                            return new AutocompleteResult { Name = Time.ConvertMillisecondsToString(value), Value = value.ToString() };
                        }
                    ));
                }
                else
                {
                    choices.AddRange(
                        Time.timeValues.Keys.Where(v => v < highestUnit && v != 1).Reverse().Select(v =>
                        {
                            var value = duration + (extra * v);

                            return new AutocompleteResult { Name = Time.ConvertMillisecondsToString(value), Value = value.ToString() };
                        }
                    ));
                }

                return choices;
            }
            else
            {
                return [new AutocompleteResult { Name = Time.ConvertMillisecondsToString(duration), Value = duration.ToString() }];
            }
        }

        [GeneratedRegex(@".+((,|and| )( *))")]
        private static partial Regex DurationRegex();
    }
}
