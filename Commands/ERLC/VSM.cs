using Discord;
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
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot.Commands.ERLC
{
    public class ERLC_VSM : Command
    {
        public override string Name => "ER:LC Virtual Server Management";
        public override string Description => "Run a command inside your server from Discord.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "command"];
        public override List<SlashCommandArg>? Arguments => [
            new ("command", "The command to run on the server.", CommandArgType.ERLCCommand),
            new ("server", "The ERLC server to run the command on. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<command:erlccommand>"];
        public override List<string> Aliases => ["vsm", "erlc vsm", "erlc command", ":"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ERLCModerator | BotPermissions.ERLCAdmin | BotPermissions.ERLCOWner)) return;

            if (ctx.args.Count == 0)
            {
                await ctx.Reply(
                    components: new ComponentBuilderV2()
                        .WithContainer(
                            new ContainerBuilder()
                                .WithTextDisplay("## {string.title.vsm.commands}")
                                .WithTextDisplay($"**{{string.title.vsm.mod}}**\n> {ERLCCommands.modCommands.Keys.Join(", ")}")
                                .WithTextDisplay($"**{{string.title.vsm.admin}}**\n> {ERLCCommands.adminCommands.Keys.Join(", ")}")
                                .WithTextDisplay($"**{{string.title.vsm.owner}}**\n> {ERLCCommands.ownerCommands.Keys.Join(", ")}")
                        )
                        .Build(),
                    flags: MessageFlags.ComponentsV2
                );
            }
            else
            {
                string command = ctx.args.Get("command")?.GetString()!;
                List<string> args = [..command.Split(' ')];
                string commandName = args[0];
                if (commandName.StartsWith(':')) commandName = commandName[1..];
                args.RemoveAt(0);

                async Task OnMissingArgs(int requiredNum, string format)
                {
                    await ctx.Reply($"Missing arguments for command, requires {requiredNum} arguments in the format `:{commandName} {format}`,");
                }
                
                if (ERLCCommands.modCommands.TryGetValue(commandName, out (int, string) v))
                {
                    if (args.Count < v.Item1)
                    {
                        await OnMissingArgs(v.Item1, v.Item2);
                        return;
                    }
                }
                else if (ERLCCommands.adminCommands.TryGetValue(commandName, out (int, string) a))
                {
                    if (args.Count < a.Item1)
                    {
                        await OnMissingArgs(a.Item1, a.Item2);
                        return;
                    }

                    if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ERLCAdmin | BotPermissions.ERLCOWner)) return;
                }
                else if (ERLCCommands.ownerCommands.TryGetValue(commandName, out (int, string) o))
                {
                    if (args.Count < o.Item1)
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

                string? serverName = ctx.type == CommandType.Legacy 
                    ? args.IndexOf("in") != -1 ? args.Join(" ").Split(" in ")[^1] : null
                    : ctx.args.Get("server")?.GetString();

                ERLCServerConfig? server = ERLCDatabase.GetServerFromString(servers, serverName ?? "thisservernameshouldntbepossibletomatch");

                if (server is not null)
                {
                    args.RemoveRange(args.LastIndexOf("in"), args.Count - args.LastIndexOf("in"));
                }
                else
                {
                    server = servers.FirstOrDefault(s => s.is_default);
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

                var response = await ERLCAPI.SendCommand(server, $":{commandName} {args.Join(" ")}");

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcvsm.failed}");
                    return;
                }

                if (Errors.ResponseHasError(response, out var errorMessage))
                {
                    await ctx.EditResponse(text: "", components: errorMessage!);
                    return;
                }

                await ctx.EditResponse("{emoji.tick} {string.content.erlcvsm.success}.");
            }
        }
    }
}

