using System.Text.Json.Serialization;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchAccessToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";
    [JsonPropertyName("state")]
    public string State { get; set; } = "";
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}
