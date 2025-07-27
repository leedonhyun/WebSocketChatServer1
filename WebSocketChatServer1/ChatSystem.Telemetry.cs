using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WebSocketChatServer1.Telemetry;

/// <summary>
/// WebSocket 채팅 서버의 텔레메트리를 관리하는 정적 클래스
/// OpenTelemetry 및 Prometheus와 호환되는 메트릭과 추적을 제공합니다.
/// </summary>
public static class ChatTelemetry
{
    public static readonly string ServiceName = "WebSocketChatServer";
    public static readonly string ServiceVersion = "1.0.0";

    // Activity Source for distributed tracing
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // Meter for metrics collection
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    #region Counters (Rate Metrics)

    /// <summary>총 클라이언트 연결 수 (누적)</summary>
    public static readonly Counter<long> ClientConnectionsTotal = Meter.CreateCounter<long>(
        "websocket_chat_client_connections_total",
        "connections",
        "Total number of client connections established");

    /// <summary>처리된 메시지 총 수</summary>
    public static readonly Counter<long> MessagesProcessedTotal = Meter.CreateCounter<long>(
        "websocket_chat_messages_processed_total",
        "messages",
        "Total number of chat messages processed");

    /// <summary>실행된 명령어 총 수</summary>
    public static readonly Counter<long> CommandsExecutedTotal = Meter.CreateCounter<long>(
        "websocket_chat_commands_executed_total",
        "commands",
        "Total number of commands executed");

    /// <summary>명령어 오류 총 수</summary>
    public static readonly Counter<long> CommandErrorsTotal = Meter.CreateCounter<long>(
        "websocket_chat_command_errors_total",
        "errors",
        "Total number of command execution errors");

    /// <summary>개인 메시지 총 수</summary>
    public static readonly Counter<long> PrivateMessagesTotal = Meter.CreateCounter<long>(
        "websocket_chat_private_messages_total",
        "messages",
        "Total number of private messages sent");

    /// <summary>그룹 작업 총 수</summary>
    public static readonly Counter<long> GroupOperationsTotal = Meter.CreateCounter<long>(
        "websocket_chat_group_operations_total",
        "operations",
        "Total number of group operations performed");

    /// <summary>사용자명 변경 총 수</summary>
    public static readonly Counter<long> UsernameChangesTotal = Meter.CreateCounter<long>(
        "websocket_chat_username_changes_total",
        "changes",
        "Total number of username changes");

    /// <summary>전송된 파일 총 수</summary>
    public static readonly Counter<long> FilesTransferredTotal = Meter.CreateCounter<long>(
        "websocket_chat_files_transferred_total",
        "files",
        "Total number of files transferred");

    /// <summary>생성된 그룹 총 수</summary>
    public static readonly Counter<long> GroupsCreatedTotal = Meter.CreateCounter<long>(
        "websocket_chat_groups_created_total",
        "groups",
        "Total number of groups created");

    /// <summary>발생한 오류 총 수</summary>
    public static readonly Counter<long> ErrorsOccurredTotal = Meter.CreateCounter<long>(
        "websocket_chat_errors_total",
        "errors",
        "Total number of errors occurred");

    #endregion

    #region Gauges (Current State Metrics)

    // Thread-safe counters for current state
    private static long _activeConnections = 0;
    private static long _activeGroups = 0;

    /// <summary>현재 활성 연결 수</summary>
    public static readonly ObservableGauge<long> ActiveConnections = Meter.CreateObservableGauge<long>(
        "websocket_chat_active_connections",
        () => _activeConnections,
        "connections",
        "Current number of active WebSocket connections");

    /// <summary>현재 활성 그룹 수</summary>
    public static readonly ObservableGauge<long> ActiveGroups = Meter.CreateObservableGauge<long>(
        "websocket_chat_active_groups",
        () => _activeGroups,
        "groups",
        "Current number of active chat groups");

    #endregion

    #region Histograms (Distribution Metrics)

