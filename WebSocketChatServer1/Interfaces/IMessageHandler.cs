using ChatSystem.Models;
using System.Threading.Tasks;

namespace ChatSystem.Interfaces;
public interface IMessageHandler<T> where T : BaseMessage
{
    Task HandleAsync(string clientId, T message);
}