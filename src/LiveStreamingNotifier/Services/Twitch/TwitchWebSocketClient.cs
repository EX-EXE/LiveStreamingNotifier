using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;

namespace LiveStreamingNotifier.Services.Twitch;

internal class TwitchWebSocketClient(
	ILogger<TwitchWebSocketClient> logger,
	IOptions<TwitchConfig> options) : IAsyncDisposable
{
	public event Func<TwitchWebSocketClient, TwitchMessage, CancellationToken, ValueTask>? OnMessageReceived = null;

	private readonly TwitchConfig config = options.Value;

	private readonly ClientWebSocket clientWebSocket = new ClientWebSocket();
	private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
	private CancellationToken cancellationToken => cancellationTokenSource.Token;

	private Task? task = null;
	private TwitchMessage? sessionWelcomeMessage = null;

	public void Dispose()
	{
		DisposeAsync().AsTask().Wait();
	}
	public async ValueTask DisposeAsync()
	{
		await StopAsync(default).ConfigureAwait(false);
		if(OnMessageReceived != null)
		{
			foreach(var invocation in OnMessageReceived.GetInvocationList())
			{
				OnMessageReceived -= (Func<TwitchWebSocketClient, TwitchMessage, CancellationToken, ValueTask>)invocation;
			}
		}
	}

	public bool IsConnecting()
	{
		return clientWebSocket.State == WebSocketState.Open || 
			clientWebSocket.State == WebSocketState.Connecting;
	}
	public bool IsReady()
	{
		if (sessionWelcomeMessage == null)
		{
			return false;
		}
		return IsConnecting();
	}

	public bool IsClosed()
	{
		return clientWebSocket.State != WebSocketState.None &&
			clientWebSocket.State != WebSocketState.Open &&
			clientWebSocket.State != WebSocketState.Connecting;
	}

	public bool TryGetSessionId(out string sessionId)
	{
		sessionId = "";
		if(sessionWelcomeMessage != null)
		{
			sessionId = sessionWelcomeMessage.PayloadData.SessionData.Id;
			return true;
		}
		return false;
	}

	public void Start()
	{
		if (clientWebSocket.State != WebSocketState.None)
		{
			return;
		}
		task = Task.Run(async () =>
		{
			try
			{
				var eventSubUri = new Uri(config.EventSub.Uri);
				await clientWebSocket.ConnectAsync(eventSubUri, cancellationToken).ConfigureAwait(false);

				var pipe = new Pipe();
				var fillTask = FillPipeAsync(clientWebSocket, pipe.Writer, cancellationToken: cancellationToken);
				var readTask = ReadPipeAsync(pipe.Reader, cancellationToken: cancellationToken);
				await Task.WhenAll(fillTask, readTask);
			}
			catch (Exception ex)
			{
				if (ex is not OperationCanceledException)
				{
					logger.LogError(ex, $"Exception {nameof(TwitchWebSocketClient)}.");
				}
			}
		}, cancellationToken);

	}
	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		if (!cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource.Cancel();
		}
		await clientWebSocket.CloseAsync(
			WebSocketCloseStatus.NormalClosure,
			"Client closed",
			cancellationToken).ConfigureAwait(false);
		if (task != null)
		{
			await task;
		}
	}

	private async Task FillPipeAsync(ClientWebSocket socket, PipeWriter writer, CancellationToken cancellationToken)
	{
		const int minimumBufferSize = 512;

		while (!cancellationToken.IsCancellationRequested)
		{
			var memory = writer.GetMemory(minimumBufferSize);
			try
			{
				// 最後のbyteは残す
				var reciveMemory = memory.Slice(0, memory.Length - 1);

				var receiveResult = await socket.ReceiveAsync(reciveMemory, cancellationToken: cancellationToken);
				if (receiveResult.MessageType == WebSocketMessageType.Close)
				{
					break;
				}

				var writeByteCount = receiveResult.Count;
				if (receiveResult.EndOfMessage)
				{
					static void WriteEnd(Span<byte> span, int position)
					{
						span[position] = (byte)'\0';
					}
					WriteEnd(memory.Span, receiveResult.Count);
					++writeByteCount;
				}
				writer.Advance(writeByteCount);
			}
			catch (Exception ex)
			{
				if (ex is not OperationCanceledException)
				{
					logger.LogWarning(ex, $"Exception {nameof(FillPipeAsync)}.");
				}
				break;
			}

			FlushResult result = await writer.FlushAsync();
			if (result.IsCompleted)
			{
				break;
			}
		}
		await writer.CompleteAsync();
	}

	private async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken)
	{
		while (true)
		{
			ReadResult result = await reader.ReadAsync();
			ReadOnlySequence<byte> buffer = result.Buffer;

			while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
			{
				await ProcessLineAsync(line, cancellationToken).ConfigureAwait(false);
			}
			reader.AdvanceTo(buffer.Start, buffer.End);
			if (result.IsCompleted)
			{
				break;
			}
		}
		await reader.CompleteAsync();
	}

	private bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
	{
		line = default;
		var position = buffer.PositionOf((byte)'\0');
		if (position == null)
		{
			return false;
		}

		line = buffer.Slice(0, position.Value);
		buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
		return true;
	}

	private async ValueTask ProcessLineAsync(ReadOnlySequence<byte> line, CancellationToken cancellationToken)
	{
		var document = JsonDocument.Parse(line);
		var message = JsonSerializer.Deserialize<TwitchMessage>(document);
		if (message == null)
		{
			return;
		}
		logger.LogInformation($"{message.Meta.MessageType} [{message.Meta.MessageTimestamp.ToLocalTime()}]");
		if (message.Meta.MessageType.Equals("session_welcome", StringComparison.Ordinal))
		{
			sessionWelcomeMessage = message;
			//// 初期化
			//var users = await twitchApi.FetchUsersAsync([], "", cancellationToken: cancellationToken).ConfigureAwait(false);
			//var user = users.Users[0];
			//var channels = await twitchApi.FetchAllFollowedChannels(user.Id, cancellationToken: cancellationToken);
			//
			//foreach (var channel in channels)
			//{
			//	var result = await twitchApi.PostEventSubSubscriptionStreamOnlineAsync(
			//		channel.BroadcasterId,
			//		message.PayloadData.SessionData.Id,
			//		cancellationToken).ConfigureAwait(false);
			//}
		}
		if (OnMessageReceived != null)
		{
			await OnMessageReceived.Invoke(this, message, cancellationToken).ConfigureAwait(false);
		}
	}

}