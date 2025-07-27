using WebSocketChatShared.Models;

namespace WebSocketChatServer1.Interfaces
{
    public interface IFileTransferStateService
    {
        //void AddOrUpdateTransferState(string fileId, FileTransferState state);
        //FileTransferState GetTransferState(string fileId);
        //void RemoveTransferState(string fileId);
        bool TryAddTransfer(string fileId, FileTransferInfo fileInfo);
        bool TryGetTransfer(string fileId, out FileTransferInfo? fileInfo);
        bool TryRemoveTransfer(string fileId);
    }
}