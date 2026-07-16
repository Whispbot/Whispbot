using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Cache;
using Whispbot.Commands;
using Whispbot.Tools.Games.ERLCAPI.Classes;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCAPI
    {
        public static async Task<PRCResponse?> GetERLCServer(CommandContext ctx, ERLCServerConfig server)
        {
            using var _ = Tracer.Start($"ERLC.GetServerV2");

            var response = await ERLCCache.GetCache(new PRCRequest // Fake request to check if the server is cached
            {
                method = "GET",
                endpoint = "/v2/server?Players=true&Staff=true&JoinLogs=true&Queue=true&KillLogs=true&CommandLogs=true&ModCalls=true&EmergencyCalls=true&Vehicles=true",
                serverId = server.internal_id
            });

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlc.fetching}...");
                response = await GetERLCServer(server);

                if (response is null)
                {
                    await ctx.EditResponse(m => m.Content = "{emoji.cross} {string.errors.erlcserver.apierror}");
                    return null;
                }
            }

            if (Errors.ResponseHasError(response, out var errorMessage))
            {
                await ctx.EditResponse(m => { m.Components = errorMessage!; m.Flags = MessageFlags.ComponentsV2; });
                return null;
            }

            return response;
        }

        public static async Task<PRCResponse?> GetERLCServer(ERLCServerConfig server)
        {
            return await ERLCRequest.Request(
                "GET",
                "/v2/server?Players=true&Staff=true&JoinLogs=true&Queue=true&KillLogs=true&CommandLogs=true&ModCalls=true&EmergencyCalls=true&Vehicles=true",
                server.internal_id,
                server.api_key
            );
        }

        public static async Task<PRCResponse?> SendCommand(ERLCServerConfig server, string command)
        {
            return await ERLCRequest.Request(
                "POST",
                "/v2/server/command",
                server.internal_id,
                server.api_key,
                new { command }
            );
        }
    }
}
