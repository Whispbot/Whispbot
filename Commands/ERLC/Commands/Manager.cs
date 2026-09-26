using Discord.WebSocket;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.ERLC.Commands.Debug;
using Whispbot.Commands.ERLC.Commands.Moderation;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using Discord;
using Whispbot.Tools.Logging;

namespace Whispbot.Commands.ERLC.Commands
{
    public static partial class ERLCCommandManager
    {
        public static readonly List<ERLCCommand> commands = [];
        public static readonly List<ERLCCommand> staffCommands = [];

        public static readonly Dictionary<string, RatelimitData> ratelimits = [];

        public static readonly Dictionary<string, ERLCServerConfig?> serverMap = [];

        public static void Init(DiscordShardedClient client)
        {
            #region Commands

            RegisterCommand(new ServerInfo());
            RegisterCommand(new PlayerCount());

            RegisterCommand(new EditReason());
            RegisterCommand(new EditType());
            RegisterCommand(new VoidRobloxModeration());

            RegisterCommand(new PMUsers());

            #endregion

            Logging.Log($"Loaded {commands.Count} ERLC commands");

            client.MessageReceived += async (message) =>
            {
                await HandleMessage(client, message);
            };
        }

        public static void RegisterCommand(ERLCCommand command)
        {
            if (commands.Any(c => c.Name == command.Name)) return;
            commands.Add(command);
        }

        public static void RegisterStaffCommand(ERLCCommand command)
        {
            if (staffCommands.Any(c => c.Name == command.Name)) return;
            staffCommands.Add(command);
        }

        private static int? _maxLength = null;
        public static int MaxLength
        {
            get
            {
                _maxLength ??= commands.Max(c => c.Aliases.Max(a => a.Split(" ").Length));
                return _maxLength ?? 0;
            }
        }

        public static async Task HandleMessage(DiscordShardedClient client, SocketMessage message)
        {
            if (message.Source != MessageSource.Webhook) return; // Not from command webhook
            if (message.Embeds.Count == 0) return; // Doesn't contain command data
            if (message.Channel is not SocketTextChannel channel) return;

            GuildConfig? config = await WhispCache.GuildConfig.Get(channel.Guild.Id);
            if (config is null) return;
            if (config.version != Config.EnvId) return; // Make sure commands are only responded to once

            // EMBED CONTENT
            // Title: Command Usage | Player Kicked | Player Banned
            // Description: [Username:UserID](ProfileUrl) [used the command | kicked | banned] `:command args`
            // Footer: Private Server: Code

            Embed commandEmbed = message.Embeds.First();
            string? description = commandEmbed.Description;
            string? footer = commandEmbed.Footer?.Text;

            if (description is null || footer is null || !footer.Contains("Private Server: ")) return; // Not valid command log

            // 1: Username, 2: UserID, 3: Action, 4: Command, 5: Args https://regex101.com/r/riJkf5/1
            Regex regex = ERLCCommandRegex();
            var commandGroups = regex.Match(description).Groups;
            if (commandGroups.Count != 6) return; // Malformed data

            string username = commandGroups[1].Value;
            string userId = commandGroups[2].Value;
            string action = commandGroups[3].Value;
            string commandName = commandGroups[4].Value;
            string commandArgs = commandGroups[5].Value;

            if (username == "Remote Server") return; // Ignore VSM (commands ran by the bot) to avoid infinite loops

            using var _ = Tracer.Start($"ERLCCommand: {(action == "used the command:" ? commandName : action)}");

            string serverKey = footer.Replace("Private Server: ", "").Trim();

            if (!serverMap.TryGetValue(serverKey, out ERLCServerConfig? serverConfig))
            {
                serverConfig = Postgres.SelectFirst<ERLCServerConfig>(
                    "SELECT * FROM erlc_servers WHERE guild_id = @1 AND code = @2",
                    [channel.Guild.Id, serverKey]
                );

                serverMap[serverKey] = serverConfig;
            }

            // Make sure that the config is for this server to avoid cross-server spoofing
            if (serverConfig is null || serverConfig.guild_id != channel.Guild.Id) return;

            MatchCollection matches = CommandArgsRegex().Matches(commandArgs);
            List<string> flags = [.. matches.Select(m => m.Groups[1].Value.ToLower())];
            List<string> args = [.. commandArgs.Split(" ").Where(a => !flags.Contains(a.Replace("--", "")))];

            ERLCCommandContext ctx = new(client, message, serverConfig, username, userId, args, flags);

            if (ctx.UserConfig is null)
            {
                if (commandName == ":log") // All commands that use :log require being logged in to work
                {
                    await ctx.Reply($"{ctx.String("erlc.errors.not_connected")}.");
                }
                return;
            }

            if (action == "used the command:")
            {
                commandName = commandName[1..];

                if (commandName == "log" && ctx.args.Count > 0)
                {
                    string cmdName = ctx.args[0];
                    ERLCCommand? command = commands.FirstOrDefault(c => c.Aliases.Contains(cmdName.ToLower()));

                    if (command is not null)
                    {
                        try
                        {
                            ctx.args.RemoveAt(0);
                            await command.ExecuteAsync(ctx);
                        }
                        catch (Exception ex)
                        {
                            SentrySdk.CaptureException(ex);
                        }
                    }
                    else
                    {
                        List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);

                        if (types is null || types.Count == 0) return;
                        if (ctx.args.Count < 2) return;

                        bool arg = true; // true for first, false for second
                        string arg1 = ctx.args[0];
                        string arg2 = ctx.args[1];
                        string reason = ctx.args.Count > 2 ? ctx.args[2..].Join(" ") : "No reason provided";

                        RobloxModerationType? modType = types.FirstOrDefault(t => t.triggers.Contains(arg1.ToLower()));
                        if (modType is null)
                        {
                            modType = types.FirstOrDefault(t => t.triggers.Contains(arg2.ToLower()));
                            arg = false;
                        }
                        if (modType is null) return;

                        string targetUser = arg ? arg2 : arg1;
                        string? targetData = await ERLCCommandUtils.GetUserFromPartialName(targetUser, serverConfig);

                        string? playerId = targetData?.Split(":")[1] ?? await Roblox.GetUserIdByUsername(targetUser);

                        if (playerId is null)
                        {
                            await ctx.Reply($"{ctx.String("erlc.log.player_not_found")}.");
                            return;
                        }

                        ulong targetId = ulong.Parse(playerId);

                        var (moderation, error) = await Procedures.CreateModeration(ctx.GuildId, ctx.UserId, targetId, modType, reason);

                        if (moderation is null)
                        {
                            await ctx.Reply(error ?? $"{ctx.String("erlc.errors.log_failed")}.");
                            return;
                        }

                        if (ctx.flags.Contains("bolo"))
                        {
                            var (bolo, boloError) = await Procedures.CreateBanRequest(ctx.GuildId, ctx.UserId, targetId, reason);

                            if (bolo is null)
                            {
                                await ctx.Reply($"{ctx.String("erlc.errors.request_failed")}.");
                                return;
                            }
                            else
                            {
                                await ctx.Reply($"{ctx.String("erlc.log.request_success")}.");
                                return;
                            }
                        }

                        await ctx.Reply($"{ctx.String("erlc.log.success")}.");
                    }
                }
            }
            else if (action == "kicked" || action == "banned")
            {
                if (string.IsNullOrEmpty(commandName)) return;

                List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);

