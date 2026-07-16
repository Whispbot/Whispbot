using Discord;
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
using Whispbot.Extensions;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot.Commands.ERLC
{
    public class ERLC_Queue : Command
    {
        public override string Name => "ER:LC Queue";
        public override string Description => "Get the players currently in the server queue.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [
            new RateLimit()
            {
                type = RateLimitType.User
            }
        ];
        public override List<string>? SlashCommand => ["erlc", "queue"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["queue", "erlc queue"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var queue = response?.Server?.Queue;

            if (queue is not null)
            {
                if (queue.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlcqueue.noplayers}}.\n-# {{string.content.erlcserver.updated}}: {(response!.CachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAtMs)/1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                int queueLength = queue.Count;
                queue = queue[..Math.Min(queueLength, 20)];

                List<string> userIds = [..queue.Select(u => u.ToString())];
                List<Roblox.RobloxUser> relatedUsers = await Roblox.GetUserById(userIds) ?? [];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds([.. relatedUsers.Select(u => ulong.Parse(u.id))]);
                List<IGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder sb = new();

                foreach (ulong id in queue)
                {
                    Roblox.RobloxUser? user = relatedUsers.Find(u => u.id == id.ToString());
                    UserConfig? config = userConfigs.Find(u => u.roblox_id == id);
                    IGuildUser? member = members.Find(m => m.Id == config?.id);

                    List<string> flags = [];

                    if (member is not null)
                    {
                        flags.Add("{emoji.indiscord}");
                        if (member.PremiumSince is not null) flags.Add("{emoji.booster}");
                    }

                    sb.AppendLine($"{flags.Join("")}{(flags.Count > 0 ? " " : "")}**@{user?.name ?? "error"}** ({id})");
                }

                if (queueLength > 20)
                {
                    sb.AppendLine($"...and {queueLength - 20} more.");
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle($"{{string.title.erlcqueue}} ({queueLength})")
                        .WithDescription(sb.ToString())
                        .WithFooter(ERLCCache.GenerateFooter(response!))
						.Build()
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response?.error}] {response?.error_message ?? "An unknown error occured"}.");
            }
        }
    }
}

