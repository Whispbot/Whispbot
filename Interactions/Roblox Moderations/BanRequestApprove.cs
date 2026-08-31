using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Tools;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class BanRequestApprove : InteractionCommandData
    {
        public override string CustomId => "rm_br_confirm";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            List<ERLCServerConfig>? erlcServers = (await WhispCache.ERLCServerConfigs.Get(ctx.GuildId.Value))?.Where(s => s.allow_ban_requests)?.ToList();
            if ((erlcServers?.Count ?? 0) > 0)
            {
                if (erlcServers!.Count > 1)
                {
                    await ctx.ShowModal(
                        new ModalBuilder()
                            .WithCustomId($"rm_br_confirm {ctx.args[0]}")
                            .WithTitle($"{ctx.String("rmod.requests.title.select_server")}")
                            .AddSelectMenu(
                                $"{ctx.String("rmod.requests.title.select_server_prompt")}",
                                new SelectMenuBuilder()
                                    .WithOptions([..
                                        erlcServers.Select(s => 
                                            new SelectMenuOptionBuilder()
                                                .WithLabel(s.name ?? $"Server {s.id}")
                                                .WithValue(s.id.ToString())
                                                .WithDescription($"Code: {s.code} | Players: {s.ingame_players}")
                                        )
                                    ])
                                    .WithRequired(true)
                            )
                            .Build()
                    );
                }
                else
                {
                    await ctx.DeferResponse();
                    var result = await Procedures.ApproveBanRequest(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId, erlcServers[0]);
                    if (result.Item1 is null)
                    {
                        await ctx.Respond($"{ctx.Emoji("cross")} {result.Item2}");
                        return;
                    }
                }
            }
            else
            {
                await ctx.DeferResponse();
                var result = await Procedures.MarkAsBanned(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId);
                if (result.Item1 is null)
                {
                    await ctx.Respond($"{ctx.Emoji("cross")} {result.Item2}");
                    return;
                }
            }
        }
    }
}
