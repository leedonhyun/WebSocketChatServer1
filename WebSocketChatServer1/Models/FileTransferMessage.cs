namespace ChatSystem.Models;

public class FileTransferMessage : BaseMessage
{
    public string FileId { get; set; } = "";
    public FileTransferInfo? FileInfo { get; set; }
    public byte[]? Data { get; set; }
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string FromUsername { get; set; } = "";
    public string ToUsername { get; set; } = "";
}