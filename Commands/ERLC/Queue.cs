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

            List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

            if (servers is null || servers.Count == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}.");
                return;
            }

            ERLCServerConfig? server = Tools.ERLC.GetServerFromString(servers, ctx.args.Join(" "));

            if (server is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}.");
                return;
            }

            if (server.api_key is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.nokey}");
                return;
            }

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerQueue, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlcqueue.fetching}...");
                response = await Tools.ERLC.GetQueue(server);

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcserver.apierror}.");
                    return;
                }
            }

            if (Tools.ERLC.ResponseHasError(response, out var errorMessage))
            {
                await ctx.EditResponse(errorMessage!);
                return;
            }

            List<long>? queue = JsonConvert.DeserializeObject<List<long>>(response.data?.ToString() ?? "[]");

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
                List<Roblox.RobloxUser> relatedUsers = Roblox.Users.FindMany((u,_) => userIds.Contains(u.id));
                userIds = [..userIds.Except(relatedUsers.Select(u => u.id))];
                if (userIds.Count > 0)
                {
                    relatedUsers.AddRange(await Roblox.GetUserById(userIds) ?? []);
                }

                List<UserConfig> userConfigs = WhispCache.UserConfig.FindMany((u, _) => queue.Contains(u.roblox_id ?? -1));
                List<long> missingIds = [.. queue.Where(id => !userConfigs.Any(u => u.id == id))];
                if (missingIds.Count > 0)
                {
                    List<UserConfig>? fetchedConfigs = Postgres.Select<UserConfig>(
                        @"SELECT * FROM user_config WHERE roblox_id IS NOT NULL AND roblox_id = ANY(@1);",
                        [missingIds]
                    );

                    if (fetchedConfigs is not null && fetchedConfigs.Count > 0)
                    {
                        userConfigs.AddRange(fetchedConfigs);
                        foreach (var config in fetchedConfigs)
                        {
                            WhispCache.UserConfig.Insert(config.id.ToString(), config);
                        }
                    }
                }

                List<string> discordIds = [..userConfigs.Select(u => u.id.ToString())];
                List<Member> members = ctx.Guild?.members.FindMany((m, _) => discordIds.Contains(m.user?.id ?? "")) ?? [];
                List<string> remainingDiscordIds = [.. discordIds.Except(members.Select(m => m.user?.id!))];
                if (remainingDiscordIds.Count > 0 && ctx.Guild is not null)
                {
                    members.AddRange(await ctx.Guild.GetMembers(ctx.client, remainingDiscordIds) ?? []);
                }

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
                                footer = new EmbedFooter { text = $"{{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt)/1000)}s ago" : "{string.content.erlcserver.justnow}")}" }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response.code}] {response.message ?? "An unknown error occured"}.");
            }
        }
    }
}
