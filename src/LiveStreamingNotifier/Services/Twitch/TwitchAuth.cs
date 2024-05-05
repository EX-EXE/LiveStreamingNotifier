using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Web;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchAuth(
	ILogger<TwitchAuth> logger,
	IOptions<TwitchConfig> options)
	: IDisposable
{
	public event Action<Uri>? OnAuthRedirectUri = null;

	private readonly TwitchConfig config = options.Value;

	private TwitchAccessToken? accessToken = null;

	public void Dispose()
	{
		if (OnAuthRedirectUri != null)
		{
			foreach (var invocation in OnAuthRedirectUri.GetInvocationList())
			{
				OnAuthRedirectUri -= (Action<Uri>)invocation;
			}
		}
	}

	public bool IsExistAccessToken() => accessToken != null;

	public bool TryGetAccessToken([NotNullWhen(true)] out TwitchAccessToken? token)
	{
		var readToken = accessToken;
		if (readToken != null)
		{
			token = readToken;
			return true;
		}
		else
		{
			token = default;
			return false;
		}
	}


	public async ValueTask<TwitchAccessToken> AuthAsync(CancellationToken cancellationToken)
	{
		var state = Guid.NewGuid().ToString();
		var callbackUri = "http://localhost:30000/";
		var authUri = BuildAuthorizeUri(
			config.Authentication.Uri,
			config.Authentication.ClientId,
			callbackUri,
			config.Authentication.ResponseType,
			config.Authentication.Scope,
			state);

		logger.LogInformation($"Access Uri : {authUri}");
		OnAuthRedirectUri?.Invoke(authUri);

		var newAccessToken = await WaitCallbackAsync(callbackUri, state, cancellationToken).ConfigureAwait(false);
		accessToken = newAccessToken;
		return newAccessToken;
	}

	private static Uri BuildAuthorizeUri(
		string uri,
		string clientId,
		string redirectUri,
		string responseType,
		string scope,
		string state)
	{
		var uriBuilder = new UriBuilder(uri);

		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		query["client_id"] = clientId;
		query["redirect_uri"] = redirectUri;
		query["response_type"] = responseType;
		query["scope"] = scope;
		query["state"] = state;

		uriBuilder.Query = query.ToString();
		return uriBuilder.Uri;
	}

	private async ValueTask<TwitchAccessToken> WaitCallbackAsync(string prefix, string state, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var listener = new HttpListener();
		listener.Prefixes.Add(prefix);
		listener.Start();

		static Task<TwitchAccessToken> WaitAsync(HttpListener httpListener, string state, CancellationToken cancellationToken)
		{
			var taskCompletionSource = new TaskCompletionSource<TwitchAccessToken>();

			using var cancellationTokenRegistration = cancellationToken.Register(static completion =>
			{
				if (completion is TaskCompletionSource<TwitchAccessToken> castCompletion)
				{
					castCompletion.TrySetCanceled();
				}
			}, taskCompletionSource);

			_ = Task.Run(async () =>
			{
				while (true)
				{
					try
					{
						var context = await httpListener.GetContextAsync();
						if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
						{
							// Javascript経由で、アクセストークンをPOSTで送信
							var responseString = $$$"""
<!DOCTYPE html>
<html>
	<head>
		<meta charset="UTF-8">
	</head>
	<body>
		<script>
			window.onload = function () {
				if (document.location.hash) {
					console.log(document.location.hash);
					const parsedHash = new URLSearchParams(window.location.hash.substr(1));
					console.log(parsedHash);
					const accessToken = parsedHash.get('access_token');
					const state = parsedHash.get('state');
					if (accessToken && state && state === '{{{state}}}' )
					//if (accessToken && state )
					{
						const data = {}
						for(const [key, value] of parsedHash)
						{
							data[key] = value;
						}

						const params = {method : "POST", body : JSON.stringify(data) };
						fetch("/", params)
							.then(response => {
								console.log(response); 
								if(response.ok) {
									window.close();
								}
							})
							.then(data => console.log(data));
					}
				}
			};
		</script>
	</body>
</html>
""";
							var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
							var response = context.Response;
							response.ContentLength64 = buffer.Length;
							response.OutputStream.Write(buffer, 0, buffer.Length);
							response.OutputStream.Close();
							response.Close();
						}
						else if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
						{
							var request = context.Request;
							using var response = context.Response;
							response.StatusCode = (int)HttpStatusCode.BadRequest;

							var accessToken = await System.Text.Json.JsonSerializer.DeserializeAsync<TwitchAccessToken>(
								request.InputStream,
								cancellationToken: cancellationToken).ConfigureAwait(false);
							if (accessToken == null)
							{
								throw new InvalidOperationException($"Failed Deserialize AccessToken.");
							}
							if (!accessToken.State.Equals(state, StringComparison.Ordinal))
							{
								throw new InvalidOperationException($"Failed State.");
							}


							taskCompletionSource.TrySetResult(accessToken);

							var buffer = System.Text.Encoding.UTF8.GetBytes("Success.");
							response.StatusCode = (int)HttpStatusCode.OK;
							response.ContentLength64 = buffer.Length;
							response.OutputStream.Write(buffer, 0, buffer.Length);
							response.OutputStream.Close();
							response.Close();
							break;
						}
						else
						{
							throw new InvalidOperationException($"Invalid HttpMethod.");
						}


					}
					catch (Exception ex)
					{
						taskCompletionSource.TrySetException(ex);
						break;
					}
				}
			}, cancellationToken);
			return taskCompletionSource.Task;
		};
		var context = await WaitAsync(listener, state, cancellationToken).ConfigureAwait(false);
		listener.Stop();
		return context;
	}

}
