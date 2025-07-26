using ChatSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

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