                if (types is null || types.Count == 0) return;

                RobloxModerationType? modType = types.FirstOrDefault(t => action == "kicked" ? t.is_kick_type : t.is_ban_type);
                
                if (modType is null) return;

                Roblox.RobloxUser? target = await Roblox.GetUserByUsername(commandName);

                if (target is null)
                {
                    await ctx.Reply($"{ctx.String("erlc.log.player_not_found")}");
                    return;
                }

                ulong targetId = ulong.Parse(target.id);

                string reason = ctx.args.Join(" ");
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "No reason provided";
                }
                else
                {
                    reason = reason.Replace(" - Player Not In Game", "");
                }

                    var (moderation, error) = await Procedures.CreateModeration(ctx.GuildId, ctx.UserId, targetId, modType, reason);

                if (moderation is not null)
                {
                    if (ctx.flags.Contains("bolo"))
                    {
                        var (bolo, boloError) = await Procedures.CreateBanRequest(ctx.GuildId, ctx.UserId, targetId, reason);

                        if (bolo is not null)
                        {
                            await ctx.Reply($"{ctx.String("erlc.log.kick_and_request_logged")}");
                        }
                        else
                        {
                            await ctx.Reply($"{ctx.String("erlc.log.kick_logged")}. {error ?? "{ string.errors.erlccommand.log.bolofailed}"}.");
                        }
                    }
                    else
                    {
                        await ctx.Reply(action == "kicked" ? $"{ctx.String("erlc.log.kick_logged")}" : $"{ctx.String("erlc.log.ban_logged")}");
                    }
                }
                else
                {
                    await ctx.Reply(error ?? $"{ctx.String("erlc.errors.log_failed")}");
                }
            }
        }

        public class RatelimitData
        {
            public int Remaining;
            public DateTimeOffset Reset;
        }

        [GeneratedRegex(@"\[(.+):([0-9]+)\]\(.+\) (used the command:|banned|kicked) `([^ ]+) *(.*)`")]
        private static partial Regex ERLCCommandRegex();
        [GeneratedRegex(@"--(\w+)")]
        private static partial Regex CommandArgsRegex();
    }
}
