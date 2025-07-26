using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;

using Microsoft.Extensions.Logging;

using Nerdbank.Streams;

using System.Text;
using System.Text.Json;

using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Services;

public class WebSocketClientConnection : IClientConnection
{
    private readonly MultiplexingStream.Channel _messageChannel;
    private readonly MultiplexingStream.Channel _fileChannel;
    private readonly ILogger<WebSocketClientConnection> _logger;
    private bool _isConnected = true;

    public bool IsConnected => _isConnected;

    public WebSocketClientConnection(
        MultiplexingStream.Channel messageChannel,
        MultiplexingStream.Channel fileChannel,
        ILogger<WebSocketClientConnection> logger)
    {
        _messageChannel = messageChannel;
        _fileChannel = fileChannel;
        _logger = logger;
    }

    public async Task SendAsync<T>(T message) where T : BaseMessage
    {
        try
        {
            var channel = message switch
            {
                ChatMessage => _messageChannel,
                FileTransferMessage => _fileChannel,
                _ => _messageChannel
            };

            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json + "\n");

            await channel.Output.WriteAsync(buffer.AsMemory());
            await channel.Output.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            _isConnected = false;
            throw;
        }
    }
}