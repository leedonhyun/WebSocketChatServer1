// (Add this to your interfaces file)
using WebSocketChatServer1.Models;

public interface IFileTransferStateService
{
    bool TryAddTransfer(string fileId, FileTransferInfo fileInfo);
    bool TryGetTransfer(string fileId, out FileTransferInfo? fileInfo);
    bool TryRemoveTransfer(string fileId);
}