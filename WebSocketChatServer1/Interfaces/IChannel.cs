using ChatSystem.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatSystem.Interfaces;

public interface IChannel : IDisposable
{
    Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage;
    IAsyncEnumerable<T> ReceiveAsync<T>(CancellationToken cancellationToken) where T : BaseMessage;
}