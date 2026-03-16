using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using YellowMacaroni.Discord.Sharding;
using YellowMacaroni.Discord.Websocket.Events;
using static Sentry.MeasurementUnit;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        private static readonly Dictionary<AuditActionType, DiscordModerationType> _moderationTypes = new()
        {
            { AuditActionType.MemberBanAdd, DiscordModerationType.Ban },
            { AuditActionType.MemberBanRemove, DiscordModerationType.Unban },
            { AuditActionType.MemberKick, DiscordModerationType.Kick },
            { AuditActionType.MemberUpdate, DiscordModerationType.Mute }
        };

        public static void RegisterClient(Client client)
        {
            client.GuildAuditLogEntryCreate += async (_, log) =>
            {
                Logger.WithData(log).Information("Recieved audit log:");
                if (log.action_type is null) return;
                if (!_moderationTypes.ContainsKey(log.action_type.Value)) return; // Not an audit log we care about

                // Bot already logs its own actions so ignore from events to avoid duplicates
                if (log.user_id == client.readyData?.user?.id) return;

                var mType = _moderationTypes[(AuditActionType)log.action_type];
                var duration = -1L;
                if (mType == DiscordModerationType.Mute)
                {
                    var change = log.changes?.FirstOrDefault(c => c.key == "communication_disabled_until");
                    if (change is null) return; // Only need to worry about changing mute duration

                    if (change.new_value is null)
                    {
                        mType = DiscordModerationType.Unmute;
                    }
                    else if (change.new_value is DateTime dt)
                    {
                        // Ceiling otherwise we end up with e.g. 59 minutes, 59 seconds instead of 1 hour
                        duration = (long)Math.Ceiling((dt - DateTime.UtcNow).TotalSeconds);
                    }
                }

                var guild = await DiscordCache.Guilds.Get(log.guild_id);
                if (guild is null) return;

                var moderator = await DiscordCache.Users.Get(log.user_id!);
                if (moderator is null) return; // Couldnt find moderator user, cry about it

                var target = await DiscordCache.Users.Get(log.target_id!);
                if (target is null) return;

                var context = new Context(
                    target,
                    log.reason ?? "*No reason provided.*",
                    duration,
                    guild,
                    moderator,
                    mType,
                    null
                );

                var (mcase, transaction) = await CreateCase(context);
                if (mcase is null)
                {
                    transaction?.Rollback();
                    return;
                }
                else
                {
                    transaction?.Commit();
                }

                await Log(mcase);
            };
        }

        public static void RegisterClient(ShardingManager manager)
        {
            foreach (var shard in manager.shards)
            {
                RegisterClient(shard.client);
            }
        }
    }
}
