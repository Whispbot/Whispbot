using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using static Whispbot.Commands.General.Connections;

namespace Whispbot.Interactions.Roblox_Connection
{
    public class RobloxDisconnect: InteractionCommandData
    {
        public override string CustomId => "disconnect_roblox";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferResponse();

            UserConfig? updatedConfig = Postgres.SelectFirst<UserConfig>(
                @"UPDATE user_config SET roblox_id = NULL WHERE id = @1 RETURNING *;",
                [ctx.UserId]
            );

            if (updatedConfig is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("connections.errors.disconnect_failed")}.", ephemeral: true);
            }
            else
            {
                await ctx.EditMessage(m => { m.Components = GetConnectionsMessage(false, ctx.UserId, null, ctx.Language); });
            }
        }
    }
}
