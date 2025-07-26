using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;
using ChatSystem.Models;

namespace ChatSystem.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
        // MongoDB 단독 서버에서 트랜잭션 비활성화
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    public DbSet<UserActivity> UserActivities { get; set; }
    public DbSet<CommandLog> CommandLogs { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<RoomActivity> RoomActivities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MongoDB Collection 설정
        modelBuilder.Entity<UserActivity>().ToCollection("user_activities");
        modelBuilder.Entity<CommandLog>().ToCollection("command_logs");
        modelBuilder.Entity<UserProfile>().ToCollection("user_profiles");
        modelBuilder.Entity<RoomActivity>().ToCollection("room_activities");
    }
}
