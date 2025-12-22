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
    public class ERLC_Vehicles : Command
    {
        public override string Name => "ER:LC Vehicles";
        public override string Description => "Get the currently spawned vehicles.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["vehicles", "cars", "erlc vehicles"];
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

            var response = await ERLC.GetEndpointData<List<ERLC.PRC_Vehicle>>(ctx, server, ERLC.Endpoint.ServerVehicles);
            var vehicles = response?.data;

            if (vehicles is not null)
            {
                if (vehicles.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlcvehicles.novehicles}}\n-# {{string.content.erlcserver.updated}}: {(response!.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt)/1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                List<Roblox.RobloxUser>? users = await Roblox.GetUserByUsername([.. vehicles.Select(v => v.Owner)]);
                List<long> robloxIds = [.. users?.Select(u => long.Parse(u.id)) ?? []];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder strings = new();
                foreach (var vehicle in vehicles)
                {
                    Roblox.RobloxUser? owner = users?.Find(u => u.name.Equals(vehicle.Owner, StringComparison.OrdinalIgnoreCase));
                    UserConfig? config = userConfigs?.Find(u => u.roblox_id == long.Parse(owner?.id ?? "0"));
                    Member? member = members?.Find(m => m.user?.id == config?.id.ToString());

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append("{emoji.indiscord}");

                        if (member.premium_since is not null) flags.Append("{emoji.booster}");
                    }

                    strings.Append($"**{flags}{(flags.Length > 0 ? " " : "")}@{vehicle.Owner}**\n> **{{string.title.erlcvehicles.model}}:** {vehicle.Name}\n> **{{string.title.erlcvehicles.texture}}:** {vehicle.Texture}\n\n");
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.erlcvehicles}} ({vehicles.Count})",
                                description = strings.ToString(),
                                footer = new EmbedFooter { text = await ERLC.GenerateFooter(response!) }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response!.code}] {response.message ?? "An unknown error occured"}.");
            }
        }
    }
}
