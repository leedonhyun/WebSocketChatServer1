using ChatSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface IClientManager
{
    Task AddClientAsync(string clientId, Client client);
    Task RemoveClientAsync(string clientId);
    Task<Client?> GetClientAsync(string clientId);
    Task<IEnumerable<Client>> GetAllClientsAsync();
    Task UpdateClientUsernameAsync(string clientId, string newUsername);
}