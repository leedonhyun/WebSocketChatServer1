using WebSocketChatShared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebSocketChatServer1.Interfaces;

public interface IClientManager
{
    Task AddClientAsync(string clientId, Client client);
    Task RemoveClientAsync(string clientId);
    Task<Client?> GetClientAsync(string clientId);
    Task<IEnumerable<Client>> GetAllClientsAsync();
    Task<string> UpdateClientUserNameAsync(string clientId, string newUsername);
}