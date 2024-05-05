using System.Text.Json.Serialization;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchApiResponseChannels
{
    [JsonPropertyName("data")]
    public Data[] Channels { get; set; } = [];
    [JsonPropertyName("pagination")]
    public Pagination Page { get; set; } = new Pagination();

    [JsonPropertyName("total")]
    public int Total { get; set; } = 0;
    internal class Data
    {
        [JsonPropertyName("broadcaster_id")]
        public string BroadcasterId { get; set; } = "";
        [JsonPropertyName("broadcaster_login")]
        public string BroadcasterLogin { get; set; } = "";
        [JsonPropertyName("broadcaster_name")]
        public string BroadcasterName { get; set; } = "";
        [JsonPropertyName("followed_at")]
        public string FollowedAt { get; set; } = "";
    }
    internal class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = "";
    }

}
