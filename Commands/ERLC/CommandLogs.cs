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
    public class ERLC_CommandLogs : Command
    {
        public override string Name => "ER:LC Command Logs";
        public override string Description => "View the recently run commands.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["commandlogs", "erlc commandlogs", "erlc commands", "erlc cmds", "erlc logs"];
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
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                return;
            }

            ERLCServerConfig? server = Tools.ERLC.GetServerFromString(servers, ctx.args.Join(" "));

            if (server is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                return;
            }

            if (server.api_key is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.nokey}");
                return;
            }

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerCommandlogs, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlccommandlogs.fetching}...");
                response = await Tools.ERLC.GetCommands(server);

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcserver.apierror}");
                    return;
                }
            }

            if (Tools.ERLC.ResponseHasError(response, out var errorMessage))
            {
                await ctx.EditResponse(errorMessage!);
                return;
            }

            List<Tools.ERLC.PRC_CommandLog>? commandLogs = JsonConvert.DeserializeObject<List<Tools.ERLC.PRC_CommandLog>>(response.data?.ToString() ?? "[]");

            if (commandLogs is not null)
            {
                if (commandLogs.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlccommandlogs.nocommands}}\n-# {{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                commandLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                commandLogs = [.. commandLogs.Take(20)];

                List<long> robloxIds = [.. commandLogs.Select(j => long.Parse(j.Player.Split(":")[1]))];
                robloxIds = [..robloxIds.Distinct()];
                List<UserConfig>? userConfigs = WhispCache.UserConfig.FindMany((u, _) => robloxIds.Contains(u.roblox_id ?? 0));
                List<long> missingIds = [.. robloxIds.Where(id => !userConfigs.Any(u => u.roblox_id == id))];
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

                List<Member>? members = null;
                if (userConfigs is not null && userConfigs.Count > 0 && ctx.Guild is not null)
                {
                    members = ctx.Guild.members.FindMany((m, _) => userConfigs.Any(u => u.id.ToString() == m.user?.id));
                    List<string> remainingMembers = [.. userConfigs.Where(u => !members.Any(m => m.user!.id == u.id.ToString())).Select(u => u.id.ToString())];
                    if (remainingMembers.Count > 0)
                    {
                        members.AddRange(await ctx.Guild.GetMembers(ctx.client, [.. userConfigs.Select(u => u.id.ToString())]));
                    }
                }

                StringBuilder strings = new();
                foreach (var log in commandLogs)
                {
                    UserConfig? config = userConfigs?.Find(u => u.roblox_id.ToString() == log.Player.Split(":")[1]);
                    Member? member = members?.Find(m => m.user?.id == config?.id.ToString());

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append("{emoji.indiscord}");
                        if (member.premium_since is not null) flags.Append("{emoji.booster}");
                    }

                    string action = "used";
                    string command = log.Command;

                    if (command.StartsWith(":ban"))
                    {
                        action = "banned";
                        command = command.Replace(":ban", "").Trim();
                    }
                    else if (command.StartsWith(":kick"))
                    {
                        action = "kicked";
                        command = command.Replace(":kick", "").Trim();
                    }

                    strings.AppendLine($"[<t:{log.Timestamp}:T>] {flags}{(flags.Length > 0 ? " " : "")}**@{log.Player.Split(":")[0]}** {action} `{command.AsSpan(0, 50)}{(command.Length > 50 ? "..." : "")}`.");
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.commandlogs}}",
                                description = strings.ToString(),
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
