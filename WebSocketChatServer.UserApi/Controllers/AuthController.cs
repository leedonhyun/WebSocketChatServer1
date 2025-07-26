using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSocketChatServer.UserApi.Models;
using WebSocketChatServer.UserApi.Services;

namespace WebSocketChatServer.UserApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(request);
            var token = _jwtTokenService.GenerateToken(user);

            var response = new LoginResponse
            {
                AccessToken = token,
                ExpiresIn = 3600, // 1 hour
                User = new UserInfo
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
                }
            };

            _logger.LogInformation("User registered successfully: {Username}", user.Username);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userService.AuthenticateUserAsync(request.Username, request.Password);

            if (user == null)
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            var token = _jwtTokenService.GenerateToken(user);

            var response = new LoginResponse
            {
                AccessToken = token,
                ExpiresIn = 3600, // 1 hour
                User = new UserInfo
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
                }
            };

            _logger.LogInformation("User logged in successfully: {Username}", user.Username);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("validate-token")]
    [Authorize]
    public IActionResult ValidateToken()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return Ok(new
        {
            valid = true,
            userId,
            username,
            roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray()
        });
    }
}