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
    public class ERLC_ModCalls : Command
    {
        public override string Name => "ER:LC Mod Calls";
        public override string Description => "View the recent mod calls.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "modcall-logs"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["modcalls", "erlc modcalls", "erlc calls"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var callLogs = response?.Server?.ModCalls;

            if (callLogs is not null)
            {
                if (callLogs.Count == 0)
                {
                    await ctx.EditResponse($"{ctx.Emoji("cross")} {ctx.String("erlc.modcalls.errors.none")}\n-# {ERLCCache.GenerateFooter(ctx, response!)}");
                    return;
                }

                callLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                callLogs = [.. callLogs.Take(20)];

                List<ulong> robloxIds = [.. callLogs.Select(j => ulong.Parse(j.Caller.Split(":")[1])), .. callLogs.Where(c => c.Moderator is not null).Select(c => ulong.Parse(c.Moderator!.Split(":")[1]))];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                var IN_DISCORD = ctx.Emoji("indiscord");
				var BOOSTER = ctx.Emoji("booster");
                var ANSWERED = ctx.Emoji("clockedin");
                var UNANSWERED = ctx.Emoji("clockedout");
                var HELPED = ctx.String("erlc.modcalls.helped");
                var CALLED = ctx.String("erlc.modcalls.called");

				StringBuilder strings = new();
                foreach (var log in callLogs)
                {
                    UserConfig? callerConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Caller.Split(":")[1]);
                    SocketGuildUser? callerMember = members?.Find(m => m.Id == callerConfig?.id);

                    UserConfig? modConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Moderator?.Split(":")?[1]);
                    SocketGuildUser? modMember = members?.Find(m => m.Id == modConfig?.id);

                    StringBuilder callerFlags = new();
                    if (callerMember is not null)
                    {
                        callerFlags.Append(IN_DISCORD);
                        if (callerMember.PremiumSince is not null) callerFlags.Append(BOOSTER);
                    }

                    StringBuilder modFlags = new();
                    if (modMember is not null)
                    {
                        modFlags.Append(IN_DISCORD);
                        if (modMember.PremiumSince is not null) modFlags.Append(BOOSTER);
                    }

                    if (log.Moderator is not null && log.Moderator.Split(':')[1] != "1")
                    {
                        strings.AppendLine($"{ANSWERED} [<t:{log.Timestamp}:T>] {modFlags}{(modFlags.Length > 0 ? " " : "")}**@{log.Moderator.Split(':')[0]}** {HELPED} {callerFlags}{(callerFlags.Length > 0 ? " " : "")}**@{log.Caller.Split(':')[0]}**");
                    }
                    else
                    {
                        strings.AppendLine($"{UNANSWERED} [<t:{log.Timestamp}:T>] {callerFlags}{(callerFlags.Length > 0 ? " " : "")}**@{log.Caller.Split(':')[0]}** {CALLED}");
                    }
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle(ctx.String("erlc.modcalls.title"))
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

