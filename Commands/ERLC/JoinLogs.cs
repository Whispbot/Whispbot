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
    public class ERLC_JoinLogs : Command
    {
        public override string Name => "ER:LC Join Logs";
        public override string Description => "View the players who have recently joined / left.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "join-logs"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["joinlogs", "erlc joinlogs", "erlc joins", "erlc joinlog"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var joinlogs = response?.Server?.JoinLogs;

            if (joinlogs is not null)
            {
                if (joinlogs.Count == 0)
                {
                    await ctx.EditResponse($"{ctx.Emoji("cross")} {ctx.String("erlc.joinlogs.errors.none")}\n-# {ERLCCache.GenerateFooter(ctx, response!)}");
                    return;
                }

                joinlogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                joinlogs = [..joinlogs.Take(20)];

                List<ulong> robloxIds = [..joinlogs.Select(j => ulong.Parse(j.Player.Split(":")[1]))];
                robloxIds = [.. robloxIds.Distinct()];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                var IN_DISCORD = ctx.Emoji("indiscord");
                var BOOSTER = ctx.Emoji("booster");
                var JOINED = ctx.Emoji("clockedin");
                var LEFT = ctx.Emoji("clockedout");

				StringBuilder strings = new();
                foreach (var log in joinlogs)
                {
                    UserConfig? config = userConfigs?.Find(u => u.roblox_id.ToString() == log.Player.Split(":")[1]);
                    SocketGuildUser? member = members?.Find(m => m.Id == config?.id);

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append(IN_DISCORD);
                        if (member.PremiumSince is not null) flags.Append(BOOSTER);
                    }

                    strings.AppendLine($"{(log.Join ? JOINED : LEFT)} [<t:{log.Timestamp}:T>] {flags}{(flags.Length > 0 ? " " : "")}**@{log.Player.Split(":")[0]}** {ctx.String(log.Join ? "erlc.joinlogs.joined" : "erlc.joinlogs.left")}");
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle($"{ctx.String("erlc.joinlogs.title")}")
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

