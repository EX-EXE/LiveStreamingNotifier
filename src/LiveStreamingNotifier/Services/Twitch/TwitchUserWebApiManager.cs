using Microsoft.Extensions.Caching.Memory;
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

internal class TwitchStreamInfo
{
	public required TwitchApiResponseStreams.Data StreamInfo { get; init; }
	public required TwitchApiResponseUsers.Data UserInfo { get; init; }
	public required string UserImageCacheFile { get; init; }
	public required string StreamImageCacheFile { get; init; }
}

internal class TwitchUserWebApiManager(
	ILogger<TwitchUserWebApiManager> logger,
	IServiceProvider serviceProvider,
	IMemoryCache memoryCache,
	TwitchAuth twitchAuth,
	TwitchApi twitchApi,
	WebImageCacheService webImageCacheService) : IAsyncDisposable
{
	public event Action<TwitchStreamInfo>? OnOnlineStream = null;

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

		var streamIds = FrozenSet<string>.Empty;
		while (!cancellationToken.IsCancellationRequested)
		{
			// チャンネル
			var fetchChannels = await twitchApi.FetchAllFollowedChannels(
				user.Id,
				cancellationToken: cancellationToken).ConfigureAwait(false);
			var fetchChannelIds = fetchChannels.Select(x => x.BroadcasterId).ToFrozenSet();

			// Fetch Streams
			var fetchFollowedStreams = await twitchApi.FetchAllFollowedStreams(
				user.Id,
				first: 100,
				cancellationToken: cancellationToken).ConfigureAwait(false);
			var newFollowedStreams = fetchFollowedStreams.Where(x => !streamIds.Contains(x.Id)).ToArray();
			streamIds = fetchFollowedStreams.Select(x => x.Id).ToFrozenSet();
			var fetchUserIds = newFollowedStreams
				.Select(x => x.UserId)
				.Where(x => !memoryCache.TryGetValue(x, out _))
				.ToArray();

			// User Profile
			foreach (var chunkUserIds in fetchUserIds.Chunk(100))
			{
				var fetchUsers = await twitchApi.FetchUsersAsync(
					chunkUserIds,
					cancellationToken: cancellationToken).ConfigureAwait(false);
				foreach (var fetchUser in fetchUsers.Users)
				{
					memoryCache.Set(fetchUser.Id, fetchUser, new MemoryCacheEntryOptions()
					{
						SlidingExpiration = TimeSpan.FromHours(1.0)
					});
				}
			}

			// Event
			foreach (var newFollowedStream in newFollowedStreams)
			{
				if (memoryCache.TryGetValue<TwitchApiResponseUsers.Data>(newFollowedStream.UserId, out var userInfo) &&
					userInfo != null)
				{
					var userImageTask = webImageCacheService.DownloadAsync(userInfo.ProfileImageUrl, false, cancellationToken);
					var streamImageTask = webImageCacheService.DownloadAsync(
						newFollowedStream.ThumbnailUrl.Replace("{width}", "480").Replace("{height}", "270"), true, cancellationToken);
					var images = await Task.WhenAll(userImageTask.AsTask(), streamImageTask.AsTask()).ConfigureAwait(false);

					var streamInfo = new TwitchStreamInfo()
					{
						StreamInfo = newFollowedStream,
						UserInfo = userInfo,
						UserImageCacheFile = images[0],
						StreamImageCacheFile = images[1],
					};
					OnOnlineStream?.Invoke(streamInfo);
				}
			}


			// Delay
			await Task.Delay(
				TimeSpan.FromMinutes(1.0),
				cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}
}
