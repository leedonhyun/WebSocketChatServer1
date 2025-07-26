using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using WebSocketChatServer1.Telemetry;

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace WebSocketChatServer1.Services;

public class GroupManager : IGroupManager
{
    private readonly ConcurrentDictionary<string, Group> _groups = new();
    private readonly ILogger<GroupManager> _logger;
    private readonly ITelemetryService _telemetry;

    public GroupManager(ILogger<GroupManager> logger, ITelemetryService telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<string> CreateGroupAsync(string groupName, string createdBy)
    {
        using var activity = ChatTelemetry.StartGroupOperationActivity("create", "");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var groupId = Guid.NewGuid().ToString();
            var group = new Group
            {
                Id = groupId,
                Name = groupName,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Members = new HashSet<string> { createdBy }
            };

            _groups[groupId] = group;
            activity?.SetTag("chat.group.id", groupId);
            activity?.SetTag("chat.group.name", groupName);
            activity?.SetTag("chat.group.created_by", createdBy);

            _telemetry.RecordGroupCreated("group");
            _telemetry.UpdateActiveGroups(_groups.Count);

            _logger.LogInformation($"Group '{groupName}' (ID: {groupId}) created by {createdBy}");

            return await Task.FromResult(groupId);
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<bool> AddMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            var added = group.Members.Add(username);
            if (added)
            {
                _logger.LogInformation($"User {username} added to group {group.Name} (ID: {groupId})");
            }
            return await Task.FromResult(added);
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> RemoveMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            var removed = group.Members.Remove(username);
            if (removed)
            {
                _logger.LogInformation($"User {username} removed from group {group.Name} (ID: {groupId})");

                // 그룹이 비어있으면 삭제
                if (group.Members.Count == 0)
                {
                    _groups.TryRemove(groupId, out _);
                    _logger.LogInformation($"Group {group.Name} (ID: {groupId}) deleted (no members left)");
                }
            }
            return await Task.FromResult(removed);
        }
        return await Task.FromResult(false);
    }

    public async Task<Group?> GetGroupAsync(string groupId)
    {
        _groups.TryGetValue(groupId, out var group);
        return await Task.FromResult(group);
    }

    public async Task<IEnumerable<Group>> GetGroupsByUserAsync(string username)
    {
        var userGroups = _groups.Values.Where(g => g.Members.Contains(username)).ToList();
        return await Task.FromResult(userGroups);
    }

    public async Task<IEnumerable<Group>> GetAllGroupsAsync()
    {
        var allGroups = _groups.Values.ToList();
        return await Task.FromResult(allGroups);
    }

    public async Task<bool> IsGroupMemberAsync(string groupId, string username)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            return await Task.FromResult(group.Members.Contains(username));
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> DeleteGroupAsync(string groupId)
    {
        var removed = _groups.TryRemove(groupId, out var group);
        if (removed && group != null)
        {
            _logger.LogInformation($"Group {group.Name} (ID: {groupId}) deleted");
        }
        return await Task.FromResult(removed);
    }

    public async Task<IEnumerable<string>> GetGroupMembersAsync(string groupId)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            return await Task.FromResult(group.Members.ToList());
        }
        return await Task.FromResult(Enumerable.Empty<string>());
    }
}