using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLC.Classes
{
    public class ERLCStaff
    {
        [JsonConverter(typeof(EmptyArrayAsDictionaryConverter))]
        public Dictionary<string, string> Admins { get; init; } = [];
        [JsonConverter(typeof(EmptyArrayAsDictionaryConverter))]
        public Dictionary<string, string> Mods { get; init; } = [];
        [JsonConverter(typeof(EmptyArrayAsDictionaryConverter))]
        public Dictionary<string, string> Helpers { get; init; } = [];
    }

    public class EmptyArrayAsDictionaryConverter : JsonConverter<Dictionary<string, string>>
    {
        public override Dictionary<string, string>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, string>? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var token = Newtonsoft.Json.Linq.JToken.Load(reader);
            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                return [];
            return token.ToObject<Dictionary<string, string>>();
        }

        public override void WriteJson(JsonWriter writer, Dictionary<string, string>? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
