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
                    @"INSERT INTO user_config(id, roblox_id) VALUES (@1, @2) ON CONFLICT (id) DO UPDATE SET roblox_id = @2;",
                    [long.Parse(ctx.UserId), long.Parse(bloxlinkUser.RobloxID)]
                );

                if (updatedConfig is null) return;
                WhispCache.UserConfig.Insert(ctx.UserId, updatedConfig);
            }
        }
    }
}
