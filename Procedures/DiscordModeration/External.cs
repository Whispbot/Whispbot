using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Websocket.Events;

namespace Whispbot
{
    public static partial class DiscordModeration
    {
        public static async Task<string?> FromContext(Context context)
        {
            var guild = context.Guild;
            var moderator = context.Moderator;
            var target = context.TargetUser;

            if (guild is null || moderator is null || target is null)
            {
                return "{string.errors.dm.invalid_ctx}";
            }

            // Make sure user has correct permissions and the module is enabled
            var permissionCheck = await HasPermission(context);
            if (!permissionCheck.Item1)
            {
                return permissionCheck.Item2;
            }

            var (newCase, transaction) = await CreateCase(context);

            if (newCase is null)
            {
                transaction?.Rollback();
                return "{string.errors.dm.failed_create_case}";
            }

            var typeData = TypeData[context.Type!.Value];
            if (typeData.Item6 is not null)
            {
                try
                {
                    var errorMessage = await typeData.Item6(context);

                    if (errorMessage is not null)
                    {
                        transaction?.Rollback();
                        return errorMessage;
                    }
                }
                catch
                {
                    transaction?.Rollback();
                    return "{string.errors.dm.action_failed}";
                }
            }

            transaction?.Commit();

            var userMessage = await SendUserMessage(newCase);
            _ = Task.Run(() => Log(newCase));

            return null;
        }

        //public static Task OnBanAdd(GuildBan ban)
        //{
        //    var guild = DiscordCache.Guilds.Get(ban.guild_id!);

        //    Context context = new Context(ban.user, "", -1, DiscordCache.Guilds.Get(ban.guild_id!));
        //}
    }
}
