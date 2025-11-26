using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class BanRequestApprove : InteractionData
    {
        public override string CustomId => "rm_br_confirm";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            List<ERLCServerConfig>? erlcServers = (await WhispCache.ERLCServerConfigs.Get(ctx.GuildId))?.Where(s => s.allow_ban_requests)?.ToList();
            if ((erlcServers?.Count ?? 0) > 0)
            {
                if (erlcServers!.Count > 1)
                {
                    await ctx.ShowModal(
                        new ModalBuilder
                        {
                            custom_id = $"rm_br_confirm {ctx.args[0]}",
                            title = "{string.title.rmbr.selectserver}",
                            components = [
                                new LabelBuilder
                                {
                                    label = "{string.title.rmbr.selectserver2}",
                                    component = new StringSelectBuilder("server")
                                    {
                                        options = [..erlcServers.Select(s => new StringSelectOption
                                        {
                                            label = s.name ?? $"Server {s.id}",
                                            value = s.id.ToString(),
                                            description = $"Code: {s.code} | Players: {s.ingame_players}"
                                        })],
                                        required = true
                                    }
                                }
                            ]
                        }
                    );
                }
                else
                {
                    await ctx.DeferUpdate();
                    await Procedures.ApproveBanRequest(long.Parse(ctx.args[0]), long.Parse(ctx.GuildId), long.Parse(ctx.UserId), erlcServers[0]);
                }
            }
            else
            {
                await ctx.DeferUpdate();
                await Procedures.MarkAsBanned(long.Parse(ctx.args[0]), long.Parse(ctx.GuildId), long.Parse(ctx.UserId));
            }
        }
    }
}
