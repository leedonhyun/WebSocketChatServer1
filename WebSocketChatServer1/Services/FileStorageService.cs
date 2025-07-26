using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Telemetry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

namespace WebSocketChatServer1.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<FileStorageService> _logger;
    private readonly ITelemetryService _telemetry;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger, ITelemetryService telemetry)
    {
        _storagePath = configuration.GetValue<string>("FileStorage:Path") ??
                      Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _logger = logger;
        _telemetry = telemetry;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> SaveFileAsync(string fileId, string fileName, byte[] data, bool append = false)
    {
        using var activity = ChatTelemetry.StartFileOperationActivity("SaveFile", fileName, data.Length);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(_storagePath, $"{fileId}_{sanitizedFileName}");

            activity?.SetTag("chat.file.path", filePath);
            activity?.SetTag("chat.file.append", append);

            var mode = append ? FileMode.Append : FileMode.Create;
            using var fileStream = new FileStream(filePath, mode, FileAccess.Write);
            await fileStream.WriteAsync(data);
            await fileStream.FlushAsync();

            stopwatch.Stop();
            _telemetry.RecordFileOperation("save", stopwatch.Elapsed.TotalMilliseconds, data.Length);
            _telemetry.IncrementFileUploads();

            _logger.LogDebug($"File saved: {filePath} ({data.Length} bytes)");
            return filePath;
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_save_error", ex.Message);
            _logger.LogError(ex, $"Error saving file: {fileName}");
            throw;
        }
    }

    public async Task<byte[]> ReadFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("ReadFile", Path.GetFileName(filePath), fileSize);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var data = await File.ReadAllBytesAsync(filePath);

            stopwatch.Stop();
            _telemetry.RecordFileOperation("read", stopwatch.Elapsed.TotalMilliseconds, data.Length);

            activity?.SetTag("chat.file.size", data.Length);
            return data;
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_read_error", ex.Message);
            _logger.LogError(ex, $"Error reading file: {filePath}");
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("FileExists", Path.GetFileName(filePath), fileSize);
        var exists = await Task.FromResult(File.Exists(filePath));
        activity?.SetTag("chat.file.exists", exists);
        return exists;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
        using var activity = ChatTelemetry.StartFileOperationActivity("DeleteFile", Path.GetFileName(filePath), fileSize);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                stopwatch.Stop();
                _telemetry.RecordFileOperation("delete", stopwatch.Elapsed.TotalMilliseconds, 0);
                _logger.LogDebug($"File deleted: {filePath}");
            }
            else
            {
                activity?.SetTag("chat.file.found", false);
            }
        }
        catch (Exception ex)
        {
            ChatTelemetry.RecordError(activity, ex);
            _telemetry.RecordError("file_delete_error", ex.Message);
            _logger.LogError(ex, $"Error deleting file: {filePath}");
            throw;
        }
        await Task.CompletedTask;
    }

    public string GetFilePath(string fileId, string fileName)
    {
        var sanitizedFileName = SanitizeFileName(fileName);
        return Path.Combine(_storagePath, $"{fileId}_{sanitizedFileName}");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
    }
}