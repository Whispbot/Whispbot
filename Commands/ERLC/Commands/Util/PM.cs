using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands.Commands.Debug
{
    public class PMUsers: ERLCCommand
    {
        public override string Name => "Private Message (Using VSM)";
        public override string Description => "Use Virtual Server Management to pm users";
        public override List<string> Aliases => ["pm", "privatemessage", "send"];
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(ERLCCommandContext ctx)
        {
            if (ctx.GuildId is null || ctx.UserId is null) return;

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{string.errors.erlccommand.pm.missinguser}");
                return;
            }

            if (ctx.args.Count < 2)
            {
                await ctx.Reply("{string.errors.erlccommand.pm.missingmessage}");
                return;
            }

            if (!await WhispPermissions.HasPermission(ctx.GuildId, ctx.UserId, BotPermissions.ERLCAdmin | BotPermissions.ERLCOWner))
            {
                await ctx.Reply("{string.errors.erlccommand.pm.nopermission}");
                return;
            }

            List<string> pmUsers = [];

            string usernames = ctx.args[0].ToLower();
            string reciever = "";
            ctx.args.RemoveAt(0);

            if (new List<string> { "@od", "@onduty" }.Contains(usernames))
            {
                reciever = "@onduty";

                List<Shift>? activeShifts = Postgres.Select<Shift>("SELECT * FROM shifts WHERE guild_id = @1 AND end_time IS NULL", [long.Parse(ctx.GuildId)]);

                if (activeShifts != null)
                {
                    List<long> activeIds = activeShifts.Select(s => s.moderator_id).Distinct().ToList();
                    List<UserConfig> userConfigs = WhispCache.UserConfig.FindMany((u,_) => activeIds.Contains(u.id));
                    if (userConfigs.Count < activeIds.Count)
                    {
                        List<UserConfig>? otherConfigs = Postgres.Select<UserConfig>("SELECT * FROM user_config WHERE id = ANY(@1) AND roblox_id IS NOT NULL", [activeIds.Where(id => !userConfigs.Any(u => u.id == id)).ToList()]);
                        if (otherConfigs != null)
                        {
                            userConfigs.AddRange(otherConfigs);
                            foreach (var config in otherConfigs)
                            {
                                WhispCache.UserConfig.Insert(config.id.ToString(), config);
                            }
                        }
                    }

                    List<string> robloxIds = [..userConfigs.Select(uc => uc.roblox_id.ToString()!)];
                    List<Roblox.RobloxUser>? robloxUsers = await Roblox.GetUserById(robloxIds);
                    if (robloxUsers != null)
                    {
                        pmUsers.AddRange([..robloxUsers.Select(ru => ru.name)]);
                    }
                }
            }
            else
            {
                pmUsers.AddRange([..usernames.Split(",")]);
                reciever = pmUsers.Count > 1 ? $"{pmUsers.Count} people" : "You";
            }

            if (pmUsers.Count == 0)
            {
                await ctx.Reply("{string.errors.erlccommand.pm.nousers}");
            }

            await Tools.ERLC.SendCommand(ctx.server, $":pm {pmUsers.Join(",")} {ctx.robloxUsername} to {reciever}: {ctx.args.Join(" ")}");
        }
    }
}
