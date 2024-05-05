using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LiveStreamingNotifier.Services;
using LiveStreamingNotifier.Services.Twitch;

namespace LiveStreamingNotifier;

internal class Program
{
	static void Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		builder.Services.Configure<Configuration>(builder.Configuration);
		builder.Services.Configure<TwitchConfig>(builder.Configuration.GetSection(nameof(Configuration.Twitch)));

		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddMemoryCache();

		builder.Services.AddTransient<TwitchApi.TwitchRetryHandler>();
		builder.Services.AddHttpClient("TwitchApiClient")
			 .AddHttpMessageHandler<TwitchApi.TwitchRetryHandler>();

		builder.Services.AddTransient<TwitchUserWebApiManager>();
		builder.Services.AddTransient<TwitchUserWebSocketManager>();
		builder.Services.AddTransient<TwitchEventSubWebSocketClient>();
		builder.Services.AddTransient<TwitchApi>();
		builder.Services.AddSingleton<TwitchAuth>();
		builder.Services.AddHostedService<TwitchService>();

		builder.Services.AddSingleton<NotificationProvider>();
		builder.Services.AddHostedService<NotificationService>();

		builder.Services.AddSingleton<WebImageCacheService>();

		var host = builder.Build();
		host.Run();
	}
}
