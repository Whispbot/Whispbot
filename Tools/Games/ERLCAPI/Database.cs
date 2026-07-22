using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Cache;
using Whispbot.Commands;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCDatabase
    {
        public static ERLCServerConfig? GetServerFromString(IEnumerable<ERLCServerConfig> servers, string str)
        {
            if (String.IsNullOrWhiteSpace(str))
            {
                return servers.FirstOrDefault(s => s.is_default);
            }
            else
            {
                var server = servers.FirstOrDefault(s => s.name?.Contains(str, StringComparison.CurrentCultureIgnoreCase) ?? false);
                server ??= servers.FirstOrDefault(s => s.code?.Contains(str, StringComparison.CurrentCultureIgnoreCase) ?? false);

                return server;
            }
        }

        public static async Task<ERLCServerConfig?> TryGetServer(CommandContext ctx)
        {
            List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

            if (servers is null || servers.Count == 0)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.noservers")}");
                return null;
            }

            string searchString = ctx.args.Get("server")?.GetString() ?? "";

            if (String.IsNullOrWhiteSpace(searchString))
            {
                ERLCServerConfig? defaultServer = servers.Count == 1 ? servers[0] : servers.FirstOrDefault(s => s.is_default);
                if (defaultServer is not null)
                {
                    return defaultServer;
                }
                else
                {
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.nodefault")}");
                    return null;
                }
            }

            ERLCServerConfig? server = GetServerFromString(servers, searchString);

            if (server is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.erlcserver.notfound")}");
                return null;
            }

            if (server.api_key is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.erlcserver.nokey")}");
                return null;
            }

            return server;
        }
    }
}
