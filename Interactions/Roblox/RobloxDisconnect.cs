using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Roblox
{
    public class RobloxDisconnect: InteractionData
    {
        public override string CustomId => "disconnect_roblox";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null) return;
            if (await ctx.CheckAllowed()) return;

            Task _ = ctx.DeferUpdate();

            UserConfig? updatedConfig = Postgres.SelectFirst<UserConfig>(
                @"UPDATE user_config SET roblox_id = NULL WHERE id = @1 RETURNING *;",
                [long.Parse(ctx.UserId)]
            );

            if (updatedConfig is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.disconnect.failedroblox}.", true);
            }
            else
            {
                await ctx.EditMessage(Commands.General.Connections.GetConnectionsMessage(false, ctx.UserId, null));
            }
        }
    }
}
