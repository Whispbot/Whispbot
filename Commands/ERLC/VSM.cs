using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using Sentry.Protocol;
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
    public class ERLC_VSM : Command
    {
        public override string Name => "ER:LC Virtual Server Management";
        public override string Description => "Run a command inside your server from Discord.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["vsm", "erlc vsm", "erlc command"];
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
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ERLCModerator | BotPermissions.ERLCAdmin | BotPermissions.ERLCOWner)) return;

            if (ctx.args.Count == 0)
            {
                await ctx.Reply(
                    new MessageBuilder
                    {
                        components = [
                            new ContainerBuilder
                            {
                                components = [
                                    new TextDisplayBuilder("## {string.title.vsm.commands}"),
                                    new TextDisplayBuilder($"**{{string.title.vsm.mod}}**\n> {Tools.ERLC.ERLC_Commands.modCommands.Keys.Join(", ")}"),
                                    new TextDisplayBuilder($"**{{string.title.vsm.admin}}**\n> {Tools.ERLC.ERLC_Commands.adminCommands.Keys.Join(", ")}"),
                                    new TextDisplayBuilder($"**{{string.title.vsm.owner}}**\n> {Tools.ERLC.ERLC_Commands.ownerCommands.Keys.Join(", ")}")
                                ]
                            }
                        ],
                        flags = MessageFlags.IsComponentsV2
                    }
                );
            }
            else
            {
                string commandName = ctx.args[0];
                if (commandName.StartsWith(':')) commandName = commandName[1..];
                ctx.args.RemoveAt(0);

                async Task OnMissingArgs(int requiredNum, string format)
                {
                    await ctx.Reply($"Missing arguments for command, requires {requiredNum} arguments in the format `:{commandName} {format}`,");
                }
                
                if (Tools.ERLC.ERLC_Commands.modCommands.TryGetValue(commandName, out (int, string) v))
                {
                    if (ctx.args.Count < v.Item1)
                    {
                        await OnMissingArgs(v.Item1, v.Item2);
                        return;
                    }
                }
                else if (Tools.ERLC.ERLC_Commands.adminCommands.TryGetValue(commandName, out (int, string) a))
                {
                    if (ctx.args.Count < v.Item1)
                    {
                        await OnMissingArgs(a.Item1, a.Item2);
                        return;
                    }

                    if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ERLCAdmin | BotPermissions.ERLCOWner)) return;
                }
                else if (Tools.ERLC.ERLC_Commands.ownerCommands.TryGetValue(commandName, out (int, string) o))
                {
                    if (ctx.args.Count < v.Item1)
                    {
                        await OnMissingArgs(o.Item1, o.Item2);
                        return;
                    }

                    if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ERLCOWner)) return;
                }
                else
                {
                    await ctx.Reply($"{{emoji.cross}} Unknown command `:{commandName}`. Use this command without arguments to see a list of possible commands.");
                    return;
                }

                List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

                if (servers is null || servers.Count == 0)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                    return;
                }

                string? serverName = ctx.args.IndexOf("in") != -1 ? ctx.args.Join(" ").Split(" in ")[^1] : null;

                ERLCServerConfig? server = Tools.ERLC.GetServerFromString(servers, serverName ?? "thisservernameshouldntbepossibletomatch");

                if (server is not null)
                {
                    ctx.args.RemoveRange(ctx.args.LastIndexOf("in"), ctx.args.Count - ctx.args.LastIndexOf("in"));
                }
                else
                {
                    server = servers.FirstOrDefault();
                }

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

                await ctx.Reply("{emoji.loading} {string.content.erlcvsm.sending}...");

                var response = await Tools.ERLC.SendCommand(server, $":{commandName} {ctx.args.Join(" ")}");

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcvsm.failed}");
                    return;
                }

                if (Tools.ERLC.ResponseHasError(response, out var errorMessage))
                {
                    await ctx.EditResponse(errorMessage!);
                    return;
                }

                await ctx.EditResponse("{emoji.tick} {string.content.erlcvsm.success}.");
            }
        }
    }
}
