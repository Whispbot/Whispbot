using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot.Commands.ERLC
{
    public class ERLC_CommandLogs : Command
    {
        public override string Name => "ER:LC Command Logs";
        public override string Description => "View the recently run commands.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "command-logs"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["commandlogs", "erlc commandlogs", "erlc commands", "erlc cmds", "erlc logs"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var commandLogs = response?.Server?.CommandLogs;

            if (commandLogs is not null)
            {
                if (commandLogs.Count == 0)
                {
                    await ctx.EditResponse($"{ctx.Emoji("cross")} {ctx.String("erlc.commandlogs.errors.none")}\n-# {ERLCCache.GenerateFooter(ctx, response!)}");
                    return;
                }

                commandLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                commandLogs = [.. commandLogs.Take(20)];

                List<ulong> robloxIds = [.. commandLogs.Where(j => j.Player != "Remote Server").Select(j => ulong.Parse(j.Player.Split(":")[1]))];
                robloxIds = [..robloxIds.Distinct()];

                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                var IN_DISCORD = ctx.Emoji("indiscord");
                var BOOSTER = ctx.Emoji("booster");
                var USED = ctx.String("erlc.commandlogs.used");
                var KICKED = ctx.String("erlc.commandlogs.kicked");
                var BANNED = ctx.String("erlc.commandlogs.banned");

				StringBuilder strings = new();
                foreach (var log in commandLogs)
                {
                    UserConfig? config = log.Player == "Remote Server" ? null : userConfigs?.Find(u => u.roblox_id.ToString() == log.Player.Split(":")[1]);
                    SocketGuildUser? member = members?.Find(m => m.Id == config?.id);

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append(IN_DISCORD);
                        if (member.PremiumSince is not null) flags.Append(BOOSTER);
                    }

                    string action = USED;
                    string command = log.Command;

                    if (command.StartsWith(":ban"))
                    {
                        action = BANNED;
                        command = command.Replace(":ban", "").Trim();
                    }
                    else if (command.StartsWith(":kick"))
                    {
                        action = KICKED;
                        command = command.Replace(":kick", "").Trim();
                    }

                    strings.AppendLine($"[<t:{log.Timestamp}:T>] {flags}{(flags.Length > 0 ? " " : "")}**@{(log.Player == "Remote Server" ? "VSM" : log.Player.Split(":")[0])}** {action} `{command[0..(Math.Min(50, command.Length))]}{(command.Length > 50 ? "..." : "")}`.");
                }

                await ctx.EditResponse(
                    "",
                    embed: new EmbedBuilder()
                        .WithTitle($"{ctx.String("erlc.commandlogs.title")}")
                        .WithDescription(strings.ToString())
                        .WithFooter(ERLCCache.GenerateFooter(ctx, response!))
                        .Build()
                );
            }
            else
            {
                await ctx.EditResponse(response.GenerateErrorMessage(ctx));
            }
        }
    }
}