    /// <summary>메시지 처리 시간 분포</summary>
    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        "websocket_chat_message_processing_duration_seconds",
        "seconds",
        "Duration of message processing in seconds");

    /// <summary>명령어 실행 시간 분포</summary>
    public static readonly Histogram<double> CommandExecutionDuration = Meter.CreateHistogram<double>(
        "websocket_chat_command_execution_duration_seconds",
        "seconds",
        "Duration of command execution in seconds");

    /// <summary>파일 전송 시간 분포</summary>
    public static readonly Histogram<double> FileTransferDuration = Meter.CreateHistogram<double>(
        "websocket_chat_file_transfer_duration_seconds",
        "seconds",
        "Duration of file transfer in seconds");

    /// <summary>메시지 크기 분포</summary>
    public static readonly Histogram<long> MessageSizeBytes = Meter.CreateHistogram<long>(
        "websocket_chat_message_size_bytes",
        "bytes",
        "Size distribution of chat messages in bytes");

    /// <summary>파일 크기 분포</summary>
    public static readonly Histogram<long> FileSizeBytes = Meter.CreateHistogram<long>(
        "websocket_chat_file_size_bytes",
        "bytes",
        "Size distribution of transferred files in bytes");

    #endregion

    #region Activity Names for Distributed Tracing

    public const string ActivityClientConnection = "websocket.client.connection";
    public const string ActivityMessageProcessing = "websocket.message.processing";
    public const string ActivityFileTransfer = "websocket.file.transfer";
    public const string ActivityGroupOperation = "websocket.group.operation";
    public const string ActivityBroadcast = "websocket.broadcast";
    public const string ActivityPrivateMessage = "websocket.private_message";
    public const string ActivityCommandExecution = "websocket.command.execution";

    #endregion

    #region Helper Methods for Activity Creation

    /// <summary>클라이언트 연결 Activity 시작</summary>
    public static Activity? StartClientConnectionActivity(string clientId, string username)
    {
        var activity = ActivitySource.StartActivity(ActivityClientConnection);
        activity?.SetTag("websocket.client.id", clientId);
        activity?.SetTag("websocket.client.username", username);
        activity?.SetTag("websocket.server.instance", Environment.MachineName);
        return activity;
    }

    /// <summary>메시지 처리 Activity 시작</summary>
    public static Activity? StartMessageProcessingActivity(string messageType, string clientId)
    {
        var activity = ActivitySource.StartActivity(ActivityMessageProcessing);
        activity?.SetTag("websocket.message.type", messageType);
        activity?.SetTag("websocket.client.id", clientId);
        return activity;
    }

    /// <summary>파일 전송 Activity 시작</summary>
    public static Activity? StartFileTransferActivity(string fileId, string fileName, long fileSize)
    {
        var activity = ActivitySource.StartActivity(ActivityFileTransfer);
        activity?.SetTag("websocket.file.id", fileId);
        activity?.SetTag("websocket.file.name", fileName);
        activity?.SetTag("websocket.file.size", fileSize);
        return activity;
    }

    /// <summary>파일 작업 Activity 시작 (레거시 호환성)</summary>
    public static Activity? StartFileOperationActivity(string operation, string fileName, long fileSize)
    {
        var activity = ActivitySource.StartActivity(ActivityFileTransfer);
        activity?.SetTag("websocket.file.operation", operation);
        activity?.SetTag("websocket.file.name", fileName);
        activity?.SetTag("websocket.file.size", fileSize);
        return activity;
    }

    /// <summary>그룹 작업 Activity 시작</summary>
    //public static Activity? StartGroupOperationActivity(string operation, string groupId)
    //{
    //    var activity = ActivitySource.StartActivity(ActivityGroupOperation);
    //    activity?.SetTag("websocket.group.operation", operation);
    //    activity?.SetTag("websocket.group.id", groupId);
    //    return activity;
    //}

    /// <summary>브로드캐스트 Activity 시작</summary>
    public static Activity? StartBroadcastActivity(int recipientCount, string messageType)
    {
        var activity = ActivitySource.StartActivity(ActivityBroadcast);
        activity?.SetTag("websocket.broadcast.recipient_count", recipientCount);
        activity?.SetTag("websocket.message.type", messageType);
        return activity;
    }

    /// <summary>개인 메시지 Activity 시작</summary>
    public static Activity? StartPrivateMessageActivity(string fromClient, string toClient)
    {
        var activity = ActivitySource.StartActivity(ActivityPrivateMessage);
        activity?.SetTag("websocket.private_message.from", fromClient);
        activity?.SetTag("websocket.private_message.to", toClient);
        return activity;
    }

    /// <summary>명령어 실행 Activity 시작</summary>
    public static Activity? StartCommandActivity(string commandType, string clientId)
    {
        var activity = ActivitySource.StartActivity(ActivityCommandExecution);
        activity?.SetTag("websocket.command.type", commandType);
        activity?.SetTag("websocket.client.id", clientId);
        return activity;
    }

    /// <summary>일반적인 Activity 시작</summary>
    public static Activity? StartActivity(string name)
    {
        return ActivitySource.StartActivity(name);
    }

    #endregion

    #region State Management Methods

    /// <summary>활성 연결 수 증가</summary>
    public static void IncrementActiveConnections()
    {
        Interlocked.Increment(ref _activeConnections);
    }

    /// <summary>활성 연결 수 감소</summary>
    public static void DecrementActiveConnections()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    /// <summary>활성 그룹 수 증가</summary>
    public static void IncrementActiveGroups()
    {
        Interlocked.Increment(ref _activeGroups);
    }

    /// <summary>활성 그룹 수 감소</summary>
    public static void DecrementActiveGroups()
    {
        Interlocked.Decrement(ref _activeGroups);
    }

    /// <summary>활성 연결 수 설정</summary>
    public static void SetActiveConnections(long count)
    {
        Interlocked.Exchange(ref _activeConnections, count);
    }

    /// <summary>활성 그룹 수 설정</summary>
    public static void SetActiveGroups(long count)
    {
        Interlocked.Exchange(ref _activeGroups, count);
    }

    #endregion

    #region Error Recording

    /// <summary>오류 기록</summary>
    public static void RecordError(Activity? activity, Exception exception, string? context = null)
    {
        ErrorsOccurredTotal.Add(1,
            new KeyValuePair<string, object?>("error.type", exception.GetType().Name),
            new KeyValuePair<string, object?>("error.message", exception.Message),
            new KeyValuePair<string, object?>("error.context", context ?? "unknown"));

        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetTag("error.stack_trace", exception.StackTrace);

        if (context != null)
        {
            activity?.SetTag("error.context", context);
        }
    }

    #endregion

    #region Resource Cleanup

    /// <summary>텔레메트리 리소스 정리</summary>
    public static void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }

    #endregion
}

