using ChatSystem.Interfaces;
using ChatSystem.Models;
using System.Collections.Concurrent;

namespace ChatSystem.Services;

public class FileTransferStateService : IFileTransferStateService
{
    private readonly ConcurrentDictionary<string, FileTransferInfo> _activeTransfers = new();

    public bool TryAddTransfer(string fileId, FileTransferInfo fileInfo)
    {
        return _activeTransfers.TryAdd(fileId, fileInfo);
    }

    public bool TryGetTransfer(string fileId, out FileTransferInfo? fileInfo)
    {
        return _activeTransfers.TryGetValue(fileId, out fileInfo);
    }

    public bool TryRemoveTransfer(string fileId)
    {
        return _activeTransfers.TryRemove(fileId, out _);
    }
}