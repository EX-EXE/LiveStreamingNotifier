using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Web;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchApi(
	ILogger<TwitchApi> logger,
	IOptions<TwitchConfig> options,
	TwitchAuth twitchAuth,
	IHttpClientFactory httpClientFactory)
{
	private readonly TwitchConfig config = options.Value;

	public async ValueTask<TwitchApiResponseUsers> FetchUsersAsync(
		string[] ids,
		string login = "",
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/users");

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		foreach (var id in ids)
		{
			query.Add("id", id);
		}
		if (!string.IsNullOrEmpty(login))
		{
			query["login"] = login;
		}

		uriBuilder.Query = query.ToString();

		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var jsonResponse = await response.Content.ReadFromJsonAsync<TwitchApiResponseUsers>(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (jsonResponse == null)
		{
			throw new InvalidOperationException($"Failed Deserialize.");
		}
		return jsonResponse;
	}

	public async ValueTask<TwitchApiResponseEventSubSubscriptions> PostEventSubSubscriptionStreamOnlineAsync(
		string broadcasterUserId,
		string sessionId,
		CancellationToken cancellationToken)
	{
		var request = new TwitchApiRequestEventSubSubscriptions()
		{
			Type = "stream.online",
			Version = "1",
			ConditionData = new TwitchApiRequestEventSubSubscriptions.Condition()
			{
				BroadcasterUserId = broadcasterUserId,
			},
			TransportData = new TwitchApiRequestEventSubSubscriptions.Transport()
			{
				Method = "websocket",
				SessionId = sessionId,
			}
		};

		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.PostAsJsonAsync(
			"https://api.twitch.tv/helix/eventsub/subscriptions",
			request,
			cancellationToken: cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var jsonResponse = await response.Content.ReadFromJsonAsync<TwitchApiResponseEventSubSubscriptions>(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (jsonResponse == null)
		{
			throw new InvalidOperationException($"Failed Deserialize.");
		}
		return jsonResponse;
	}

	public async ValueTask<bool> DeleteEventSubSubscriptionStreamOnlineAsync(
		string id,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/eventsub/subscriptions");

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		query["id"] = id;
		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.DeleteAsync(
			uriBuilder.Uri,
			cancellationToken: cancellationToken).ConfigureAwait(false);
		return response.IsSuccessStatusCode ? true : false;
	}



	public async ValueTask<TwitchApiResponseChannels.Data[]> FetchAllFollowedChannels(
		string userId,
		string broadcasterId = "",
		CancellationToken cancellationToken = default)
	{
		var result = new List<TwitchApiResponseChannels.Data>();
		var after = string.Empty;
		while (true)
		{
			var response = await FetchFollowedChannels(userId, broadcasterId, first: 0, after: after, cancellationToken: cancellationToken).ConfigureAwait(false);
			result.AddRange(response.Channels);

			if (!string.IsNullOrEmpty(response.Page.Cursor))
			{
				after = response.Page.Cursor;
			}
			else
			{
				break;
			}
		}
		return result.ToArray();
	}

	public async ValueTask<TwitchApiResponseChannels> FetchFollowedChannels(
		string userId,
		string broadcasterId = "",
		int first = 0,
		string after = "",
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/channels/followed");

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		query["user_id"] = userId;
		if (!string.IsNullOrEmpty(broadcasterId))
		{
			query["broadcaster_id"] = broadcasterId;
		}

		if (0 < first)
		{
			query["first"] = first.ToString();
		}
		if (!string.IsNullOrEmpty(after))
		{
			query["after"] = after;
		}

		uriBuilder.Query = query.ToString();

		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var jsonResponse = await response.Content.ReadFromJsonAsync<TwitchApiResponseChannels>(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (jsonResponse == null)
		{
			throw new InvalidOperationException($"Failed Deserialize.");
		}
		return jsonResponse;
	}

	public async ValueTask<TwitchApiResponseStreams.Data[]> FetchAllStreams(
		string[] userIds,
		string[] userLogins,
		string[] gameIds,
		int first,
		string type = "",
		string language = "",
		CancellationToken cancellationToken = default)
	{
		var result = new List<TwitchApiResponseStreams.Data>();
		var after = string.Empty;
		while (true)
		{
			var response = await FetchStreams(userIds, userLogins, gameIds, type, language, first: first, "", after: after, cancellationToken: cancellationToken).ConfigureAwait(false);
			result.AddRange(response.Streams);

			if (!string.IsNullOrEmpty(response.Page.Cursor))
			{
				after = response.Page.Cursor;
			}
			else
			{
				break;
			}
		}
		return result.ToArray();
	}

	public async ValueTask<TwitchApiResponseStreams> FetchStreams(
		string[] userIds,
		string[] userLogins,
		string[] gameIds,
		string type = "",
		string language = "",
		int first = 0,
		string before = "",
		string after = "",
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/streams");

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		foreach(var userId in userIds)
		{
			query.Add("user_id", userId);
		}
		foreach (var userLogin in userLogins)
		{
			query.Add("user_login", userLogin);
		}
		foreach (var gameId in gameIds)
		{
			query.Add("game_id", gameId);
		}
		if (!string.IsNullOrEmpty(type))
		{
			query["type"] = type;
		}
		if (!string.IsNullOrEmpty(language))
		{
			query["language"] = language;
		}

		if (0 < first)
		{
			query["first"] = first.ToString();
		}
		if (!string.IsNullOrEmpty(before))
		{
			query["before"] = before;
		}
		if (!string.IsNullOrEmpty(after))
		{
			query["after"] = after;
		}

		uriBuilder.Query = query.ToString();

		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var jsonResponse = await response.Content.ReadFromJsonAsync<TwitchApiResponseStreams>(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (jsonResponse == null)
		{
			throw new InvalidOperationException($"Failed Deserialize.");
		}
		return jsonResponse;
	}

	public async ValueTask<TwitchApiResponseStreams.Data[]> FetchAllFollowedStreams(
		string userId,
		int first,
		CancellationToken cancellationToken)
	{
		var result = new List<TwitchApiResponseStreams.Data>();
		var after = string.Empty;
		while (true)
		{
			var response = await FetchFollowedStreams(userId, first, after, cancellationToken).ConfigureAwait(false);
			result.AddRange(response.Streams);
			if (!string.IsNullOrEmpty(response.Page.Cursor))
			{
				after = response.Page.Cursor;
			}
			else
			{
				break;
			}
		}
		return result.ToArray();
	}

	public async ValueTask<TwitchApiResponseStreams> FetchFollowedStreams(
		string userId,
		int first,
		string after,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/streams/followed");

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		query["user_id"] = userId;
		query["first"] = first.ToString();
		if (!string.IsNullOrEmpty(after))
		{
			query["after"] = after;
		}

		uriBuilder.Query = query.ToString();

		var httpClient = CreateAuthHttpClient();
		var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var jsonResponse = await response.Content.ReadFromJsonAsync<TwitchApiResponseStreams>(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (jsonResponse == null)
		{
			throw new InvalidOperationException($"Failed Deserialize.");
		}
		return jsonResponse;
	}

	private HttpClient CreateAuthHttpClient()
	{
		var httpClient = httpClientFactory.CreateClient("TwitchApiClient");
		if (twitchAuth.TryGetAccessToken(out var accessToken))
		{
			httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken.AccessToken}");
			httpClient.DefaultRequestHeaders.Add("Client-Id", config.Authentication.ClientId);
		}
		else
		{
			throw new InvalidOperationException($"Need Auth.");
		}
		return httpClient;
	}

	public class TwitchRetryHandler(
		ILogger<TwitchRetryHandler> logger,
		TimeProvider timeProvider) : DelegatingHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			for (var retry = 0; retry < 3; ++retry)
			{
				var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
				if (response.IsSuccessStatusCode)
				{
					return response;
				}

				// Retry
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{
					if (response.Headers.TryGetValues("Ratelimit-Limit", out var limits))
					{
						foreach (var limit in limits)
						{
							logger.LogInformation($"Ratelimit-Limit : {limit}");
						}
					}
					if (response.Headers.TryGetValues("Ratelimit-Remaining", out var remainings))
					{
						foreach (var remaining in remainings)
						{
							logger.LogInformation($"Ratelimit-Remaining : {remaining}");
						}
					}
					if (response.Headers.TryGetValues("Ratelimit-Reset", out var resets))
					{
						var currentTime = timeProvider.GetLocalNow();
						var resetTime = currentTime;
						foreach (var reset in resets)
						{
							logger.LogInformation($"Ratelimit-Reset : {reset}");
							if (long.TryParse(reset, out var resetLong))
							{
								var limitTime = DateTimeOffset.FromUnixTimeSeconds(resetLong);
								resetTime = currentTime < limitTime ? limitTime : currentTime;
							}
						}

						var waitTime = TimeSpan.FromMinutes(0.1);
						//var waitTime = (currentTime < resetTime)
						//	? (resetTime - currentTime)
						//	: TimeSpan.FromMinutes(1.0);
						await Task.Delay(waitTime, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
					}
				}
				else
				{
					return response;
				}
			}
			throw new InvalidOperationException($"Failed Send.(Max Retry)");
		}
	}
}
