using System.Text.Json;
using System.Text.Json.Serialization;
using PiAiAssistant.Application.Chat;
using PiAiAssistant.Domain.Enums;

namespace PiAiAssistant.Tests;

public class ChatJsonContractTests
{
    private static readonly JsonSerializerOptions ControllerJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Persona_string_from_ui_deserializes()
    {
        const string json =
            """{"message":"How is the fleet?","persona":"Executive","language":"en","screenLevel":"kingdom"}""";

        var request = JsonSerializer.Deserialize<ChatAskRequest>(json, ControllerJson);

        Assert.NotNull(request);
        Assert.Equal("How is the fleet?", request!.Message);
        Assert.Equal(AssistantPersona.Executive, request.Persona);
        Assert.Equal("en", request.Language);
        Assert.Equal("kingdom", request.ScreenLevel);
    }

    [Fact]
    public void Answer_serializes_as_camelCase_answer()
    {
        var response = new ChatAskResponse(
            "cid",
            "Fleet loading is 75%.",
            [],
            [],
            Persona: AssistantPersona.Executive);

        var json = JsonSerializer.Serialize(response, ControllerJson);

        Assert.Contains("\"answer\":\"Fleet loading is 75%.\"", json);
        Assert.Contains("\"persona\":\"Executive\"", json);
        Assert.DoesNotContain("\"Answer\":", json);
    }

    [Fact]
    public void Persona_string_without_enum_converter_fails()
    {
        const string json = """{"message":"How is the fleet?","persona":"Executive"}""";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ChatAskRequest>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
