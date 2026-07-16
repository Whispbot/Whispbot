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
                            .WithTitle("{string.title.rmbr.selectserver}")
                            .AddSelectMenu(
                                "{string.title.rmbr.selectserver2}",
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
                    await Procedures.ApproveBanRequest(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId, erlcServers[0]);
                }
            }
            else
            {
                await ctx.DeferResponse();
                await Procedures.MarkAsBanned(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId);
            }
        }
    }
}
