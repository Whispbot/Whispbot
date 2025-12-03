using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools;

namespace Whispbot.Commands.ERLC.Commands
{
    public static class ERLCCommandUtils
    {
        public static async Task<string?> GetUserFromPartialName(string partialName, ERLCServerConfig serverConfig)
        {
            Tools.ERLC.PRC_Response? response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerPlayers, serverConfig.DecryptedApiKey) ?? await Tools.ERLC.GetPlayers(serverConfig);

            if (response is null) return null;
            if ((response.code == Tools.ERLC.ErrorCode.Success || response.code == Tools.ERLC.ErrorCode.Cached) && response.data is not null)
            {
                List<Tools.ERLC.PRC_Player>? players = JsonConvert.DeserializeObject<List<Tools.ERLC.PRC_Player>>(response.data.ToString()!);
                if (players is null) return null;

                Tools.ERLC.PRC_Player? matchedPlayer = players.FirstOrDefault(p => p.player.Contains(partialName, StringComparison.OrdinalIgnoreCase));
                if (matchedPlayer is not null)
                {
                    return matchedPlayer.player;
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
