using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LiveStreamingNotifier.Services;

internal interface INotification
{
	Guid Id { get; }
	TimeSpan ExpirationTime { get; }
	ToastContentBuilder CreateToastContentBuilder();

	Action<string>? OnAction { get; }
	Func<string, ValueTask>? OnTask { get; }
}

internal class NotificationAction : INotification
{
	public Guid Id { get; private set; } = Guid.NewGuid();
	public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromHours(1.0);
	public Action<string>? OnAction { get; set; } = null;
	public Func<string, ValueTask>? OnTask { get; set; } = null;


	public string[] TextLines { get; set; } = [];
	public Uri? IconUri { get; set; } = null;
	public Uri? ImageUri { get; set; } = null;
	public string[] Actions { get; set; } = [];

	public ToastContentBuilder CreateToastContentBuilder()
	{
		var builder = new ToastContentBuilder();
		builder.AddArgument(nameof(Id), Id.ToString());

		foreach(var textLine in TextLines)
		{
			builder.AddText(textLine);
		}

		if (IconUri != null)
		{
			builder.AddAppLogoOverride(IconUri, ToastGenericAppLogoCrop.Circle);
		}
		if (ImageUri != null)
		{
			builder.AddInlineImage(ImageUri);
		}

		foreach (var action in Actions)
		{
			builder.AddButton(new ToastButton()
				.SetContent(action)
				.AddArgument("Action", action)
				.SetBackgroundActivation());
		}
		return builder;
	}
}
internal class NotificationOpenUrl : NotificationAction
{
	public NotificationOpenUrl()
		: base()
	{
	}
}


internal class NotificationProvider
{
	private Channel<INotification> channel = Channel.CreateBounded<INotification>(128);

	public ChannelReader<INotification> ChannelReader => channel.Reader;

	public bool AddNotification(INotification notification)
	{
		return channel.Writer.TryWrite(notification);
	}

}

internal class NotificationService(
	ILogger<NotificationService> logger,
	TimeProvider timeProvider,
	NotificationProvider notificationProvider) : BackgroundService
{
	private Dictionary<Guid, (INotification, DateTimeOffset)> notificationDict = new();

	public override void Dispose()
	{
		base.Dispose();
		ToastNotificationManagerCompat.Uninstall();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

		while (!stoppingToken.IsCancellationRequested)
		{
			var notification = (INotification?)default;
			while (await notificationProvider.ChannelReader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
			{
				while (notificationProvider.ChannelReader.TryRead(out notification))
				{
					try
					{
						var builder = notification.CreateToastContentBuilder();
						if (TimeSpan.Zero < notification.ExpirationTime)
						{
							var expirationDate = timeProvider.GetLocalNow();
							expirationDate = expirationDate.Add(notification.ExpirationTime);
							notificationDict.Add(notification.Id, (notification, expirationDate));
							builder.Show(toast =>
							{
								toast.ExpirationTime = expirationDate;
							});
						}
						else
						{
							builder.Show();
						}
					}
					catch (Exception ex)
					{
						logger.LogWarning(ex, $"Failed Notification.");
					}
				}

				// Clean
				var currentDateTime = timeProvider.GetLocalNow();
				foreach (var (id, _) in notificationDict.Where(x => x.Value.Item2 < currentDateTime).ToArray())
				{
					notificationDict.Remove(id);
				}
			}
		}
	}

	private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
	{
		var args = ToastArguments.Parse(e.Argument);

		var idArg = args.FirstOrDefault(x => x.Key.Equals(nameof(INotification.Id), StringComparison.Ordinal));
		if (Guid.TryParse(idArg.Value, out var id) &&
			notificationDict.TryGetValue(id, out var notificationItems))
		{
			var actionArg = args.FirstOrDefault(x => x.Key.Equals("Action", StringComparison.Ordinal));
			try
			{
				notificationItems.Item1.OnAction?.Invoke(actionArg.Value);
			}
			catch { }
			Task.Run(async () =>
			{
				try
				{
					if (notificationItems.Item1.OnTask != null)
					{
						await notificationItems.Item1.OnTask(actionArg.Value);
					}
				}
				catch { }
			});
		}
	}
}
