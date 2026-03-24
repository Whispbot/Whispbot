using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OpenAI.Realtime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions
{
    public static class Autocomplete
    {
        public static ApplicationCommandInteractionDataOption? GetOption(List<ApplicationCommandInteractionDataOption> options, List<string> names, out List<string> outNames)
        {
            outNames = names;

            foreach (var option in options)
            {
                if ((option.type == ApplicationCommandOptionType.SubCommand || option.type == ApplicationCommandOptionType.SubCommandGroup) && option.options is not null)
                {
                    if (option.name is not null) outNames.Add(option.name);
                    var found = GetOption(option.options, outNames, out var finalNames);
                    outNames = finalNames;
                    if (found is not null) return found;
                }
                else if (option.focused == true)
                {
                    return option;
                }
            }

            return null;
        }

        public static Command? GetCommand(List<string> names) =>
            Config.commands?.commands?.Find(cmd =>
                cmd.SlashCommand is not null
                && cmd.SlashCommand.Count == names.Count
                && cmd.SlashCommand.SequenceEqual(names, StringComparer.OrdinalIgnoreCase));

        public static SlashCommandArg? GetArg(Command command, string name) => command.Arguments?.Find(arg => arg.name == name);
        public static SlashCommandArgType? GetArgType(Command command, string name) => GetArg(command, name)?.type;

        public static async Task Handle(Interaction interaction)
        {
            var data = interaction.data;
            if (data?.options is null || data.name is null) return;

            var option = GetOption(data.options, [data.name], out var names);
            if (option?.name is null) return;

            var command = GetCommand(names);
            if (command is null) return;

            var type = GetArgType(command, option.name);
            if (type is null) return;

            var value = option.value;
            if (value is null) return;

            if (Functions.TryGetValue(type.Value, out var func))
            {
                var choices = await func(interaction, value);
                await interaction.AutocompleteResult(choices);
            }
            else
            {
                await interaction.AutocompleteResult([]);
            }
        }

        public static Dictionary<SlashCommandArgType, Func<Interaction, dynamic, Task<List<AutocompleteChoices>>>> Functions = new()
        {
            { SlashCommandArgType.ShiftType,    ShiftType               },
            { SlashCommandArgType.RobloxType,   RobloxModerationType    },
            { SlashCommandArgType.ERLCServer,   ERLCServer              },
            { SlashCommandArgType.Case,         DiscordCase             },
            { SlashCommandArgType.RobloxUser,   RobloxUser              }
        };

        public static async Task<List<AutocompleteChoices>> ShiftType(Interaction interaction, dynamic value)
        {
            if (value is not string text || interaction.guild_id is null) return [];

            var types = await WhispCache.ShiftTypes.Get(interaction.guild_id);
            var searchedTypes = String.IsNullOrWhiteSpace(text)
                ? types
                : types?.FindAll(
                    t =>
                    t.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || t.triggers.Any(tr => tr.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                )) ?? [];

            return [.. (searchedTypes?.Select(t => new AutocompleteChoices { name = t.name, value = t.id.ToString() }) ?? [])];
        }

        public static async Task<List<AutocompleteChoices>> RobloxModerationType(Interaction interaction, dynamic value)
        {
            if (value is not string text || interaction.guild_id is null) return [];

            var types = await WhispCache.RobloxModerationTypes.Get(interaction.guild_id);
            var searchedTypes = String.IsNullOrWhiteSpace(text)
                ? types
                : types?.FindAll(
                    t =>
                    t.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || t.triggers.Any(tr => tr.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                )) ?? [];

            return [.. (searchedTypes?.Select(t => new AutocompleteChoices { name = t.name, value = t.id.ToString() }) ?? [])];
        }

        public static async Task<List<AutocompleteChoices>> ERLCServer(Interaction interaction, dynamic value)
        {
            if (value is not string text || interaction.guild_id is null) return [];

            var servers = await WhispCache.ERLCServerConfigs.Get(interaction.guild_id);
            var searchedServers = String.IsNullOrWhiteSpace(text)
                ? servers
                : servers?.FindAll(
                    s =>
                    s.name is not null && s.name.Contains(text, StringComparison.OrdinalIgnoreCase)
                ) ?? [];

            return [.. (searchedServers?.Select(s => new AutocompleteChoices { name = s.name ?? $"Server {s.id}", value = s.id.ToString() }) ?? [])];
        }

        public static async Task<List<AutocompleteChoices>> DiscordCase(Interaction interaction, dynamic value)
        {
            if (value is not string text || interaction.guild_id is null) return [];

            if (text.Equals("last", StringComparison.OrdinalIgnoreCase) && interaction.member?.user?.id is not null)
            {
                var lastCase = Postgres.SelectFirst<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY created_at DESC LIMIT 1",
                    [interaction.guild_id.ToLong(), interaction.member.user.id.ToLong()]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteChoices { name = $"Case #{lastCase.case_id} - '{lastCase.reason[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason.Length > 30 ? "..." : "")}'", value = lastCase.case_id.ToString() }];
                }
                else return [];
            }
            else if (text.Equals("slast", StringComparison.OrdinalIgnoreCase) && interaction.member?.user?.id is not null)
            {
                var lastCase = Postgres.SelectFirst<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 ORDER BY created_at DESC LIMIT 1",
                    [interaction.guild_id.ToLong()]
                );

                if (lastCase is not null)
                {
                    return [new AutocompleteChoices { name = $"Case #{lastCase.case_id} - '{lastCase.reason[..Math.Min(30, lastCase.reason.Length)]}{(lastCase.reason.Length > 30 ? "..." : "")}'", value = lastCase.case_id.ToString() }];
                }
                else return [];
            }
            else if (long.TryParse(text, out var possibleId))
            {
                var cases = Postgres.Select<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND (moderator_id = @2 OR target_id = @2) ORDER BY created_at DESC LIMIT 25",
                    [interaction.guild_id.ToLong(), possibleId]
                );

                if (cases is not null)
                {
                    return [..cases.Select(
                        c => new AutocompleteChoices
                        {
                            name = $"Case #{c.case_id} - '{c.reason[..Math.Min(30, c.reason.Length)]}{(c.reason.Length > 30 ? "..." : "")}'",
                            value = c.case_id.ToString()
                        }
                    )];
                }
                else return [];
            }
            else
            {
                var cases = Postgres.Select<DiscordModerationCase>(
                    "SELECT * FROM discord_moderations WHERE guild_id = @1 AND reason ILIKE @2 ORDER BY created_at DESC LIMIT 25",
                    [interaction.guild_id.ToLong(), $"%{text}%"]
                );

                var lastCases = String.IsNullOrWhiteSpace(text) ? new List<AutocompleteChoices>() {
                    new() { name = "My Last Case", value = "last" },
                    new() { name = "Server's Last Case", value = "slast" }
                } : [];

                if (cases is not null)
                {
                    return [..lastCases, ..cases.Select(
                        c => new AutocompleteChoices
                        {
                            name = $"Case #{c.case_id} - '{c.reason[..Math.Min(30, c.reason.Length)]}{(c.reason.Length > 30 ? "..." : "")}'",
                            value = c.case_id.ToString()
                        }
                    )];
                }
                else return [.. lastCases];
            }
        }

        public static async Task<List<AutocompleteChoices>> RobloxUser(Interaction interaction, dynamic value)
        {
            if (value is not string text || interaction.guild_id is null) return [];

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

            return [..choices.Select(u => new AutocompleteChoices { name = $"@{u.name} ({u.id})", value = u.id })];
        }
    }
}
