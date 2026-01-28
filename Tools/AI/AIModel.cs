using Newtonsoft.Json;
using OpenAI.Chat;
using OpenAI.Responses;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot.AI
{
    public static class AIModel
    {
        private static readonly Dictionary<string, List<ChatMessage>> _messageHistory = [];
        private static readonly ChatClient _staffClient = new(model: "gpt-5.2", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_TOKEN_STAFF"));

        private static List<ChatMessage> GetChatHistory(string key)
        {
            if (_messageHistory.TryGetValue(key, out var messages))
            {
                return messages;
            }
            else return [];
        }

        private static void SaveChatHistory(string key, List<ChatMessage> messages)
        {
            _messageHistory[key] = messages;
        }

        public static readonly List<Tool> StaffTools = [
            new()
            {
                name = "getGuildData",
                description = "Fetches data about a guild using its ID.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "guildId": {
                            "type": "string",
                            "description": "The ID of the guild to fetch data for."
                        }
                    },
                    "required": ["guildId"]
                }
                """,
                function = AIStaffTools.GetGuildData
            },
            new()
            {
                name = "searchInternet",
                description = "Searches the internet using google, meaning you can use google search formatting, and returns the results.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The search query to use."
                        },
                        "count": {
                            "type": "integer",
                            "description": "The number of results to return (min 1, default 10, max 50).",
                            "default": 10
                        },
                        "start": {
                            "type": "integer",
                            "description": "The result number to start from (default 1).",
                            "default": 1
                        }
                    },
                    "required": ["query"]
                }
                """,
                function = AIStaffTools.SearchInternet
            },
            new()
            {
                name = "searchWhisp",
                description = "Searches all Whisp related domains using the google search engine api and returns the relevant results. If you get no results on the first search, try just searching a keyword which may turn up more results.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The search query to use."
                        },
                        "count": {
                            "type": "integer",
                            "description": "The number of results to return (min 1, default 10, max 50).",
                            "default": 10
                        },
                        "start": {
                            "type": "integer",
                            "description": "The result number to start from (default 1).",
                            "default": 1
                        }
                    },
                    "required": ["query"]
                }
                """,
                function = AIStaffTools.SearchWhisp
            },
            new()
            {
                name = "getUserData",
                description = "Fetches data about a user using their ID.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "userId": {
                            "type": "string",
                            "description": "The ID of the user to fetch data for."
                        }
                    },
                    "required": ["userId"]
                }
                """,
                function = AIStaffTools.GetUserData
            },
            new()
            {
                name = "getMemberData",
                description = "Fetches data about a guild member using the guild ID and user ID.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "guildId": {
                            "type": "string",
                            "description": "The ID of the guild to fetch the member from."
                        },
                        "userId": {
                            "type": "string",
                            "description": "The ID of the user to fetch data for."
                        }
                    },
                    "required": ["guildId", "userId"]
                }
                """,
                function = AIStaffTools.GetMemberData
            },
            new()
            {
                name = "getChannelData",
                description = "Fetches data about a channel using its ID.",
                parameters = """
                {
                    "type": "object",
                    "properties": {
                        "channelId": {
                            "type": "string",
                            "description": "The ID of the channel to fetch data for."
                        }
                    },
                    "required": ["channelId"]
                }
                """,
                function = AIStaffTools.GetChannelData
            }
        ];

        public static string? SendMessage(string message, string chatKey, string context = "", AIType type = AIType.Staff, Action<string>? onUpdate = null)
        {
            List<ChatMessage> messages = GetChatHistory(chatKey);
            if (messages.Count == 0)
            {
                messages.Add(new SystemChatMessage($"""
                        You are a helpful assistant that answers questions and provides information based on the input provided for staff members of the Discord bot 'Whisp'.
                        This response will be sent to a Discord channel, so make sure that your response is formatted as such (can contain markdown and code blocks) and is kept snappy and concise, but informative.
                        You do not have to be professional, you should be friendly and approachable but you should never use emojis.
                        NEVER guess anything, if you do not know the answer to something, do not mention it or just say that you do not know. You may ask questions to clarify the request, but do not make assumptions.
                        You should use tool calls to perform actions, such as fetching data from the database, searching the internet or whisp website or performing other tasks. If you do not get the information that you wanted on the first try, give another query a go to get more accurate data. Using single keyword queries with the whisp search tool normally helps.
                        {(String.IsNullOrEmpty(context) ? "" : $"\n\nContext:{context}")}

                        System Information:
                        Date/Time: {DateTimeOffset.UtcNow}
                        Website: https://whisp.bot
                        Support: https://whisp.bot/support
                        Documentation: https://docs.whisp.bot
                        Main Server ID: 1096509172784300174
                    """));
            }
            messages.Add(new UserChatMessage(message));

            List<Tool> Tools = type switch
            {
                AIType.Staff => StaffTools,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            List<ChatTool> chatTools = [.. Tools.Select(tool => ChatTool.CreateFunctionTool(
                tool.name,
                tool.description,
                tool.parameters is not null ? BinaryData.FromString(tool.parameters, "UTF8") : null
             ))];

            ChatCompletionOptions options = new();
            foreach (ChatTool tool in chatTools)
            {
                options.Tools.Add(tool);
            }
            options.Metadata.Add("chat-key", chatKey);
            options.StoredOutputEnabled = true;

            ChatClient client = type switch
            {
                AIType.Staff => _staffClient,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            bool requiresAction = false;

            do
            {
                requiresAction = false;
                ChatCompletion completion = client.CompleteChat(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(completion));
                        break;

                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(completion));

                        foreach (ChatToolCall toolCall in completion.ToolCalls)
                        {
                            Tool? tool = Tools.FirstOrDefault(t => t.name == toolCall.FunctionName);
                            if (tool is not null)
                            {
                                onUpdate?.Invoke($"{{emoji.break}} Using tool {tool.Value.name}.");

                                JsonDocument arguments = JsonDocument.Parse(toolCall.FunctionArguments);
                                string result = tool.Value.function(arguments);
                                messages.Add(new ToolChatMessage(toolCall.Id, result));
                            }
                            else
                            {
                                messages.Add(new ToolChatMessage(toolCall.Id, $"Tool '{toolCall.FunctionName}' not found."));
                            }
                        }

                        requiresAction = true;
                        break;

                    case ChatFinishReason.Length:
                        return "Ran out of tokens...";

                    case ChatFinishReason.ContentFilter:
                        return "Content filtered by OpenAI's content filter.";
                }
            }
            while (requiresAction);

            SaveChatHistory(chatKey, messages);

            return messages.Last().Content[0].Text;
        }

        public enum AIType
        {
            Staff
        }

        public struct Tool
        {
            public string name;
            public string description;
            public string? parameters;

            public Func<JsonDocument, string> function;
        }
    }
}
