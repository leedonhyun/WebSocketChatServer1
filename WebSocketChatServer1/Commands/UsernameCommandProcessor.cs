using WebSocketChatServer1.Interfaces;
using WebSocketChatShared;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Commands
{
    public class UsernameCommandProcessor : BaseCommandProcessor
    {
        private readonly IMessageBroadcaster _broadcaster;

        public UsernameCommandProcessor(
            IClientManager clientManager,
            IMessageBroadcaster broadcaster,
            ICommandLogger commandLogger,
            ILogger<UsernameCommandProcessor> logger) : base(clientManager, commandLogger, logger)
        {
            _broadcaster = broadcaster;
        }

        public override Task<bool> CanProcessAsync(string command)
        {
            return Task.FromResult(command.Equals(ChatConstants.MessageTypes.SetUserName, StringComparison.OrdinalIgnoreCase));
        }

        public override async Task ProcessAsync(string clientId, string command, string[] args, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var success = false;
            string? errorMessage = null;

            try
            {
                if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                {
                    errorMessage = "Username cannot be empty";
                    await SendErrorMessage(clientId, errorMessage);
                    return;
                }

                var newUsername = args[0];
                var oldUsername = await ClientManager.UpdateClientUserNameAsync(clientId, newUsername);

                if (oldUsername != null)
                {
                    var notification = new ChatMessage
                    {
                        Type = ChatConstants.MessageTypes.System,
                        Username = ChatConstants.SystemUsername,
                        Message = $"{oldUsername} is now known as {newUsername}",
                        Timestamp = DateTime.UtcNow
                    };
                    await _broadcaster.BroadcastAsync(notification);
                    Logger.LogInformation($"User {clientId} changed username from {oldUsername} to {newUsername}");
                    success = true;
                }
                else
                {
                    errorMessage = "Failed to update username";
                    await SendErrorMessage(clientId, errorMessage);
                }
            }
            finally
            {
                stopwatch.Stop();
                await LogCommandAsync(clientId, command, args, stopwatch.Elapsed.TotalMilliseconds, success, errorMessage);
            }
        }

        public override async Task ProcessAsync(string clientId, ChatMessage chatMessage, CancellationToken cancellationToken = default)
        {
            await ProcessAsync(clientId, chatMessage.Type, new[] { chatMessage.Message }, cancellationToken);
        }

        private async Task SendErrorMessage(string clientId, string errorMessage)
        {
            var error = new ChatMessage
            {
                Type = ChatConstants.MessageTypes.Error,
                Username = ChatConstants.SystemUsername,
                Message = errorMessage,
                Timestamp = DateTime.UtcNow
            };
            await _broadcaster.SendToClientAsync(clientId, error);
        }
    }
}