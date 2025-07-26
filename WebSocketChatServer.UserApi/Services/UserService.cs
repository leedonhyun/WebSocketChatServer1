using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebSocketChatServer.UserApi.Models;
using BCrypt.Net;

namespace WebSocketChatServer.UserApi.Services;

public interface IUserService
{
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(RegisterRequest request);
    Task<User?> AuthenticateUserAsync(string username, string password);
    Task<User?> UpdateUserAsync(string id, UpdateUserRequest request);
    Task<bool> ChangePasswordAsync(string id, string currentPassword, string newPassword);
    Task<bool> DeleteUserAsync(string id);
    Task<List<User>> GetUsersAsync(int skip = 0, int limit = 50);
    Task<bool> UserExistsAsync(string username, string email);
}

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _users;

    public UserService(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
    {
        var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        _users = database.GetCollection<User>(settings.Value.UsersCollectionName);

        // Create indexes
        CreateIndexesAsync().Wait();
    }

    private async Task CreateIndexesAsync()
    {
        var indexKeysDefinition = Builders<User>.IndexKeys
            .Ascending(u => u.Username);
        await _users.Indexes.CreateOneAsync(new CreateIndexModel<User>(indexKeysDefinition,
            new CreateIndexOptions { Unique = true }));

        var emailIndexKeysDefinition = Builders<User>.IndexKeys
            .Ascending(u => u.Email);
        await _users.Indexes.CreateOneAsync(new CreateIndexModel<User>(emailIndexKeysDefinition,
            new CreateIndexOptions { Unique = true }));
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
    }

    public async Task<User> CreateUserAsync(RegisterRequest request)
    {
        // Check if user already exists
        if (await UserExistsAsync(request.Username, request.Email))
        {
            throw new InvalidOperationException("User with this username or email already exists");
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);
        return user;
    }

    public async Task<User?> AuthenticateUserAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);

        if (user == null || !user.IsActive)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        // Update last login time
        var update = Builders<User>.Update
            .Set(u => u.LastLoginAt, DateTime.UtcNow)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
        user.LastLoginAt = DateTime.UtcNow;

        return user;
    }

    public async Task<User?> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var updateBuilder = Builders<User>.Update.Set(u => u.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.FirstName))
            updateBuilder = updateBuilder.Set(u => u.FirstName, request.FirstName);

        if (!string.IsNullOrEmpty(request.LastName))
            updateBuilder = updateBuilder.Set(u => u.LastName, request.LastName);

        if (!string.IsNullOrEmpty(request.Email))
        {
            // Check if email is already in use by another user
            var existingUser = await GetUserByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != id)
                throw new InvalidOperationException("Email is already in use");

            updateBuilder = updateBuilder.Set(u => u.Email, request.Email);
        }

        var result = await _users.UpdateOneAsync(u => u.Id == id, updateBuilder);

        if (result.ModifiedCount == 0)
            return null;

        return await GetUserByIdAsync(id);
    }

    public async Task<bool> ChangePasswordAsync(string id, string currentPassword, string newPassword)
    {
        var user = await GetUserByIdAsync(id);
        if (user == null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(newPassword))
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var result = await _users.UpdateOneAsync(u => u.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var result = await _users.DeleteOneAsync(u => u.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<User>> GetUsersAsync(int skip = 0, int limit = 50)
    {
        return await _users.Find(_ => true)
            .Skip(skip)
            .Limit(limit)
            .SortBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<bool> UserExistsAsync(string username, string email)
    {
        var count = await _users.CountDocumentsAsync(u => u.Username == username || u.Email == email);
        return count > 0;
    }
}
