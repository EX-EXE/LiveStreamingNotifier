using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LiveStreamingNotifier.Services.Twitch;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using static LiveStreamingNotifier.Services.Twitch.TwitchMessage.Payload.Subscription;
using static System.Formats.Asn1.AsnWriter;

namespace LiveStreamingNotifier.Services;


internal class TwitchAuthNotification
	: NotificationAction
{
	public TwitchAuthNotification(Uri authUri)
	{
		TextLines = ["Twitchアプリ認証"];
		Actions = ["ブラウザで開く"];
		OnAction = (string _) =>
		{
			try
			{
				var replaceUri = authUri.ToString().Replace("&", "^&");
				var psi = new ProcessStartInfo
				{
					FileName = "cmd",
					Arguments = $"/c start {replaceUri}"
				};
				Process.Start(psi);
			}
			catch (Exception)
			{
			}
		};
	}
}

internal class TwitchOnlineStreamNotification
	: NotificationAction
{
	public TwitchOnlineStreamNotification(TwitchStreamInfo info)
	{
		TextLines = [
			$"{info.UserInfo.DisplayName} [{info.StreamInfo.StartedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss")}]",
			$"{info.StreamInfo.Title}",
			$"{info.StreamInfo.GameName}"
		];

		if (!string.IsNullOrEmpty(info.UserImageCacheFile))
		{
			IconUri = new Uri(info.UserImageCacheFile);
		}
		if (!string.IsNullOrEmpty(info.StreamImageCacheFile))
		{
			ImageUri = new Uri(info.StreamImageCacheFile);
		}

		Actions = ["ブラウザで開く"];
		OnAction = (string _) =>
		{
			try
			{
				var twitchUri = $"https://www.twitch.tv/{info.UserInfo.Login}";
				var replaceUri = twitchUri.ToString().Replace("&", "^&");
				var psi = new ProcessStartInfo
				{
					FileName = "cmd",
					Arguments = $"/c start {replaceUri}"
				};
				Process.Start(psi);
			}
			catch (Exception)
			{
			}
		};
	}
}

internal class TwitchService(
	ILogger<TwitchService> logger,
	IOptions<TwitchConfig> options,
	IServiceProvider serviceProvider,
	TwitchAuth twitchAuth,
	NotificationProvider notificationProvider) : BackgroundService
{
	private readonly TwitchConfig config = options.Value;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var cancellationToken = stoppingToken;

		twitchAuth.OnAuthRedirectUri += (authUri) =>
		{
			notificationProvider.AddNotification(new TwitchAuthNotification(authUri));
		};

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await using var userManager = serviceProvider.GetService<TwitchUserWebApiManager>();
				if (userManager != null)
				{
					userManager.OnOnlineStream += (info) =>
					{
						notificationProvider.AddNotification(new TwitchOnlineStreamNotification(info));
					};
				}
				if (userManager != null)
				{
					await userManager.RunAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				if (ex is not OperationCanceledException)
				{
					logger.LogError(ex, $"Exception.");
				}
				await Task.Delay(1000);
			}
		}
	}

}
