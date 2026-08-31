using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Discord_Moderation;
using Whispbot.Commands.ERLC;
using Whispbot.Commands.General;
using Whispbot.Commands.Roblox_Moderation;
using Whispbot.Commands.Shifts;
using Whispbot.Commands.Staff;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools;
using Whispbot.Tools.Bot;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Logger;

namespace Whispbot.Commands
{
    public static class CommandManager
    {
        public static readonly List<Command> commands = [];
        public static readonly List<Command> staffCommands = [];

        public static readonly Dictionary<string, RatelimitData> ratelimits = [];

        public static readonly List<ulong> ignoreGuilds = [];

        public static void Init(DiscordShardedClient client)
        {
            #region Commands
            RegisterCommand(new Ping());
            RegisterCommand(new About());
            RegisterCommand(new Support());
            RegisterCommand(new Prefix());
            RegisterCommand(new Connections());

            RegisterCommand(new Clockin());
            RegisterCommand(new Clockout());
            RegisterCommand(new ShiftManage());
            RegisterCommand(new ShiftAdmin());
            RegisterCommand(new ShiftLeaderboard());
            RegisterCommand(new ShiftActivity());
            RegisterCommand(new ShiftActive());

            RegisterCommand(new LogModeration());
            RegisterCommand(new RobloxCase());
            RegisterCommand(new RobloxReason());
            RegisterCommand(new RobloxType());
            RegisterCommand(new RobloxVoid());
            RegisterCommand(new Roblox_Moderation.BanRequest());

            RegisterCommand(new ERLC_ServerInfo());
            RegisterCommand(new ERLC_Players());
            RegisterCommand(new ERLC_Player());
            RegisterCommand(new ERLC_Queue());
            RegisterCommand(new ERLC_Vehicles());
            RegisterCommand(new ERLC_JoinLogs());
            RegisterCommand(new ERLC_JoinServer());
            RegisterCommand(new ERLC_KillLogs());
            RegisterCommand(new ERLC_CommandLogs());
            RegisterCommand(new ERLC_ModCalls());
            RegisterCommand(new ERLC_VSM());

            RegisterCommand(new Warn());
            RegisterCommand(new Mute());
            RegisterCommand(new Unmute());
            RegisterCommand(new Kick());
            RegisterCommand(new Softban());
            RegisterCommand(new Ban());
            RegisterCommand(new Unban());
            RegisterCommand(new Reason());
            RegisterCommand(new VoidCase());

            RegisterStaffCommand(new Test());
            RegisterStaffCommand(new SQL());
            RegisterStaffCommand(new AIRequest());
            RegisterStaffCommand(new ResolveError());
            RegisterStaffCommand(new GuildFeatureFlags());
            RegisterStaffCommand(new ViewColor());
            RegisterStaffCommand(new ViewLanguages());
            RegisterStaffCommand(new GuildVersion());
            RegisterStaffCommand(new Page());
            #endregion

            Logging.Log($"Loaded {commands.Count} commands");

            client.MessageReceived += async (message) =>
            {
                await HandleMessage(client, message);
            };
        }

        public static void RegisterCommand(Command command)
        {
            if (commands.Any(c => c.Name == command.Name)) return;
            commands.Add(command);
        }

        public static void RegisterStaffCommand(Command command)
        {
            if (staffCommands.Any(c => c.Name == command.Name)) return;
            staffCommands.Add(command);
        }

        private static int? _maxLength = null;
        public static int MaxLength => _maxLength ??= commands.Max(c => c.Aliases.Max(a => a.Split(" ").Length));

        private static List<string> _allowMentionCommands = ["ping"];

