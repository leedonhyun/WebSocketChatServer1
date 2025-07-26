using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSocketChatServer.UserApi.Models;
using WebSocketChatServer.UserApi.Services;

namespace WebSocketChatServer.UserApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserInfo>>> GetUsers([FromQuery] int skip = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var users = await _userService.GetUsersAsync(skip, limit);
            var userInfos = users.Select(u => new UserInfo
            {
                Id = u.Id ?? string.Empty,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                Roles = u.Roles
            }).ToList();

            return Ok(userInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserInfo>> GetUser(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var userInfo = new UserInfo
            {
                Id = user.Id ?? string.Empty,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Roles = user.Roles
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var userInfo = new UserInfo
            {
                Id = user.Id ?? string.Empty,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Roles = user.Roles
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserInfo>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            // Users can only update their own profile (unless they have admin role)
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            if (currentUserId != id && !isAdmin)
                return Forbid();

            var updatedUser = await _userService.UpdateUserAsync(id, request);
            if (updatedUser == null)
                return NotFound(new { message = "User not found" });

            var userInfo = new UserInfo
            {
                Id = updatedUser.Id ?? string.Empty,
                Username = updatedUser.Username,
                Email = updatedUser.Email,
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName,
                IsActive = updatedUser.IsActive,
                CreatedAt = updatedUser.CreatedAt,
                LastLoginAt = updatedUser.LastLoginAt,
                Roles = updatedUser.Roles
            };

            _logger.LogInformation("User updated: {UserId}", id);
            return Ok(userInfo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("User update failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("{id}/change-password")]
    public async Task<IActionResult> ChangePassword(string id, [FromBody] ChangePasswordRequest request)
    {
        try
        {
            // Users can only change their own password (unless they have admin role)
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            if (currentUserId != id && !isAdmin)
                return Forbid();

            var success = await _userService.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword);
            if (!success)
                return BadRequest(new { message = "Invalid current password or user not found" });

            _logger.LogInformation("Password changed for user: {UserId}", id);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var success = await _userService.DeleteUserAsync(id);
            if (!success)
                return NotFound(new { message = "User not found" });

            _logger.LogInformation("User deleted: {UserId}", id);
            return Ok(new { message = "User deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
