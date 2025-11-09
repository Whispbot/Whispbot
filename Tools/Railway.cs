using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools
{
    public static class Railway
    {
        public static async Task<int> getReplicaCount()
        {
            try
            {
                string query = @"
                query GetServiceReplicas($serviceId: String!, $environmentId: String!) {
                    serviceInstance(serviceId: $serviceId, environmentId: $environmentId) {
                        latestDeployment {
                            instances {
                                status
                            }
                        }
                    }
                }";

                var variables = new
                {
                    Config.serviceId,
                    Config.environmentId
                };

                var body = new
                {
                    query,
                    variables
                };

                string? token = Environment.GetEnvironmentVariable("RAILWAY_TOKEN");
                if (token is null)
                {
                    Log.Error($"RAILWAY_TOKEN environment variable not set, defaulting to 1 replica");
                    return 1;
                }

                using var client = new HttpClient();

                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                HttpResponseMessage response = await client.PostAsync(
                    "https://backboard.railway.com/graphql/v2",
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
                );

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);

                    return json["data"]?["serviceInstance"]?["latestDeployment"]?["instances"]?.Count() ?? 1;
                }
                else
                {
                    Log.Error($"Failed to fetch replica count from Railway: {response.StatusCode} - {response.ReasonPhrase}, defaulting to 1 replica");
                    return 1;
                }
            }
            catch
            {
                Log.Error("Failed to fetch replica count from Railway, defaulting to 1 replica");
                return 1;
            }
        }
    }
}
