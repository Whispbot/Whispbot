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
    public class ERLC_KillLogs : Command
    {
        public override string Name => "ER:LC Kill Logs";
        public override string Description => "View the players who have recently been killed.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "kill-logs"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["killlogs", "erlc killlogs", "erlc kills", "erlc killlog"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var killLogs = response?.Server?.KillLogs;

            if (killLogs is not null)
            {
                if (killLogs.Count == 0)
                {
                    await ctx.EditResponse($"{ctx.Emoji("cross")} {ctx.String("erlc.killlogs.errors.none")}\n-# {ERLCCache.GenerateFooter(ctx, response!)}");
                    return;
                }

                killLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                killLogs = [.. killLogs.Take(20)];

                List<ulong> robloxIds = [.. killLogs.Select(j => ulong.Parse(j.Killed.Split(":")[1])), .. killLogs.Select(k => ulong.Parse(k.Killer.Split(":")[1]))];
                robloxIds = [..robloxIds.Distinct()];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                var IN_DISCORD = ctx.Emoji("indiscord");
                var BOOSTER = ctx.Emoji("booster");
                var KILLED = ctx.String("erlc.killlogs.killed");

				StringBuilder strings = new();
                foreach (var log in killLogs)
                {
                    UserConfig? killedConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Killed.Split(":")[1]);
                    SocketGuildUser? killedMember = members?.Find(m => m.Id == killedConfig?.id);

                    UserConfig? killerConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Killer.Split(":")[1]);
                    SocketGuildUser? killerMember = members?.Find(m => m.Id == killerConfig?.id);

                    StringBuilder killedFlags = new();
                    if (killedMember is not null)
                    {
                        killedFlags.Append(IN_DISCORD);
                        if (killedMember.PremiumSince is not null) killedFlags.Append(BOOSTER);
                    }

                    StringBuilder killerFlags = new();
                    if (killerMember is not null)
                    {
                        killerFlags.Append(IN_DISCORD);
                        if (killerMember.PremiumSince is not null) killerFlags.Append(BOOSTER);
                    }

                    strings.AppendLine($"[<t:{log.Timestamp}:T>] {killerFlags}{(killerFlags.Length > 0 ? " " : "")}**@{log.Killer.Split(":")[0]}** {KILLED} {killedFlags}{(killedFlags.Length > 0 ? " " : "")}**@{log.Killed.Split(":")[0]}**");
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle($"{ctx.String("erlc.killlogs.title")}")
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

