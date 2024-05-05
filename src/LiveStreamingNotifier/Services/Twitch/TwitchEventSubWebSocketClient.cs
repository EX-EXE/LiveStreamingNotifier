using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using static LiveStreamingNotifier.Services.Twitch.TwitchMessage.Payload;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchEventSubWebSocketClient(
	ILogger<TwitchEventSubWebSocketClient> logger,
	IOptions<TwitchConfig> options,
	TwitchApi twitchApi) : TwitchWebSocketClient(logger, options)
{
	private List<TwitchApiResponseEventSubSubscriptions.Data> subscriptions = new();

	public int TotalCost { get; private set; } = 0;
	public int MaxTotalCost { get; private set; } = 0;

	public TwitchApiResponseEventSubSubscriptions.Data[] GetSubscriptions()
		=> subscriptions.ToArray();

	public bool IsCreateSubscription()
	{
		if (TotalCost == 0 && MaxTotalCost == 0)
		{
			return true;
		}
		if (MaxTotalCost <= TotalCost)
		{
			return false;
		}
		return IsReady();
	}

	public async ValueTask<bool> SubscribeStreamOnlineAsync(string broadcastUserId, CancellationToken cancellationToken)
	{
		if (!IsConnecting())
		{
			return false;
		}
		if (!TryGetSessionId(out var sessionId))
		{
			return false;
		}

		try
		{
			var result = await twitchApi.PostEventSubSubscriptionStreamOnlineAsync(
				broadcastUserId,
				sessionId,
				cancellationToken).ConfigureAwait(false);
			TotalCost = result.TotalCost;
			MaxTotalCost = 5;
			subscriptions.AddRange(result.Subscriptions);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
	public async ValueTask<bool> UnsubscribeStreamOnlineAsync(string broadcastUserId, CancellationToken cancellationToken)
	{
		if (!IsConnecting())
		{
			return false;
		}
		if (subscriptions.Count <= 0)
		{
			return false;
		}

		var subscription = subscriptions.FirstOrDefault(x => x.ConditionData.BroadcasterUserId.Equals(broadcastUserId, StringComparison.Ordinal));
		if (subscription == default)
		{
			return false;
		}

		var delete = await twitchApi.DeleteEventSubSubscriptionStreamOnlineAsync(
				subscription.ConditionData.BroadcasterUserId,
				cancellationToken).ConfigureAwait(false);
		if (delete)
		{
			subscriptions.Remove(subscription);
		}
		return delete;
	}
}