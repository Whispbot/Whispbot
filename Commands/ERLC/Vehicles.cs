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
    public class ERLC_Vehicles : Command
    {
        public override string Name => "ER:LC Vehicles";
        public override string Description => "Get the currently spawned vehicles.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "vehicles"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["vehicles", "cars", "erlc vehicles"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var vehicles = response?.Server?.Vehicles;

            if (vehicles is not null)
            {
                if (vehicles.Count == 0)
                {
                    await ctx.EditResponse($"{ctx.Emoji("cross")} {ctx.String("erlc.vehicles.errors.none")}\n-# {ERLCCache.GenerateFooter(ctx, response!)}");
                    return;
                }

                List<Roblox.RobloxUser>? users = await Roblox.GetUserByUsername([.. vehicles.Select(v => v.Owner)]);
                List<ulong> robloxIds = [.. users?.Select(u => ulong.Parse(u.id)) ?? []];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                var IN_DISCORD = ctx.Emoji("indiscord");
                var BOOSTER = ctx.Emoji("booster");

                StringBuilder strings = new();
                foreach (var vehicle in vehicles)
                {
                    Roblox.RobloxUser? owner = users?.Find(u => u.name.Equals(vehicle.Owner, StringComparison.OrdinalIgnoreCase));
                    UserConfig? config = userConfigs?.Find(u => u.roblox_id.ToString() == owner?.id);
                    SocketGuildUser? member = members?.Find(m => m.Id == config?.id);

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append(IN_DISCORD);

                        if (member.PremiumSince is not null) flags.Append(BOOSTER);
                    }

                    strings.Append($"**{flags}{(flags.Length > 0 ? " " : "")}@{vehicle.Owner}**\n{ctx.String("erlc.vehicles.vehicle",
                        vehicle.Name,
                        vehicle.Plate,
                        vehicle.Texture ?? ctx.String("erlc.player.vehicle.no_texture"),
                        vehicle.ColorName,
                        vehicle.ColorHex
                    )}\n\n");
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
					    .WithTitle($"{ctx.String("erlc.vehicles.title")} ({vehicles.Count})")
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

