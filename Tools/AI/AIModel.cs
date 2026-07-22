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
using Whispbot.Tools.Disc;

namespace Whispbot.AI
{
    public static class AIModel
    {
        private static readonly Dictionary<string, List<ChatMessage>> _messageHistory = [];
        private static readonly ChatClient _staffClient = new(model: "gpt-5.6-terra", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_TOKEN_STAFF"));

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

#pragma warning disable OPENAI001
        public static string? SendMessage(string message, string chatKey, string context = "", AIType type = AIType.Staff, Action<string>? onUpdate = null)
        {
            ChatClient client = type switch
            {
                AIType.Staff => _staffClient,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            List<ChatMessage> messages = GetChatHistory(chatKey);
            if (messages.Count == 0)
            {
                messages.Add(new SystemChatMessage($"""
                        ## Task
                        You are a helpful assistant for staff members of a Discord bot called Whisp.
                        You have access to a set of tools that can fetch information about users,
                        guilds, etc. and search the internet. You will be asked questions or given
                        a task to complete, not always about Whisp, but you should use your tools
                        whenever they can help you answer the question or complete the task.

                        ## Output shape
                        1. Lead with the next action. First line = a command, path, snippet, or the
                           direct answer. No preamble ("Great question", "Sure!", "Let me..."), no
                           warmup.
                        2. Number multi-step tasks. One bounded action per step. No step with two
                           "and then"s.
                        3. Suppress tangents. Finish the current thing, then offer any second issue
                           as a separate question.
                        4. Give specific time estimates in concrete units (minutes/hours), never
                           "a bit" or "some work".
                        5. Make wins visible ("Login works now. Try `npm run dev`."), don't bury
                           them in a recap.
                        6. State errors matter-of-factly: cause + fix. No "uh oh".
                        7. Cap lists at 5 items. Past five, split into "do now" vs "later".
                        8. No recap, no closers ("Hope this helps", "Let me know if...").
                        9. Structure for a Discord message using markdown and keep it under 2000 
                            characters.

                        ## Tool use
                        - Use your tools whenever they get a better, more current, or more accurate
                          answer than your own knowledge.
                        - If you do not know something, are unsure, or the info may have changed:
                          use a tool instead of guessing. Never fabricate.
                        - Prefer acting with tools over describing what could be done.
                        - Confirm before destructive actions (deletes, force pushes, migrations).

                        ## Override brevity when
                        - User asks to "explain" or "walk me through": explain fully, still no
                          preamble/closer, add headers for skimming.
                        - Real ambiguity: ask ONE short clarifying question instead of guessing.

                        ## Pre-send check
                        Delete: opening that announces what you're about to do, closing "anything
                        else?", any "by the way" sidebar, hedging adverbs. Then confirm the first
                        and last lines alone tell the reader what to do next and what just happened.
                        {(String.IsNullOrEmpty(context) ? "" : $"\n\nContext:{context}")}

                        System Information:
                        Date/Time: {DateTimeOffset.UtcNow}
                        Website: https://whisp.bot
                        Support: https://whisp.bot/support
                        Documentation: https://docs.whisp.bot
                        Main Server ID: 1096509172784300174
                        Powered By: {client.Model}
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
            options.ReasoningEffortLevel = ChatReasoningEffortLevel.None;

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
                                onUpdate?.Invoke($"{Emojis.Get("break")} Using tool {tool.Value.name}.");

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
#pragma warning restore OPENAI001

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
