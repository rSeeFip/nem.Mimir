namespace Mimir.Infrastructure.Tests.LiteLlm;

using System.Text.Json;
using Mimir.Infrastructure.LiteLlm;
using Shouldly;

public sealed class ToolCallingSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Serialize_RequestWithTools_IncludesToolsArray()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = [new ChatMessageRequest { Role = "user", Content = "Hello" }],
            Stream = false,
            Tools =
            [
                new ToolDefinitionRequest
                {
                    Function = new FunctionDefinitionRequest
                    {
                        Name = "get_weather",
                        Description = "Get current weather",
                        Parameters = JsonDocument.Parse("""{"type":"object","properties":{"location":{"type":"string"}}}""").RootElement,
                    },
                },
            ],
            ToolChoice = "auto",
        };

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"tools\":");
        json.ShouldContain("\"get_weather\"");
        json.ShouldContain("\"tool_choice\":\"auto\"");
        json.ShouldContain("\"type\":\"function\"");
        json.ShouldContain("\"description\":\"Get current weather\"");
    }

    [Fact]
    public void Serialize_RequestWithoutTools_OmitsToolsField()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = [new ChatMessageRequest { Role = "user", Content = "Hello" }],
            Stream = false,
        };

        var json = JsonSerializer.Serialize(request);

        json.ShouldNotContain("\"tools\"");
        json.ShouldNotContain("\"tool_choice\"");
    }

    [Fact]
    public void Deserialize_ResponseWithToolCalls_ParsesCorrectly()
    {
        const string json = """
        {
            "id": "chatcmpl-123",
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc123",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"location\":\"London\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<ChatCompletionResponse>(json);

        response.ShouldNotBeNull();
        response.Choices.ShouldNotBeNull();
        response.Choices.Count.ShouldBe(1);

        var message = response.Choices[0].Message;
        message.ShouldNotBeNull();
        message.ToolCalls.ShouldNotBeNull();
        message.ToolCalls.Count.ShouldBe(1);

        var toolCall = message.ToolCalls[0];
        toolCall.Id.ShouldBe("call_abc123");
        toolCall.Type.ShouldBe("function");
        toolCall.Function.Name.ShouldBe("get_weather");
        toolCall.Function.Arguments.ShouldBe("{\"location\":\"London\"}");
    }

    [Fact]
    public void Deserialize_StreamingDeltaWithToolCalls_ParsesCorrectly()
    {
        const string json = """
        {
            "id": "chatcmpl-456",
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "delta": {
                        "role": "assistant",
                        "tool_calls": [
                            {
                                "index": 0,
                                "id": "call_xyz789",
                                "type": "function",
                                "function": {
                                    "name": "search",
                                    "arguments": "{\"q\":"
                                }
                            }
                        ]
                    },
                    "finish_reason": null
                }
            ]
        }
        """;

        var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json);

        chunk.ShouldNotBeNull();
        chunk.Choices.ShouldNotBeNull();
        chunk.Choices.Count.ShouldBe(1);

        var delta = chunk.Choices[0].Delta;
        delta.ShouldNotBeNull();
        delta.ToolCalls.ShouldNotBeNull();
        delta.ToolCalls.Count.ShouldBe(1);

        var toolCallDelta = delta.ToolCalls[0];
        toolCallDelta.Index.ShouldBe(0);
        toolCallDelta.Id.ShouldBe("call_xyz789");
        toolCallDelta.Type.ShouldBe("function");
        toolCallDelta.Function.ShouldNotBeNull();
        toolCallDelta.Function.Name.ShouldBe("search");
        toolCallDelta.Function.Arguments.ShouldBe("{\"q\":");
    }

    [Fact]
    public void Deserialize_ResponseWithoutToolCalls_ToolCallsIsNull()
    {
        const string json = """
        {
            "id": "chatcmpl-789",
            "model": "gpt-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "Hello! How can I help?"
                    },
                    "finish_reason": "stop"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<ChatCompletionResponse>(json);

        response.ShouldNotBeNull();
        response.Choices.ShouldNotBeNull();

        var message = response.Choices[0].Message;
        message.ShouldNotBeNull();
        message.Content.ShouldBe("Hello! How can I help?");
        message.ToolCalls.ShouldBeNull();
    }

    [Fact]
    public void Serialize_ToolResultMessage_IncludesToolCallIdAndName()
    {
        var message = new ChatMessageRequest
        {
            Role = "tool",
            Content = "{\"temperature\": 22}",
            ToolCallId = "call_abc123",
            Name = "get_weather",
        };

        var json = JsonSerializer.Serialize(message);

        json.ShouldContain("\"tool_call_id\":\"call_abc123\"");
        json.ShouldContain("\"name\":\"get_weather\"");
        json.ShouldContain("\"role\":\"tool\"");
    }

    [Fact]
    public void Serialize_RegularMessage_OmitsToolCallIdAndName()
    {
        var message = new ChatMessageRequest
        {
            Role = "user",
            Content = "Hello",
        };

        var json = JsonSerializer.Serialize(message);

        json.ShouldNotContain("\"tool_call_id\"");
        json.ShouldNotContain("\"name\"");
    }
}
