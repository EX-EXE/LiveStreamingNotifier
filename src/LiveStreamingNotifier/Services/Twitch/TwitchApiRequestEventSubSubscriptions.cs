using System.Text.Json.Serialization;
using static LiveStreamingNotifier.Services.Twitch.TwitchMessage.Payload.Subscription;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchApiRequestEventSubSubscriptions
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("condition")]
    public Condition ConditionData { get; set; } = new Condition();
    [JsonPropertyName("transport")]
    public Transport TransportData { get; set; } = new Transport();

    internal class Condition
    {
        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; } = "";
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";
    }
    internal class Transport
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = "";
        [JsonPropertyName("callback")]
        public string Callback { get; set; } = "";
        [JsonPropertyName("secret")]
        public string Secret { get; set; } = "";
        [JsonPropertyName("session_id")]
        public string SessionId { get; set; } = "";
        [JsonPropertyName("conduit_id")]
        public string ConduitId { get; set; } = "";
    }

}
