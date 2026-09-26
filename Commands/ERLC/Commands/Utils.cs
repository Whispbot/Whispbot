using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Tools;
using Whispbot.Tools.Games.ERLCAPI;
using Whispbot.Tools.Games.ERLCAPI.Classes;

namespace Whispbot.Commands.ERLC.Commands
{
    public static class ERLCCommandUtils
    {
        public static async Task<string?> GetUserFromPartialName(string partialName, ERLCServerConfig serverConfig)
        {
            if (serverConfig.api_key is null || serverConfig.internal_id is null) return null;
            if (String.IsNullOrWhiteSpace(partialName)) return null;

            PRCResponse? response = await ERLCAPI.GetERLCServer(serverConfig);

            if (response is null) return null;
            if (response.Server is not null)
            {
                List<ERLCPlayer>? players = response.Server.Players;
                if (players is null) return null;

                ERLCPlayer? matchedPlayer =
                    players.FirstOrDefault(p => p.Player.Split(':')[0] == partialName) ??
                    players.FirstOrDefault(p => p.Player.Contains(partialName, StringComparison.OrdinalIgnoreCase));

                if (matchedPlayer is not null)
                {
                    return matchedPlayer.Player;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
}

