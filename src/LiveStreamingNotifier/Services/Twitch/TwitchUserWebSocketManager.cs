using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveStreamingNotifier.Services.Twitch;
internal class TwitchUserWebSocketManager(
	ILogger<TwitchUserWebSocketManager> logger,
	IServiceProvider serviceProvider,
	TwitchAuth twitchAuth,
	TwitchApi twitchApi) : IAsyncDisposable
{
	private List<TwitchEventSubWebSocketClient> twitchWebSocketClients = new List<TwitchEventSubWebSocketClient>();

	public void Dispose()
	{
		DisposeAsync().AsTask().Wait();
	}
	public async ValueTask DisposeAsync()
	{
		foreach (var twitchWebSocketClient in twitchWebSocketClients.ToArray())
		{
			await twitchWebSocketClient.DisposeAsync().ConfigureAwait(false);
		}
		twitchWebSocketClients.Clear();
	}

	public async ValueTask RunAsync(CancellationToken cancellationToken)
	{
		await twitchAuth.AuthAsync(cancellationToken).ConfigureAwait(false);
		var users = await twitchApi.FetchUsersAsync([], "", cancellationToken: cancellationToken).ConfigureAwait(false);
		if (users.Users.Length <= 0)
		{
			throw new InvalidOperationException($"Failed UserInfo.");
		}
		var user = users.Users[0];

		while (!cancellationToken.IsCancellationRequested)
		{
			// Current
			var currentBroadcasterIds = new HashSet<string>();
			foreach (var twitchWebSocketClient in twitchWebSocketClients)
			{
				var subscriptions = twitchWebSocketClient.GetSubscriptions();
				if (twitchWebSocketClient.IsConnecting())
				{
					foreach (var id in subscriptions.Select(x => x.ConditionData.BroadcasterUserId))
					{
						currentBroadcasterIds.Add(id);
					}
				}
			}

			// Fetch
			var fetchChannels = await twitchApi.FetchAllFollowedChannels(
				user.Id,
				cancellationToken: cancellationToken).ConfigureAwait(false);
			var fetchChannelIds = fetchChannels.Select(x => x.BroadcasterId).ToFrozenSet();

			var deleteChannelIds = currentBroadcasterIds.Where(id => !fetchChannelIds.Contains(id)).ToArray();
			var newChannelIds = fetchChannelIds.Where(id => !currentBroadcasterIds.Contains(id)).ToArray();

			// Delete
			foreach (var deleteChannelId in deleteChannelIds)
			{
				foreach (var twitchWebSocketClient in twitchWebSocketClients)
				{
					if (twitchWebSocketClient.IsConnecting())
					{
						var unsubscribeResult = await twitchWebSocketClient.UnsubscribeStreamOnlineAsync(deleteChannelId, cancellationToken).ConfigureAwait(false);
						if (unsubscribeResult)
						{
							break;
						}
					}
				}
			}

			// Add
			static IEnumerable<TwitchEventSubWebSocketClient> GetActiveClients(
				IEnumerable<TwitchEventSubWebSocketClient> twitchWebSocketClients)
			{
				foreach (var twitchWebSocketClient in twitchWebSocketClients)
				{
					if (twitchWebSocketClient.IsCreateSubscription())
					{
						yield return twitchWebSocketClient;
					}
				}
			}

			static async ValueTask<bool> AddChannelAsync(
				IEnumerable<TwitchEventSubWebSocketClient> twitchWebSocketClients,
				string newChannelId,
				CancellationToken cancellationToken)
			{
				foreach (var twitchWebSocketClient in GetActiveClients(twitchWebSocketClients))
				{
					var subscribeResult = await twitchWebSocketClient.SubscribeStreamOnlineAsync(newChannelId, cancellationToken).ConfigureAwait(false);
					if (subscribeResult)
					{
						return true;
					}
				}
				return false;
			}

			var createClient = false;
			foreach (var newChannelId in newChannelIds)
			{
				var addResult = await AddChannelAsync(twitchWebSocketClients, newChannelId, cancellationToken).ConfigureAwait(false);
				if (!addResult)
				{
					createClient |= true;
				}
			}
			if (createClient && !GetActiveClients(twitchWebSocketClients).Any())
			{
				var newClient = serviceProvider.GetService<TwitchEventSubWebSocketClient>();
				if (newClient != null)
				{
					newClient.Start();
					twitchWebSocketClients.Add(newClient);
				}
			}

			// Clean
			foreach (var twitchWebSocketClient in twitchWebSocketClients.ToArray())
			{
				if (twitchWebSocketClient.IsClosed())
				{
					try
					{
						twitchWebSocketClients.Remove(twitchWebSocketClient);
						await twitchWebSocketClient.DisposeAsync();
					}
					catch
					{

					}
				}
			}

			// Delay
			await Task.Delay(
				createClient ? TimeSpan.FromSeconds(1.0) : TimeSpan.FromMinutes(1.0),
				cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}
}
