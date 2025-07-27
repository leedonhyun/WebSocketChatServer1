using WebSocketChatServer1.Interfaces;
using WebSocketChatShared;
using WebSocketChatShared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Commands
{
    public class UserListCommandProcessor : BaseCommandProcessor
    {
        private readonly IMessageBroadcaster _broadcaster;

        public UserListCommandProcessor(
            IClientManager clientManager,
            IMessageBroadcaster broadcaster,
            ICommandLogger commandLogger,
            ILogger<UserListCommandProcessor> logger) : base(clientManager, commandLogger, logger)
        {
            _broadcaster = broadcaster;
        }

        public override Task<bool> CanProcessAsync(string command)
        {
            return Task.FromResult(command.Equals(ChatConstants.MessageTypes.ListUsers, StringComparison.OrdinalIgnoreCase));
        }

        public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var success = false;
            string? errorMessage = null;

            try
            {
                var clients = await ClientManager.GetAllClientsAsync();
                var userList = string.Join(", ", clients.Select(c => c.Username));

                var message = new ChatMessage
                {
                    Type = ChatConstants.MessageTypes.System,
                    Username = ChatConstants.SystemUsername,
                    Message = $"Online users: {userList}",
                    Timestamp = DateTime.UtcNow
                };

                await _broadcaster.SendToClientAsync(clientId, message, cancellationToken);
                success = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Logger.LogError(ex, "Error processing user list command");
            }
            finally
            {
                stopwatch.Stop();
                await LogCommandAsync(clientId, command, args, stopwatch.Elapsed.TotalMilliseconds, success, errorMessage);
            }
        }

        public override async Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default)
        {
            await ProcessAsync(clientId, chatMessage.Type, Array.Empty<string>(), cancellationToken);
        }
    }
}