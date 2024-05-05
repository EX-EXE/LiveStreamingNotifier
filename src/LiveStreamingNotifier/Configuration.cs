using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static LiveStreamingNotifier.TwitchConfig;

namespace LiveStreamingNotifier;
internal class Configuration
{
	public TwitchConfig Twitch { get; init; } = new TwitchConfig();
}

public class TwitchConfig
{
	public AuthenticationConfig Authentication { get; init; } = new AuthenticationConfig();
	public EventSubConfig EventSub { get; init; } = new EventSubConfig();

	public class AuthenticationConfig
	{
		public string Uri { get; init; } = "https://id.twitch.tv/oauth2/authorize";
		public string ClientId { get; init; } = "";
		public string ResponseType { get; init; } = "token";
		public string Scope { get; init; } = "";
	}
	public class EventSubConfig
	{
		public string Uri { get; init; } = "wss://eventsub.wss.twitch.tv/ws";
		public int KeepAliveTimeoutSeconds { get; init; } = 30;
	}


}
