using WebSocketChatServer1.Models;

namespace WebSocketChatServer1.Interfaces
{
    public interface IFileTransferStateService
    {
        void AddOrUpdateTransferState(string fileId, FileTransferState state);
        FileTransferState GetTransferState(string fileId);
        void RemoveTransferState(string fileId);
    }
}