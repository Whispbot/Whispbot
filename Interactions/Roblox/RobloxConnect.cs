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
    public class RobloxConnect: InteractionData
    {
        public override string CustomId => "connection_roblox";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferUpdate();
            Task _ = ctx.EditMessage(Commands.General.Connections.GetConnectionsMessage(true, ctx.UserId, null));

            var bloxlinkUser = await Tools.Bloxlink.RobloxFromDiscord(ctx.UserId);

            if (bloxlinkUser is null)
            {
                Task __ = ctx.Respond("{emoji.cross} {string.errors.connections.nobloxlink} https://blox.link.");
                Task ___ = ctx.EditMessage(Commands.General.Connections.GetConnectionsMessage(false, ctx.UserId, null));
                return;
            }
            else
            {
                var robloxUser = await Tools.Roblox.GetUserById(bloxlinkUser.RobloxID);
                if (ctx.interaction.message is not null) { Task __ = ctx.EditMessage(Commands.General.Connections.GetConnectionsMessage(false, ctx.UserId, robloxUser)); }

                UserConfig? updatedConfig = Postgres.SelectFirst<UserConfig>(
                    @"UPDATE user_config SET roblox_id = NULL WHERE roblox_id = @1; UPDATE user_config SET roblox_id = @1 WHERE id = @2 RETURNING *;",
                    [long.Parse(bloxlinkUser.RobloxID), long.Parse(ctx.UserId)]
                );

                if (updatedConfig is null) return;
                WhispCache.UserConfig.Insert(ctx.UserId, updatedConfig);
            }
        }
    }
}
