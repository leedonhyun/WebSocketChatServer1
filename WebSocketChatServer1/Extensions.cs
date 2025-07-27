using WebSocketChatServer1.Commands;
using WebSocketChatServer1.Data;
using WebSocketChatServer1.Handlers;
using WebSocketChatServer1.Interfaces;
using WebSocketChatShared.Models;
using WebSocketChatServer1.Monitoring;
using WebSocketChatServer1.Server;
using WebSocketChatServer1.Services;
using WebSocketChatServer1.Telemetry;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Services;

namespace WebSocketChatServer1.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatSystem(this IServiceCollection services)
    {
        // Redis 연결 설정
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse("localhost:6379"); // Redis 연결 문자열 (실제 환경에 맞게 변경)
            return ConnectionMultiplexer.Connect(config);
        });

        // MongoDB 연결 설정 (선택적)
        services.AddSingleton<IMongoClient>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
            var logger = sp.GetRequiredService<ILogger<MongoClient>>();

            try
            {
                var client = new MongoClient(connectionString);

                // 연결 테스트 시도 (타임아웃 설정)
                var timeout = TimeSpan.FromSeconds(5);
                using var cts = new CancellationTokenSource(timeout);

                try
                {
                    client.ListDatabaseNames(cancellationToken: cts.Token);
                    logger.LogInformation("MongoDB connection successful");
                    return client;
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("MongoDB connection timeout after {Timeout} seconds", timeout.TotalSeconds);
                    return null!;
                }
            }
            catch (MongoAuthenticationException ex)
            {
                logger.LogWarning(ex, "MongoDB authentication failed. Check credentials.");
                return null!;
            }
            catch (MongoConnectionException ex)
            {
                logger.LogWarning(ex, "MongoDB connection refused or failed. Server may not be running.");
                return null!;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MongoDB connection failed. Falling back to null command logger.");
                return null!;
            }
        });

        // Entity Framework Core with MongoDB 설정
        services.AddDbContext<ChatDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
            var databaseName = configuration.GetValue<string>("MongoDB:DatabaseName") ?? "WebSocketChatServer";

            // MongoDB 클라이언트가 사용 가능한지 먼저 확인
            var mongoClient = serviceProvider.GetService<IMongoClient>();
            if (mongoClient == null)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ChatDbContext>>();
                logger.LogWarning("MongoDB client is not available. DbContext will be configured but may fail at runtime.");
            }

            try
            {
                options.UseMongoDB(connectionString, databaseName);
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ChatDbContext>>();
                logger.LogError(ex, "Failed to configure MongoDB for Entity Framework Core");
                throw; // 설정 시점에서는 예외를 다시 던져야 함
            }
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client = sp.GetService<IMongoClient>();
            if (client == null) return null!;

            var configuration = sp.GetRequiredService<IConfiguration>();
            var databaseName = configuration.GetValue<string>("MongoDB:DatabaseName") ?? "WebSocketChatServer";
            return client.GetDatabase(databaseName);
        });

        // Entity Framework Core 기반 서비스들
        services.AddScoped<IUserActivityService, UserActivityService>();
        services.AddScoped<IRoomActivityService, RoomActivityService>();

        // Command Logger 등록 (MongoDB 연결 상태에 따라 선택)
        services.AddScoped<WebSocketChatServer1.Interfaces.ICommandLogger>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<NullCommandLogger>>();

            try
            {
                // 먼저 MongoDB 연결을 확인
                var mongoClient = serviceProvider.GetService<IMongoClient>();
                if (mongoClient == null)
                {
                    logger.LogWarning("MongoDB client is not available (connection failed or server not running), using null command logger");
                    return new NullCommandLogger(logger);
                }

                // DbContext 생성 시도
                try
                {
                    var context = serviceProvider.GetRequiredService<ChatDbContext>();
                    var efLogger = serviceProvider.GetRequiredService<ILogger<EfCoreCommandLogger>>();

                    // MongoDB 연결 및 인증 테스트
                    var database = serviceProvider.GetService<IMongoDatabase>();
                    if (database != null)
                    {
                        // 실제 데이터베이스 작업을 시도하여 인증 확인
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                            database.ListCollectionNames(cancellationToken: cts.Token).FirstOrDefault();

                            // 간단한 컬렉션 접근 테스트
                            var testCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("test_connection");
                            _ = testCollection.EstimatedDocumentCount(cancellationToken: cts.Token);

                            efLogger.LogInformation("MongoDB connection and authentication successful, using EfCoreCommandLogger");
                            return new EfCoreCommandLogger(context, efLogger);
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogWarning("MongoDB operation timeout, using null command logger");
                            return new NullCommandLogger(logger);
                        }
                        catch (MongoDB.Driver.MongoAuthenticationException authEx)
                        {
                            logger.LogWarning(authEx, "MongoDB authentication failed, using null command logger");
                            return new NullCommandLogger(logger);
                        }
                        catch (MongoDB.Driver.MongoConnectionException connEx)
                        {
                            logger.LogWarning(connEx, "MongoDB connection failed (server not responding), using null command logger");
                            return new NullCommandLogger(logger);
                        }
                    }

                    // 여기에 도달하면 database가 null
                    logger.LogWarning("MongoDB database is not available, using null command logger");
                    return new NullCommandLogger(logger);
                }
                catch (Exception dbEx)
                {
                    logger.LogWarning(dbEx, "Failed to create ChatDbContext, using null command logger");
                    return new NullCommandLogger(logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create EfCoreCommandLogger, using null command logger");
                return new NullCommandLogger(logger);
            }
        });

        services.AddSingleton<IMonitoringService>(sp =>
        {
            var database = sp.GetService<IMongoDatabase>();
            if (database != null)
            {
                var logger = sp.GetRequiredService<ILogger<MonitoringService>>();
                return new MonitoringService(database, logger);
            }
            else
            {
                var logger = sp.GetRequiredService<ILogger<NullMonitoringService>>();
                return new NullMonitoringService(logger);
            }
        });

        // Telemetry 서비스 등록
        services.AddSingleton<ITelemetryService, TelemetryService>();

        // services.AddSingleton<IClientManager, ClientManager>();
        services.AddSingleton<IRoomManager, RoomManager>(); // room 관리자 추가
        services.AddSingleton<IFileStorageService, FileStorageService>();
        // services.AddSingleton<IMessageBroadcaster, MessageBroadcaster>();

        //// 기존 ClientManager를 분산 환경에 맞게 수정하거나 Redis 기반 ClientManager 구현
        services.AddSingleton<IClientManager, DistributedClientManager>(); // 예시: 분산 ClientManager

        //// MessageBroadcaster 대신 RedisMessageBroadcaster 구현체 사용
        services.AddSingleton<IMessageBroadcaster, RedisMessageBroadcaster>();

        // 핸들러 등록
        services.AddScoped<IMessageHandler<ChatMessage>, ChatMessageHandler>();
        services.AddScoped<IMessageHandler<FileTransferMessage>, FileTransferHandler>();
        services.AddSingleton<IFileTransferStateService, FileTransferStateService>();
        // 명령 처리기 등록
        services.AddScoped<ICommandProcessor, UsernameCommandProcessor>();
        services.AddScoped<ICommandProcessor, UserListCommandProcessor>();
        services.AddScoped<ICommandProcessor, PrivateMessageCommandProcessor>(); // 개인 메시지
        services.AddScoped<ICommandProcessor, CreateRoomCommandProcessor>(); // room 생성
        services.AddScoped<ICommandProcessor, JoinRoomCommandProcessor>(); // 그룹 참가
        //services.AddScoped<ICommandProcessor, GroupChatCommandProcessor>(); // 그룹 채팅
        services.AddScoped<ICommandProcessor, RoomMessageCommandProcessor>(); // 룸 메시지
        services.AddScoped<ICommandProcessor, SendFileCommandProcessor>(); // 파일 전송

        // 메인 서버
        services.AddScoped<ChatServer>();

        // 로깅
        services.AddLogging();

        return services;
    }

    public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = ChatTelemetry.ServiceName;
        var serviceVersion = ChatTelemetry.ServiceVersion;

        // OpenTelemetry 설정
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("service.namespace", "chat"),
                    new KeyValuePair<string, object>("service.instance.id", Environment.MachineName),
                    new KeyValuePair<string, object>("deployment.environment",
                        configuration.GetValue<string>("Environment") ?? "development")
                }))
            .WithTracing(tracing => tracing
                .AddSource(ChatTelemetry.ActivitySource.Name)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                    {
                        // WebSocket 요청도 추적
                        return true;
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(ChatTelemetry.Meter.Name)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddConsoleExporter()
                .AddPrometheusExporter());

        return services;
    }

    public static ILoggingBuilder AddOpenTelemetryLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(ChatTelemetry.ServiceName, ChatTelemetry.ServiceVersion));

            options.AddConsoleExporter();
        });

        return logging;
    }
}