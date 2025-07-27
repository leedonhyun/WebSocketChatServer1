using Nerdbank.Streams;

using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Services;
using WebSocketChatServer1.Telemetry;

namespace WebSocketChatServer1.Server;

public class ChatServer
{
    //private readonly IClientManager _clientManager;
    //private readonly IMessageBroadcaster _broadcaster;
    //private readonly IMessageHandler<ChatMessage> _chatHandler;
    //private readonly IMessageHandler<FileTransferMessage> _fileHandler;
    //private readonly IEnumerable<ICommandProcessor> _commandProcessors;
    //private readonly IServiceProvider _serviceProvider;
    //private readonly ILogger<ChatServer> _logger;
    private readonly ILogger<ChatServer> _logger;
    private readonly IClientManager _clientManager;
    //private readonly IGroupManager _groupManager;
    private readonly IMessageBroadcaster _messageBroadcaster;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileTransferStateService _fileTransferStateService;
    private readonly IEnumerable<ICommandProcessor> _commandProcessors;
    private readonly IMessageHandler<ChatMessage> _chatMessageHandler;
    private readonly IMessageHandler<FileTransferMessage> _fileTransferHandler;
    private readonly ICommandLogger _commandLogger;
    private readonly ITelemetryService _telemetry;
    private readonly IServiceProvider _serviceProvider;

