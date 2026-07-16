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
    public class BanRequestApproveModal : InteractionCommandData
    {
        public override string CustomId => "rm_br_confirm";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1 || ctx.interaction.Data is not IModalInteractionData data) return;

            string? selectedId = data.Components.FirstOrDefault(c => c.CustomId == "server")?.Value;
            if (selectedId is null) return;

            List<ERLCServerConfig>? erlcServers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId.Value);
            ERLCServerConfig? server = erlcServers?.FirstOrDefault(s => s.id.ToString() == selectedId);

            if (server is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.rmbr.servernotfound}", ephemeral: true);
                return;
            }

            await ctx.DeferResponse();
            var result = await Procedures.ApproveBanRequest(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId, server);
            if (result.Item1 is null)
            {
                await ctx.SendFollowup($"{{emoji.cross}} {result.Item2}", ephemeral: true);
            }
        }
    }
}
