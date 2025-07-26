using Microsoft.AspNetCore.Mvc;
using WebSocketChatServer1.Monitoring;
using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Models;
using System.ComponentModel.DataAnnotations;
using WebSocketChatServer1.Interfaces;

namespace WebSocketChatServer1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;
    private readonly IGroupManager _groupManager;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IMonitoringService monitoringService,
        IGroupManager groupManager,
        ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _groupManager = groupManager;
        _logger = logger;
    }

    /// <summary>
    /// 시스템 전체 상태 조회
    /// </summary>
    /// <returns>시스템 상태 정보 (총 연결 수, 메시지 수, 활성 사용자 수 등)</returns>
    /// <response code="200">시스템 상태 조회 성공</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemStatusDto), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<SystemStatusDto>> GetSystemStatus()
    {
        try
        {
            var status = await _monitoringService.GetSystemStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 명령어 통계 조회
    /// </summary>
    /// <param name="fromDate">조회 시작 날짜 (옵션)</param>
    /// <param name="toDate">조회 종료 날짜 (옵션)</param>
    /// <returns>명령어별 실행 통계</returns>
    /// <response code="200">명령어 통계 조회 성공</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("commands/stats")]
    [ProducesResponseType(typeof(List<CommandStatsDto>), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<List<CommandStatsDto>>> GetCommandStats(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var stats = await _monitoringService.GetCommandStatsAsync(fromDate, toDate);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting command stats");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 사용자 활동 조회
    /// </summary>
    [HttpGet("users/activity")]
    public async Task<ActionResult<List<UserActivityDto>>> GetUserActivity([FromQuery] int limit = 20)
    {
        try
        {
            var activity = await _monitoringService.GetUserActivityAsync(limit);
            return Ok(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user activity");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 최근 명령어 조회
    /// </summary>
    [HttpGet("commands/recent")]
    public async Task<ActionResult<List<WebSocketChatServer1.Models.CommandLog>>> GetRecentCommands([FromQuery] int limit = 50)
    {
        try
        {
            var commands = await _monitoringService.GetRecentCommandsAsync(limit);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent commands");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 특정 사용자의 명령어 이력 조회
    /// </summary>
    [HttpGet("users/{username}/commands")]
    public async Task<ActionResult<List<WebSocketChatServer1.Models.CommandLog>>> GetUserCommands(
        string username,
        [FromQuery] int limit = 50)
    {
        try
        {
            var commands = await _monitoringService.GetCommandsByUserAsync(username, limit);
            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user commands");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 에러 명령어 조회
    /// </summary>
    [HttpGet("commands/errors")]
    public async Task<ActionResult<List<WebSocketChatServer1.Models.CommandLog>>> GetErrorCommands(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var errors = await _monitoringService.GetErrorCommandsAsync(fromDate, limit);
            return Ok(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting error commands");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 대시보드용 종합 데이터 조회
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> GetDashboardData()
    {
        try
        {
            var status = await _monitoringService.GetSystemStatusAsync();
            var commandStats = await _monitoringService.GetCommandStatsAsync(DateTime.UtcNow.AddDays(-7));
            var userActivity = await _monitoringService.GetUserActivityAsync(10);
            var recentErrors = await _monitoringService.GetErrorCommandsAsync(DateTime.UtcNow.AddHours(-24), 20);

            var dashboard = new
            {
                Status = status,
                CommandStats = commandStats,
                UserActivity = userActivity,
                RecentErrors = recentErrors
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 명령어 실행 시간 통계 조회
    /// </summary>
    [HttpGet("performance")]
    public async Task<ActionResult<object>> GetPerformanceStats([FromQuery] DateTime? fromDate = null)
    {
        try
        {
            var from = fromDate ?? DateTime.UtcNow.AddDays(-1);
            var stats = await _monitoringService.GetCommandStatsAsync(from);

            var performance = new
            {
                SlowCommands = stats.Where(s => s.AvgExecutionTimeMs > 1000).OrderByDescending(s => s.AvgExecutionTimeMs),
                FastCommands = stats.Where(s => s.AvgExecutionTimeMs <= 100).OrderBy(s => s.AvgExecutionTimeMs),
                MostUsed = stats.OrderByDescending(s => s.Count).Take(10),
                WorstPerforming = stats.Where(s => s.SuccessRate < 0.95).OrderBy(s => s.SuccessRate)
            };

            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance stats");
            return StatusCode(500, "Internal server error");
        }
    }

    #region Room/Group Management APIs

    /// <summary>
    /// 전체 룸(그룹) 목록 조회
    /// </summary>
    /// <returns>모든 활성 룸 목록</returns>
    /// <response code="200">룸 목록 조회 성공</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("rooms")]
    [ProducesResponseType(typeof(List<RoomInfoDto>), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<List<RoomInfoDto>>> GetAllRooms()
    {
        try
        {
            var groups = await _groupManager.GetAllGroupsAsync();
            var rooms = groups.Select(g => new RoomInfoDto
            {
                Id = g.Id,
                Name = g.Name,
                CreatedBy = g.CreatedBy,
                CreatedAt = g.CreatedAt,
                MemberCount = g.Members.Count
            }).ToList();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all rooms");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 특정 룸(그룹) 상세 정보 조회
    /// </summary>
    /// <param name="roomId">룸 ID</param>
    /// <returns>룸 상세 정보</returns>
    /// <response code="200">룸 정보 조회 성공</response>
    /// <response code="404">룸을 찾을 수 없음</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("rooms/{roomId}")]
    [ProducesResponseType(typeof(RoomDetailDto), 200)]
    [ProducesResponseType(typeof(string), 404)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<RoomDetailDto>> GetRoomDetail(string roomId)
    {
        try
        {
            var group = await _groupManager.GetGroupAsync(roomId);
            if (group == null)
            {
                return NotFound($"Room with ID '{roomId}' not found");
            }

            var roomDetail = new RoomDetailDto
            {
                Id = group.Id,
                Name = group.Name,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                MemberCount = group.Members.Count,
                Members = group.Members.ToList()
            };

            return Ok(roomDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room detail for {RoomId}", roomId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 특정 사용자가 참여한 룸 목록 조회
    /// </summary>
    /// <param name="username">사용자명</param>
    /// <returns>사용자가 참여한 룸 목록</returns>
    /// <response code="200">사용자 룸 목록 조회 성공</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("users/{username}/rooms")]
    [ProducesResponseType(typeof(List<RoomInfoDto>), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<List<RoomInfoDto>>> GetUserRooms(string username)
    {
        try
        {
            var groups = await _groupManager.GetGroupsByUserAsync(username);
            var rooms = groups.Select(g => new RoomInfoDto
            {
                Id = g.Id,
                Name = g.Name,
                CreatedBy = g.CreatedBy,
                CreatedAt = g.CreatedAt,
                MemberCount = g.Members.Count
            }).ToList();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rooms for user {Username}", username);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 룸 통계 정보 조회
    /// </summary>
    /// <returns>룸 관련 통계 정보</returns>
    /// <response code="200">룸 통계 조회 성공</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("rooms/stats")]
    [ProducesResponseType(typeof(RoomStatsDto), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<RoomStatsDto>> GetRoomStats()
    {
        try
        {
            var groups = await _groupManager.GetAllGroupsAsync();
            var groupsList = groups.ToList();

            var totalRooms = groupsList.Count;
            var averageMembers = totalRooms > 0 ? groupsList.Average(g => g.Members.Count) : 0.0;
            var mostPopularRoom = groupsList.OrderByDescending(g => g.Members.Count).FirstOrDefault();
            var recentlyCreated = groupsList.OrderByDescending(g => g.CreatedAt).Take(5)
                .Select(g => new RoomInfoDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    CreatedBy = g.CreatedBy,
                    CreatedAt = g.CreatedAt,
                    MemberCount = g.Members.Count
                }).ToList();
            var emptyRooms = groupsList.Count(g => g.Members.Count == 0);

            var stats = new RoomStatsDto
            {
                TotalRooms = totalRooms,
                AverageMembers = Math.Round(averageMembers, 2),
                MostPopularRoom = mostPopularRoom != null ? new RoomInfoDto
                {
                    Id = mostPopularRoom.Id,
                    Name = mostPopularRoom.Name,
                    CreatedBy = mostPopularRoom.CreatedBy,
                    CreatedAt = mostPopularRoom.CreatedAt,
                    MemberCount = mostPopularRoom.Members.Count
                } : null,
                RecentlyCreatedRooms = recentlyCreated,
                EmptyRooms = emptyRooms,
                LastUpdated = DateTime.UtcNow
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room statistics");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 특정 룸의 멤버 목록 조회
    /// </summary>
    /// <param name="roomId">룸 ID</param>
    /// <returns>룸 멤버 목록</returns>
    /// <response code="200">룸 멤버 목록 조회 성공</response>
    /// <response code="404">룸을 찾을 수 없음</response>
    /// <response code="500">내부 서버 오류</response>
    [HttpGet("rooms/{roomId}/members")]
    [ProducesResponseType(typeof(List<string>), 200)]
    [ProducesResponseType(typeof(string), 404)]
    [ProducesResponseType(typeof(string), 500)]
    public async Task<ActionResult<List<string>>> GetRoomMembers(string roomId)
    {
        try
        {
            var group = await _groupManager.GetGroupAsync(roomId);
            if (group == null)
            {
                return NotFound($"Room with ID '{roomId}' not found");
            }

            var members = await _groupManager.GetGroupMembersAsync(roomId);
            return Ok(members.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for room {RoomId}", roomId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion
}

// DTOs for Room/Group APIs
public class RoomInfoDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}

public class RoomDetailDto : RoomInfoDto
{
    public List<string> Members { get; set; } = new();
}

public class RoomStatsDto
{
    public int TotalRooms { get; set; }
    public double AverageMembers { get; set; }
    public RoomInfoDto? MostPopularRoom { get; set; }
    public List<RoomInfoDto> RecentlyCreatedRooms { get; set; } = new();
    public int EmptyRooms { get; set; }
    public DateTime LastUpdated { get; set; }
}
