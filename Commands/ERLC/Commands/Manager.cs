using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Commands.ERLCCommands.Commands.Debug;
using Whispbot.Commands.ERLCCommands.Commands.Moderation;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using YellowMacaroni.Discord.Sharding;

namespace Whispbot.Commands.ERLCCommands.Commands
{
    public class ERLCCommandManager
    {
        public readonly List<ERLCCommand> commands = [];
        public readonly List<ERLCCommand> staffCommands = [];

        public readonly Dictionary<string, RatelimitData> ratelimits = [];

        public readonly Dictionary<string, ERLCServerConfig?> serverMap = [];

        public ERLCCommandManager()
        {
            #region Commands

            RegisterCommand(new ServerInfo());
            RegisterCommand(new PlayerCount());

            RegisterCommand(new EditReason());
            RegisterCommand(new EditType());
            RegisterCommand(new VoidRobloxModeration());

            RegisterCommand(new PMUsers());

            #endregion

            Log.Debug($"[Debug] Loaded {commands.Count} ERLC commands");
        }

        public void RegisterCommand(ERLCCommand command)
        {
            if (commands.Any(c => c.Name == command.Name)) return;
            commands.Add(command);
        }

        public void RegisterStaffCommand(ERLCCommand command)
        {
            if (staffCommands.Any(c => c.Name == command.Name)) return;
            staffCommands.Add(command);
        }

        private int? _maxLength = null;
        public int MaxLength
        {
            get
            {
                _maxLength ??= commands.Max(c => c.Aliases.Max(a => a.Split(" ").Length));
                return _maxLength ?? 0;
            }
        }

        public async Task HandleMessage(Client client, Message message)
        {
            if (message.webhook_id is null) return; // Not from command webhook
            if (message.embeds.Count == 0) return; // Doesn't contain command data
            if (message.channel?.guild_id is null) return;

            // EMBED CONTENT
            // Title: Command Usage | Player Kicked | Player Banned
            // Description: [Username:UserID](ProfileUrl) [used the command | kicked | banned] `:command args`
            // Footer: Private Server: Code

            Embed commandEmbed = message.embeds[0];
            string? description = commandEmbed.description;
            string? footer = commandEmbed.footer?.text;

            if (description is null || footer is null || !footer.Contains("Private Server: ")) return;

            // 1: Username, 2: UserID, 3: Action, 4: Command, 5: Args https://regex101.com/r/riJkf5/1
            Regex regex = new(@"\[(.+):([0-9]+)\]\(.+\) (used the command:|banned|kicked) `([^ ]+) *(.*)`");
            var commandGroups = regex.Match(description).Groups;
            if (commandGroups.Count != 6) return; // Malformed data

            string username = commandGroups[1].Value;
            string userId = commandGroups[2].Value;
            string action = commandGroups[3].Value;
            string commandName = commandGroups[4].Value;
            string commandArgs = commandGroups[5].Value;

            if (username == "Remote Server") return;

            using var _ = Tracer.Start($"ERLCCommand: {(action == "used the command:" ? commandName : action)}");

            string serverKey = footer.Replace("Private Server: ", "").Trim();

            if (!serverMap.TryGetValue(serverKey, out ERLCServerConfig? serverConfig))
            {
                serverConfig = Postgres.SelectFirst<ERLCServerConfig>(
                    "SELECT * FROM erlc_servers WHERE guild_id = @1 AND code = @2",
                    [long.Parse(message.channel.guild_id), serverKey]
                );

                serverMap[serverKey] = serverConfig;
            }

            if (serverConfig is null || serverConfig.guild_id.ToString() != message.channel.guild_id) return;

            MatchCollection matches = Regex.Matches(commandArgs, @"--(\w+)");
            List<string> flags = [.. matches.Select(m => m.Groups[1].Value.ToLower())];
            List<string> args = [.. commandArgs.Split(" ").Where(a => !flags.Contains(a.Replace("--", "")))];

            ERLCCommandContext ctx = new(client, message, serverConfig, username, userId, args, flags);

            if (ctx.UserConfig is null)
            {
                if (commandName == ":log")
                {
                    await ctx.Reply("{string.content.erlccommand.notloggedin}.");
                }
                return;
            }

            if (ctx.GuildConfig?.version != Config.EnvId) return;

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
                        if (ctx.GuildId is null || ctx.UserId is null) return;
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
                            await ctx.Reply("{string.content.erlccommand.log.playernotfound}.");
                            return;
                        }

                        var (moderation, error) = await Procedures.CreateModeration(ctx.GuildId, ctx.UserId, playerId, modType, reason);

                        if (moderation is null)
                        {
                            await ctx.Reply(error ?? "{string.errors.erlccommand.log.failed}.");
                            return;
                        }

                        if (ctx.flags.Contains("bolo"))
                        {
                            var (bolo, boloError) = await Procedures.CreateBanRequest(ctx.GuildId, ctx.UserId, playerId, reason);

                            if (bolo is null)
                            {
                                await ctx.Reply("{string.errors.erlccommand.log.bolofailed}.");
                                return;
                            }
                            else
                            {
                                await ctx.Reply("{string.content.erlccommand.log.success2}.");
                                return;
                            }
                        }

                        await ctx.Reply("{string.content.erlccommand.log.success}.");
                    }
                }
            }
            else if (action == "kicked" || action == "banned")
            {
                if (ctx.GuildId is null || ctx.UserId is null || string.IsNullOrEmpty(commandName)) return;

                List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);

                if (types is null || types.Count == 0) return;

                RobloxModerationType? modType = types.FirstOrDefault(t => action == "kicked" ? t.is_kick_type : t.is_ban_type);
                
                if (modType is null) return;

                Roblox.RobloxUser? target = await Roblox.GetUserByUsername(commandName);

                if (target is null)
                {
                    await ctx.Reply("{string.content.erlccommand.log.playernotfound}");
                    return;
                }

                string reason = ctx.args.Join(" ");
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "No reason provided";
                }
                else
                {
                    reason = reason.Replace(" - Player Not In Game", "");
                }

                    var (moderation, error) = await Procedures.CreateModeration(ctx.GuildId, ctx.UserId, target.id, modType, reason);

                if (moderation is not null)
                {
                    if (ctx.flags.Contains("bolo"))
                    {
                        var (bolo, boloError) = await Procedures.CreateBanRequest(ctx.GuildId, ctx.UserId, target.id, reason);

                        if (bolo is not null)
                        {
                            await ctx.Reply("{string.content.erlccommand.log.kickandbrlogged}");
                        }
                        else
                        {
                            await ctx.Reply($"{{string.content.erlccommand.log.kicklogged}}. {error ?? "{ string.errors.erlccommand.log.bolofailed}"}.");
                        }
                    }
                    else
                    {
                        await ctx.Reply(action == "kicked" ? "{string.content.erlccommand.log.kicklogged}" : "{string.content.erlccommand.log.banlogged}");
                    }
                }
                else
                {
                    await ctx.Reply(error ?? "{string.errors.erlccommand.log.failed}");
                }
            }
        }

        public void Attach(Client client)
        {
            client.MessageCreate += async (c, message) =>
            {
                if (c is not Client cl) return;
                await HandleMessage(client, message);
            };
        }

        public void Attach(ShardingManager manager)
        {
            foreach (Shard shard in manager.shards)
            {
                Attach(shard.client);
            }
        }

        public class RatelimitData
        {
            public int Remaining;
            public DateTimeOffset Reset;
        }
    }
}