        public static async Task HandleMessage(DiscordShardedClient client, SocketMessage rawMessage)
        {
            if (rawMessage is not SocketUserMessage message) return;
            if (message.Source != MessageSource.User) return; // ignore bots/webhooks
            if (message.Channel is IGuildChannel channel)
            {
                if (ignoreGuilds.Contains(channel.GuildId)) return;
            }
            else return; // only reply to guild messages

            using var messageTrace = Tracer.Start("Message");
            DateTimeOffset start = DateTimeOffset.UtcNow;

            GuildConfig? guildConfig = null;
            using (Tracer.Start("GetGuildConfig"))
                guildConfig = await WhispCache.GuildConfig.Get(channel.GuildId);

            string prefix = guildConfig?.prefix ?? Config.prefix;
            string mention = $"<@{client.CurrentUser.Id}>";
            bool mentioned = false;

            string staffPrefix = Config.staffPrefix;

            if (message.Content.StartsWith(mention)) { prefix = mention; mentioned = true; }

            if (message.Content.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                List<string> args = [.. message.Content[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
                string content = args.Join(" ");

                Command? command = GetCommandByName(content, commands, out int length);
                args.RemoveRange(0, length);

                if (command is null) return;

                if (guildConfig is null)
                {
                    await message.Channel.SendMessageAsync($"{Emojis.Get("cross")} {"errors.dbfailed".Translate(0)}.");
                    return;
                }

                if (guildConfig.version != Config.EnvId)
                {
                    if (!mentioned || !_allowMentionCommands.Contains(command.Name.ToLower()))
                    {
                        if (mentioned)
                        {
                            await message.Channel.SendMessageAsync("errors.version".Translate(guildConfig.default_language ?? 0, guildConfig.version.ToString(), Config.EnvId.ToString()));
                        }
                        return;
                    }
                }

                UserConfig? userConfig = null;
                using (Tracer.Start("GetUserConfig"))
                    userConfig = await WhispCache.UserConfig.Get(message.Author.Id);

                var (arguments, error) = await ArgParser.GetArguments(message, $"{prefix}{(prefix == mention ? " " : "")}", command, args, userConfig?.language ?? guildConfig.default_language ?? 0);
                if (error is not null)
                {
                    await ArgParser.SendArgError(message, error);
                    return;
                }

                using var commandTrace = Tracer.Start($"Command: {command.Name}");

                var ctx = new CommandContext(client, message, arguments!);

                if (await IsRatelimited(ctx, command)) return;

                try
                {
                    using (Tracer.Start("ExecuteCommand")) await command.ExecuteAsync(ctx);

                    DateTimeOffset end = DateTimeOffset.UtcNow;
                    LogCommand(ctx, command, end - start);
                }
                catch (Exception ex)
                {
                    using var _ = Tracer.Start("LoggingError");
                    var id = SentrySdk.CaptureException(ex);
                    using var __ = Tracer.Start($"SendingErrorMessage: {id}");
                    Log.Error(ex, $"An error occured while executing '{command.Name}'\nUser: @{ctx.User.Username} ({ctx.UserId})\nGuild: {ctx.Guild.Name} ({ctx.GuildId})");
                    await SendErrorMessage(ctx, id);
                }
            }
            else if
            (
                message.Content.StartsWith(staffPrefix, StringComparison.CurrentCultureIgnoreCase)
                //                          |   Support Server  |           ->         |     Member    |             ->           |  Has Staff Role?  |
                && (client.GetGuild(1096509172784300174)?.GetUser(message.Author.Id)?.Roles?.Any(r => r.Id == 1256333207599841435) ?? false)
            )
            {
                List<string> args = [.. message.Content[staffPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)];
                string content = args.Join(" ");

                Command? command = GetCommandByName(content, staffCommands, out int length);
                args.RemoveRange(0, length);

                if (command is null) return;

                var (arguments, error) = await ArgParser.GetArguments(message, staffPrefix, command, args, 0);
                if (error is not null)
                {
                    await ArgParser.SendArgError(message, error);
                    return;
                }

                command?.ExecuteAsync(new CommandContext(client, message, arguments!));
            }
        }

        public static void LogCommand(CommandContext ctx, Command command, TimeSpan duration)
        {
            Logging.Log(LogSeverity.Debug, "Commands", $"Executed '{command.Name}' in {Time.ConvertMillisecondsToString(duration.TotalMilliseconds, Small: true, RoundTo: 1)} for '@{ctx.User.Username}' ({ctx.UserId}) in '{ctx.Guild.Name}' ({ctx.GuildId})");
            Stats.LogCommand(ctx, command, duration);
        }

        public static Command? GetCommandByName(string name, List<Command> cmds, out int length)
        {
            Command? command = null;
            int commandLength = 0;
            for (int len = MaxLength; len > 0; len--)
            {
                Command? activeCommand = cmds.Find(c =>
                {
                    foreach (string alias in c.Aliases)
                    {
                        commandLength = alias.Split(" ").Length;
                        if (commandLength == len && (name.StartsWith($"{alias} ", StringComparison.CurrentCultureIgnoreCase) || name.Equals(alias, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    return false;
                });
                if (activeCommand is not null)
                {
                    command = activeCommand;
                    break;
                }
            }

            if (command is not null)
            {
                length = commandLength;
                return command;
            }
            else
            {
                length = 0;
                return null;
            }
        }

        public static async Task<bool> IsRatelimited(CommandContext ctx, Command command)
        {
            if (command.Ratelimits.Count > 0) return false;

            SocketMessage? message = ctx.message;
            SocketInteraction? interaction = ctx.interaction;

            foreach (var rl in command!.Ratelimits)
            {
                IGuildChannel? channel = message?.Channel as IGuildChannel;

                string? rlk = rl.type switch
                {
                    RateLimitType.Global => "global",
                    RateLimitType.Guild => (channel?.GuildId ?? interaction?.GuildId)?.ToString() ?? "global",
                    RateLimitType.User => (message?.Author.Id ?? interaction?.User.Id)?.ToString(),
                    _ => "global"
                };

                if (rlk is null) return true;

                string key = $"{command.Name}:{rlk}";

                RatelimitData? data = ratelimits.GetValueOrDefault(key);
                if (data is null)
                {
                    ratelimits[key] = new RatelimitData()
                    {
                        Remaining = rl.amount - 1,
                        Reset = DateTimeOffset.UtcNow + rl.per
                    };
                }
                else
                {
                    if (data.Remaining == 0 && data.Reset > DateTimeOffset.UtcNow)
                    {
                        await ctx.Reply("errors.ratelimited".Translate(ctx.Language, Time.ConvertMillisecondsToRelativeString(data.Reset.ToUnixTimeMilliseconds(), false, ", ", false, 1000)));

                        return true;
                    }
                    else
                    {
                        if (data.Reset <= DateTimeOffset.UtcNow)
                        {
                            data.Remaining = rl.amount - 1;
                            data.Reset = DateTimeOffset.UtcNow + rl.per;
                        }
                        else
                        {
                            data.Remaining--;
                        }
                    }
                }
            }

            return false;
        }

        public static async Task SendErrorMessage(CommandContext ctx, SentryId id)
        {
            var components = new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"## {ctx.String("errors.message.title")}")
                        .WithTextDisplay(ctx.String("errors.message.content"))
                        .WithTextDisplay($"{ctx.String("errors.message.id")}:\n```\n{id}\n```")
                        .WithSection(
                            [
                                new TextDisplayBuilder(ctx.String("errors.message.feedback"))
                            ],
                            new ButtonBuilder(
                                ctx.String("errors.message.feedback.button"),
                                $"error_feedback {ctx.UserId} {id}",
                                style: ButtonStyle.Secondary
                            )
                        )
                        .WithAccentColor(new(200, 69, 69))
                )
                .Build();

            if (!ctx.hasResponded)
            {
                await ctx.Reply(components: components, flags: MessageFlags.ComponentsV2);
            }
            else
            {
                await ctx.EditResponse(m => { m.Components = components; m.Flags = MessageFlags.ComponentsV2; });
            }
        }

        public class RatelimitData
        {
            public int Remaining;
            public DateTimeOffset Reset;
        }
    }
}
