using System.Text.Json.Serialization;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchApiResponseEventSubSubscriptions
{
    [JsonPropertyName("data")]
    public Data[] Subscriptions { get; set; } = [];

	[JsonPropertyName("total")]
    public int Total { get; set; } = 0;
    [JsonPropertyName("total_cost")]
    public int TotalCost { get; set; } = 0;
    [JsonPropertyName("max_total_cost")]
    public int MaxTotalCost { get; set; } = 0;




    internal class Data
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
        [JsonPropertyName("condition")]
        public Condition ConditionData { get; set; } = new Condition();
        [JsonPropertyName("transport")]
        public Transport TransportData { get; set; } = new Transport();
    }
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
        [JsonPropertyName("connected_at")]
        public string ConnectedAt { get; set; } = "";
    }

}
