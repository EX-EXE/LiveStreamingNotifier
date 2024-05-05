using System.Text.Json.Serialization;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchMessage
{
    [JsonPropertyName("metadata")]
    public MetaData Meta { get; set; } = new MetaData();
    [JsonPropertyName("payload")]
    public Payload PayloadData { get; set; } = new Payload();

    internal class MetaData
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = "";
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; } = "";
        [JsonPropertyName("message_timestamp")]
        public DateTimeOffset MessageTimestamp { get; set; } = DateTimeOffset.MinValue;
    }
    internal class Payload
    {
        [JsonPropertyName("session")]
        public Session SessionData { get; set; } = new Session();
        [JsonPropertyName("subscription")]
        public Subscription SubscriptionData { get; set; } = new Subscription();
        [JsonPropertyName("event")]
        public Event EventData { get; set; } = new Event();

        internal class Session
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";
            [JsonPropertyName("status")]
            public string Status { get; set; } = "";
            [JsonPropertyName("connected_at")]
            public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.MinValue;
            [JsonPropertyName("keepalive_timeout_seconds")]
            public int KeepaliveTimeoutSeconds { get; set; } = 0;
            [JsonPropertyName("reconnect_url")]
            public string? ReconnectUrl { get; set; } = null;
        }
        internal class Subscription
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";
            [JsonPropertyName("status")]
            public string Status { get; set; } = "";
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";
            [JsonPropertyName("version")]
            public string Version { get; set; } = "";
            [JsonPropertyName("cost")]
            public int Cost { get; set; } = 0;
            [JsonPropertyName("condition")]
            public Condition ConditionData { get; set; } = new Condition();
            [JsonPropertyName("transport")]
            public Transport TransportData { get; set; } = new Transport();
            [JsonPropertyName("created_at")]
            public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;

            internal class Condition
            {
                [JsonPropertyName("broadcaster_user_id")]
                public string BroadcasterUserId { get; set; } = "";
            }
            internal class Transport
            {
                [JsonPropertyName("method")]
                public string Method { get; set; } = "";
                [JsonPropertyName("session_id")]
                public string SessionId { get; set; } = "";
            }
        }
        internal class Event
        {
            [JsonPropertyName("user_id")]
            public string User_id { get; set; } = "";
            [JsonPropertyName("user_login")]
            public string UserLogin { get; set; } = "";
            [JsonPropertyName("user_name")]
            public string UserName { get; set; } = "";
            [JsonPropertyName("broadcaster_user_id")]
            public string BroadcasterUserId { get; set; } = "";
            [JsonPropertyName("broadcaster_user_login")]
            public string BroadcasterUserLogin { get; set; } = "";
            [JsonPropertyName("broadcaster_user_name")]
            public string BroadcasterUserName { get; set; } = "";
            [JsonPropertyName("followed_at")]
            public DateTimeOffset FollowedAt { get; set; } = DateTimeOffset.MinValue;
        }
    }
}
