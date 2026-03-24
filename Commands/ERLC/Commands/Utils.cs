using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;

namespace Whispbot.Commands.ERLCCommands.Commands
{
    public static class ERLCCommandUtils
    {
        public static async Task<string?> GetUserFromPartialName(string partialName, ERLCServerConfig serverConfig)
        {
            if (serverConfig.api_key is null || serverConfig.DecryptedApiKey == "") return null;

            ERLC.PRC_APIResponse? response = ERLC.CheckCache(serverConfig.DecryptedApiKey) ?? await ERLC.GetServerV2(serverConfig.DecryptedApiKey);

            if (response is null) return null;
            if ((response.Code == ERLC.ErrorCode.Success || response.Code == ERLC.ErrorCode.Cached) && response.Data is not null)
            {
                List<ERLC.PRC_Player>? players = response.Data.Players;
                if (players is null) return null;

                ERLC.PRC_Player? matchedPlayer =
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

