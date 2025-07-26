using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string fileId, string fileName, byte[] data, bool append = false);
    Task<byte[]> ReadFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task DeleteFileAsync(string filePath);
    string GetFilePath(string fileId, string fileName);
}