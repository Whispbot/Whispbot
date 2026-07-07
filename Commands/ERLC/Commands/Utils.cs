using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;
using Whispbot.Tools.Games.ERLC;
using Whispbot.Tools.Games.ERLC.Classes;

namespace Whispbot.Commands.ERLCCommands.Commands
{
    public static class ERLCCommandUtils
    {
        public static async Task<string?> GetUserFromPartialName(string partialName, ERLCServerConfig serverConfig)
        {
            if (serverConfig.api_key is null || serverConfig.api_key is null || serverConfig.internal_id is null) return null;
            if (String.IsNullOrWhiteSpace(partialName)) return null;

            PRCResponse? response = await ERLC.GetERLCServer(serverConfig);

            if (response is null) return null;
            if (response.error == ErrorCode.Nothing && response.data is not null)
            {
                ERLCServer? server = ERLCRequest.ConvertResponseTo<ERLCServer>(response);
                if (server is null) return null;

                List<ERLCPlayer>? players = server.Players;
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