    public ChatServer(
        IServiceProvider serviceProvider,
        ILogger<ChatServer> logger,
        IClientManager clientManager,
        //IGroupManager groupManager,
        IMessageBroadcaster messageBroadcaster,
        IFileStorageService fileStorageService,
        IFileTransferStateService fileTransferStateService,
        IEnumerable<ICommandProcessor> commandProcessors,
        IMessageHandler<ChatMessage> chatMessageHandler,
        IMessageHandler<FileTransferMessage> fileTransferHandler,
        ICommandLogger commandLogger,
        ITelemetryService telemetry)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clientManager = clientManager;
        //_groupManager = groupManager;
        _messageBroadcaster = messageBroadcaster;
        _fileStorageService = fileStorageService;
        _fileTransferStateService = fileTransferStateService;
        _commandProcessors = commandProcessors;
        _chatMessageHandler = chatMessageHandler;
        _fileTransferHandler = fileTransferHandler;
        _commandLogger = commandLogger;
        _telemetry = telemetry;
    }

    public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket, CancellationToken cancellationToken)
    //public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
    {
        var clientId = Guid.NewGuid().ToString();
        var client = new Client
        {
            Id = clientId,
            Username = $"User_{clientId[..8]}",
            ConnectedAt = DateTime.UtcNow,
            Status = ClientStatus.Connected
        };

        try
        {
            await _clientManager.AddClientAsync(clientId, client);
            var (multiplexingStream, messageChannel, fileChannel) = await CreateMultiplexedChannelsAsync(webSocket, cancellationToken);

            // WebSocket을 Stream으로 변환
            var stream = webSocket.AsStream();

            // MultiplexingStream 생성
            //var multiplexingStream = await MultiplexingStream.CreateAsync(
            //    stream,
            //    new MultiplexingStream.Options
            //    {
            //        TraceSource = new System.Diagnostics.TraceSource("ChatServer")
            //    },
            //    CancellationToken.None);

            // 채널 생성
            //var messageChannelTask = multiplexingStream.AcceptChannelAsync("messages", CancellationToken.None);
            //var fileChannelTask = multiplexingStream.AcceptChannelAsync("files", CancellationToken.None);

            //await Task.WhenAll(messageChannelTask, fileChannelTask);

            //var messageChannel = await messageChannelTask;
            //var fileChannel = await fileChannelTask;

            // 연결 등록
            var connection = new WebSocketClientConnection(
                messageChannel,
                fileChannel,
                _serviceProvider.GetRequiredService<ILogger<WebSocketClientConnection>>());

            //if (_messageBroadcaster is IMessageBroadcaster broadcaster)
            //{
            _messageBroadcaster.RegisterConnection(clientId, connection);
            //}

            _logger.LogInformation($"Client {clientId} ({client.Username}) connected successfully");

            // 환영 메시지 전송
            var welcomeMessage = new ChatMessage
            {
                Type = "system",
                Username = "System",
                Message = $"Welcome {client.Username}! You are now connected to the chat.",
                Timestamp = DateTime.UtcNow
            };

            await _messageBroadcaster.SendToClientAsync(clientId, welcomeMessage);

            // 메시지 수신 처리 시작
            var messageTask = Task.Run(async () =>
            {
                try
                {
                    await HandleMessagesAsync(clientId, messageChannel, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling messages for client {clientId}");
                }
            });

            var fileTask = Task.Run(async () =>
            {
                try
                {
                    await HandleFileTransferAsync(clientId, fileChannel, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling file transfer for client {clientId}");
                }
            });

            // 연결 유지
            await Task.WhenAny(messageTask, fileTask);
            await multiplexingStream.Completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling client {clientId}");
        }
        finally
        {
            // 정리
            await _clientManager.RemoveClientAsync(clientId);

            if (_messageBroadcaster is IMessageBroadcaster broadcaster)
            {
                broadcaster.UnregisterConnection(clientId);
            }

            // 퇴장 메시지
            var leaveMessage = new ChatMessage
            {
                Type = "system",
                Username = "System",
                Message = $"{client.Username} left the chat",
                Timestamp = DateTime.UtcNow
            };

            await _messageBroadcaster.BroadcastAsync(leaveMessage, clientId);

            _logger.LogInformation($"Client {clientId} ({client.Username}) disconnected");
        }
    }
    private async Task<(MultiplexingStream stream, MultiplexingStream.Channel messageChannel, MultiplexingStream.Channel fileChannel)> CreateMultiplexedChannelsAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var stream = webSocket.AsStream();
        var multiplexingStream = await MultiplexingStream.CreateAsync(
            stream,
            new MultiplexingStream.Options { TraceSource = new System.Diagnostics.TraceSource("ChatServer") },
            cancellationToken);

        var messageChannelTask = multiplexingStream.AcceptChannelAsync("messages", cancellationToken);
        var fileChannelTask = multiplexingStream.AcceptChannelAsync("files", cancellationToken);

        await Task.WhenAll(messageChannelTask, fileChannelTask);

        return (multiplexingStream, await messageChannelTask, await fileChannelTask);
    }

    private async Task HandleMessagesAsync(string clientId, MultiplexingStream.Channel channel, CancellationToken cancellationToken)
    {
        var messageBuffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await channel.Input.ReadAsync(cancellationToken);
                if (result.IsCompleted || result.IsCanceled)
                {
                    _logger.LogInformation("Message channel closed for client {ClientId}", clientId);
                    break;
                }

                var receivedText = Encoding.UTF8.GetString(result.Buffer.ToArray());
                channel.Input.AdvanceTo(result.Buffer.End);

                messageBuffer.Append(receivedText);
                var messages = messageBuffer.ToString().Split('\n');

                messageBuffer.Clear();
                if (!string.IsNullOrEmpty(messages[^1]))
                    messageBuffer.Append(messages[^1]);

                for (int i = 0; i < messages.Length - 1; i++)
                {
                    var messageText = messages[i].Trim();
                    if (!string.IsNullOrEmpty(messageText))
                    {
                        try
                        {
                            var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageText);
                            if (chatMessage != null)
                            {
                                _logger.LogDebug($"Received message from {clientId}: {chatMessage.Message}");
                                await ProcessChatMessageAsync(clientId, chatMessage, cancellationToken);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, $"Failed to parse message from client {clientId}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling messages for client {clientId}");
        }
    }

    private async Task HandleFileTransferAsync(string clientId, MultiplexingStream.Channel channel, CancellationToken cancellationToken = default)
    {
        var messageBuffer = new StringBuilder();

        try
        {
            while (true)
            {
                var result = await channel.Input.ReadAsync();
                if (result.IsCompleted) break;

                var receivedText = Encoding.UTF8.GetString(result.Buffer.ToArray());
                channel.Input.AdvanceTo(result.Buffer.End);

                messageBuffer.Append(receivedText);
                var messages = messageBuffer.ToString().Split('\n');

                messageBuffer.Clear();
                if (!string.IsNullOrEmpty(messages[^1]))
                    messageBuffer.Append(messages[^1]);

                for (int i = 0; i < messages.Length - 1; i++)
                {
                    var messageText = messages[i].Trim();
                    if (!string.IsNullOrEmpty(messageText))
                    {
                        try
                        {
                            var fileMessage = JsonSerializer.Deserialize<FileTransferMessage>(messageText);
                            if (fileMessage != null)
                            {
                                _logger.LogDebug($"Received file message from {clientId}: {fileMessage.Type}");
                                await _fileTransferHandler.HandleAsync(clientId, fileMessage, cancellationToken);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, $"Failed to parse file message from client {clientId}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling file transfer for client {clientId}");
        }
    }

    private async Task ProcessChatMessageAsync(string clientId, ChatMessage message, CancellationToken cancellationToken)
    {
        // 명령어 처리
        foreach (var processor in _commandProcessors)
        {
            if (await processor.CanProcessAsync(message.Type))
            {
                var args = string.IsNullOrEmpty(message.Message)
                    ? Array.Empty<string>()
                    //: new[] { message.Message };
                    : message.Message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                await processor.ProcessAsync(clientId, message.Type, args);
                return;
            }
        }

        // 채팅 타입별 처리
        switch (message.Type)
        {
            case "chat":
                await _chatMessageHandler.HandleAsync(clientId, message, cancellationToken);
                break;
            case "privateChat":
                await HandlePrivateChatAsync(clientId, message, cancellationToken);
                break;
            case "privateMessage":
                // privateMessage는 PrivateMessageCommandProcessor에서 처리
                var privateMessageProcessor = _commandProcessors.FirstOrDefault(p => p.GetType().Name == "PrivateMessageCommandProcessor");
                if (privateMessageProcessor != null)
                {
                    var args = new[] { message.ToUsername, message.Message };
                    await privateMessageProcessor.ProcessAsync(clientId, "privateMessage", args);
                }
                break;
            //case "roomChat":
            //    // 룸 채팅도 GroupChatCommandProcessor에서 처리 (동일한 로직)
            //    var roomChatProcessor = _commandProcessors.FirstOrDefault(p => p.GetType().Name == "RoomMessageCommandProcessor");
            //    if (roomChatProcessor != null)
            //    {
            //        var args = new[] { message.RoomId, message.Message };
            //        await roomChatProcessor.ProcessAsync(clientId, "roomChat", args);
            //    }
            //    break;
            case "roomMessage":
                // roomMessage는 RoomMessageCommandProcessor에서 처리
                var roomMessageProcessor = _commandProcessors.FirstOrDefault(p => p.GetType().Name == "RoomMessageCommandProcessor");
                if (roomMessageProcessor != null)
                {
                    var args = new[] { message.RoomId, message.Message };
                    await roomMessageProcessor.ProcessAsync(clientId, "roomMessage", args);
                }
                break;
            case "send":
                // send 명령은 SendFileCommandProcessor에서 처리
                var sendFileProcessor = _commandProcessors.FirstOrDefault(p => p.GetType().Name == "SendFileCommandProcessor");
                if (sendFileProcessor != null)
                {
                    // message.Message에서 파일 경로와 대상을 파싱
                    var parts = message.Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    await sendFileProcessor.ProcessAsync(clientId, "send", parts);
                }
                break;
            default:
                _logger.LogWarning($"Unknown message type: {message.Type}");
                break;
        }
    }

    private async Task HandlePrivateChatAsync(string clientId, ChatMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.ToUsername))
        {
            await SendErrorMessage(clientId, "Private chat requires a target username");
            return;
        }

        var sender = await _clientManager.GetClientAsync(clientId);
        if (sender == null) return;

        // 대상 사용자 찾기
        var allClients = await _clientManager.GetAllClientsAsync();
        var targetClient = allClients.FirstOrDefault(c => c.Username == message.ToUsername);

        if (targetClient == null)
        {
            await SendErrorMessage(clientId, $"User '{message.ToUsername}' not found or not online");
            return;
        }

        // 메시지 설정
        message.Username = sender.Username;
        message.ChatType = "private";
        message.Timestamp = DateTime.UtcNow;

        // 수신자에게 메시지 전송
        await _messageBroadcaster.SendToClientAsync(targetClient.Id, message, cancellationToken);

        // 송신자에게 확인 메시지 전송
        var confirmMessage = new ChatMessage
        {
            Type = "privateChat",
            Username = sender.Username,
            Message = message.Message,
            ToUsername = message.ToUsername,
            ChatType = "private",
            Timestamp = message.Timestamp
        };
        await _messageBroadcaster.SendToClientAsync(clientId, confirmMessage, cancellationToken);

        _logger.LogInformation($"Private message sent from {sender.Username} to {message.ToUsername}");
    }

    private async Task SendErrorMessage(string clientId, string errorMessage)
    {
        var error = new ChatMessage
        {
            Type = "error",
            Username = "System",
            Message = errorMessage,
            Timestamp = DateTime.UtcNow
        };
        await _messageBroadcaster.SendToClientAsync(clientId, error);
    }

}