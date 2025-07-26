using ChatSystem.Models;
using ChatSystem.Services;

using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ChatSystem.Interfaces;
public interface IMessageHandler<T> where T : BaseMessage
{
    Task HandleAsync(string clientId, T message);
}

public interface IChannelManager
{
    Task<IChannel> CreateChannelAsync(string name, CancellationToken cancellationToken);
    Task<IChannel> AcceptChannelAsync(string name, CancellationToken cancellationToken);
}

public interface IChannel : IDisposable
{
    Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage;
    IAsyncEnumerable<T> ReceiveAsync<T>(CancellationToken cancellationToken) where T : BaseMessage;
}

public interface IClientManager
{
    Task AddClientAsync(string clientId, Client client);
    Task RemoveClientAsync(string clientId);
    Task<Client?> GetClientAsync(string clientId);
    Task<IEnumerable<Client>> GetAllClientsAsync();
    Task UpdateClientUsernameAsync(string clientId, string newUsername);
}

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string fileId, string fileName, byte[] data, bool append = false);
    Task<byte[]> ReadFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task DeleteFileAsync(string filePath);
    string GetFilePath(string fileId, string fileName);
}

public interface IMessageBroadcaster
{
    Task BroadcastAsync<T>(T message, string? excludeClientId = null) where T : BaseMessage;
    Task SendToClientAsync<T>(string clientId, T message) where T : BaseMessage;
    Task SendToUsernameAsync<T>(string username, T message) where T : BaseMessage;
    void RegisterConnection(string clientId, IClientConnection connection);
    void UnregisterConnection(string clientId);

}

public interface ICommandProcessor
{
    Task<bool> CanProcessAsync(string command);
    Task ProcessAsync(string clientId, string command, string[] args);
}

public interface IGroupManager
{
    Task<string> CreateGroupAsync(string groupName, string createdBy);
    Task<bool> AddMemberAsync(string groupId, string username);
    Task<bool> RemoveMemberAsync(string groupId, string username);
    Task<Group?> GetGroupAsync(string groupId);
    Task<IEnumerable<Group>> GetGroupsByUserAsync(string username);
    Task<IEnumerable<Group>> GetAllGroupsAsync();
    Task<bool> IsGroupMemberAsync(string groupId, string username);
    Task<bool> DeleteGroupAsync(string groupId);
    Task<IEnumerable<string>> GetGroupMembersAsync(string groupId);
}

public interface ICommandLogger
{
    Task LogCommandAsync(string clientId, string username, string commandType, string? parameters = null, bool success = true, double executionTimeMs = 0, string? errorMessage = null);
}
