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
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands
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
        public override List<string> Aliases => ["queue", "erlc queue"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User?.id is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLC.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLC.GetEndpointData<List<long>>(ctx, server, ERLC.Endpoint.ServerQueue);
            var queue = response?.data;

            if (queue is not null)
            {
                if (queue.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlcqueue.noplayers}}.\n-# {{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt)/1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                int queueLength = queue.Count;
                queue = queue[..Math.Min(queueLength, 20)];

                List<string> userIds = [..queue.Select(u => u.ToString())];
                List<Roblox.RobloxUser> relatedUsers = await Roblox.GetUserById(userIds) ?? [];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds([.. relatedUsers.Select(u => long.Parse(u.id))]);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder sb = new();

                foreach (long id in queue)
                {
                    Roblox.RobloxUser? user = relatedUsers.Find(u => u.id == id.ToString());
                    UserConfig? config = userConfigs.Find(u => u.roblox_id == id);
                    Member? member = members.Find(m => m.user?.id == config?.id.ToString());

                    List<string> flags = [];

                    if (member is not null)
                    {
                        flags.Add("{emoji.indiscord}");
                        if (member.premium_since is not null) flags.Add("{emoji.booster}");
                    }

                    sb.AppendLine($"{flags.Join("")}{(flags.Count > 0 ? " " : "")}**@{user?.name ?? "error"}** ({id})");
                }

                if (queueLength > 20)
                {
                    sb.AppendLine($"...and {queueLength - 20} more.");
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.erlcqueue}} ({queueLength})",
                                description = sb.ToString(),
                                footer = new EmbedFooter { text = await ERLC.GenerateFooter(response!) }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response?.code}] {response?.message ?? "An unknown error occured"}.");
            }
        }
    }
}
