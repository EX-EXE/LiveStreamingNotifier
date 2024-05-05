using System.Text.Json.Serialization;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchApiResponseStreams
{
    [JsonPropertyName("data")]
    public Data[] Streams { get; set; } = [];
    [JsonPropertyName("pagination")]
    public Pagination Page { get; set; } = new Pagination();

    internal class Data
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";
        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = "";
        [JsonPropertyName("user_name")]
        public string UserName { get; set; } = "";
        [JsonPropertyName("game_id")]
        public string GameId { get; set; } = "";
        [JsonPropertyName("game_name")]
        public string GameName { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
        [JsonPropertyName("viewer_count")]
        public int ViewerCount { get; set; } = 0;
        [JsonPropertyName("started_at")]
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
        [JsonPropertyName("language")]
        public string Language { get; set; } = "";
        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; } = "";
        [JsonPropertyName("tag_ids")]
        public string[] TagIds { get; set; } = [];
        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = [];
        [JsonPropertyName("is_mature")]
        public bool IsMature { get; set; } = false;
    }
    internal class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = "";
    }

}