#region Legacy Interface Support for Backwards Compatibility

public interface ITelemetryService
{
    void IncrementClientConnections();
    void DecrementClientConnections();
    void IncrementActiveUsers();
    void DecrementActiveUsers();
    void IncrementGroups();
    void DecrementGroups();
    void IncrementFileUploads();
    void RecordMessageProcessed(string messageType, double durationMs, long sizeBytes);
    void RecordFileTransferred(string fileType, double durationMs, long sizeBytes);
    void RecordFileOperation(string operation, double durationMs, int sizeBytes);
    void RecordGroupCreated(string groupType);
    void RecordError(string errorType, string errorMessage);
    void UpdateActiveConnections(int count);
    void UpdateActiveGroups(int count);
}

/// <summary>
/// Legacy telemetry service implementation for backward compatibility
/// Redirects calls to the new ChatTelemetry static methods
/// </summary>
public class TelemetryService : ITelemetryService
{
    public void IncrementClientConnections()
    {
        ChatTelemetry.ClientConnectionsTotal.Add(1);
        ChatTelemetry.IncrementActiveConnections();
    }

    public void DecrementClientConnections()
    {
        ChatTelemetry.DecrementActiveConnections();
    }

    public void IncrementActiveUsers()
    {
        // This is now handled by connection management
        ChatTelemetry.ClientConnectionsTotal.Add(1);
    }

    public void DecrementActiveUsers()
    {
        // This is now handled by connection management
    }

    public void IncrementGroups()
    {
        ChatTelemetry.GroupsCreatedTotal.Add(1);
        ChatTelemetry.IncrementActiveGroups();
    }

    public void DecrementGroups()
    {
        ChatTelemetry.DecrementActiveGroups();
    }

    public void IncrementFileUploads()
    {
        ChatTelemetry.FilesTransferredTotal.Add(1);
    }

    public void RecordFileOperation(string operation, double durationMs, int sizeBytes)
    {
        ChatTelemetry.FileTransferDuration.Record(durationMs / 1000.0, // Convert to seconds
            new KeyValuePair<string, object?>("operation", operation));
        ChatTelemetry.FileSizeBytes.Record(sizeBytes,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordMessageProcessed(string messageType, double durationMs, long sizeBytes)
    {
        ChatTelemetry.MessagesProcessedTotal.Add(1,
            new KeyValuePair<string, object?>("message.type", messageType));

        ChatTelemetry.MessageProcessingDuration.Record(durationMs / 1000.0, // Convert to seconds
            new KeyValuePair<string, object?>("message.type", messageType));

        ChatTelemetry.MessageSizeBytes.Record(sizeBytes,
            new KeyValuePair<string, object?>("message.type", messageType));
    }

    public void RecordFileTransferred(string fileType, double durationMs, long sizeBytes)
    {
        ChatTelemetry.FilesTransferredTotal.Add(1,
            new KeyValuePair<string, object?>("file.type", fileType));

        ChatTelemetry.FileTransferDuration.Record(durationMs / 1000.0, // Convert to seconds
            new KeyValuePair<string, object?>("file.type", fileType));

        ChatTelemetry.FileSizeBytes.Record(sizeBytes,
            new KeyValuePair<string, object?>("file.type", fileType));
    }

    public void RecordGroupCreated(string groupType)
    {
        ChatTelemetry.GroupsCreatedTotal.Add(1,
            new KeyValuePair<string, object?>("group.type", groupType));

        ChatTelemetry.IncrementActiveGroups();
    }

    public void RecordError(string errorType, string errorMessage)
    {
        ChatTelemetry.ErrorsOccurredTotal.Add(1,
            new KeyValuePair<string, object?>("error.type", errorType),
            new KeyValuePair<string, object?>("error.message", errorMessage));
    }

    public void UpdateActiveConnections(int count)
    {
        ChatTelemetry.SetActiveConnections(count);
    }

    public void UpdateActiveGroups(int count)
    {
        ChatTelemetry.SetActiveGroups(count);
    }
}

#endregion
