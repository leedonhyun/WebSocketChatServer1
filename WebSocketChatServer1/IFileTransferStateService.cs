// (Add this to your interfaces file)
using ChatSystem.Models;

public interface IFileTransferStateService
{
    bool TryAddTransfer(string fileId, FileTransferInfo fileInfo);
    bool TryGetTransfer(string fileId, out FileTransferInfo? fileInfo);
    bool TryRemoveTransfer(string fileId);
}