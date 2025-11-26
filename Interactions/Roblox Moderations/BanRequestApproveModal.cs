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
    public class BanRequestApproveModal : InteractionData
    {
        public override string CustomId => "rm_br_confirm";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            string? selectedId = ctx.interaction.GetStringSelectField("server")?.FirstOrDefault();
            if (selectedId is null) return;

            List<ERLCServerConfig>? erlcServers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);
            ERLCServerConfig? server = erlcServers?.FirstOrDefault(s => s.id.ToString() == selectedId);

            if (server is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.rmbr.servernotfound}", true);
                return;
            }

            await ctx.DeferUpdate();
            var result = await Procedures.ApproveBanRequest(long.Parse(ctx.args[0]), long.Parse(ctx.GuildId), long.Parse(ctx.UserId), server);
            if (result.Item1 is null)
            {
                await ctx.SendFollowup($"{{emoji.cross}} {result.Item2}", true);
            }
        }
    }
}
