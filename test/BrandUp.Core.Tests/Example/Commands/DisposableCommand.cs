using BrandUp.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public class DisposeProbe
    {
        public bool Disposed { get; set; }
        public bool AsyncDisposed { get; set; }
    }

    public class DisposableCommand : ICommand
    {
    }

    public class DisposableCommandHandler : ICommandHandler<DisposableCommand>, IDisposable
    {
        readonly DisposeProbe probe;

        public DisposableCommandHandler(DisposeProbe probe)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public Task<Result> HandleAsync(DisposableCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }

        public void Dispose()
        {
            probe.Disposed = true;
        }
    }

    public class AsyncDisposableCommand : ICommand
    {
    }

    public class AsyncDisposableCommandHandler : ICommandHandler<AsyncDisposableCommand>, IAsyncDisposable
    {
        readonly DisposeProbe probe;

        public AsyncDisposableCommandHandler(DisposeProbe probe)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public Task<Result> HandleAsync(AsyncDisposableCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }

        public ValueTask DisposeAsync()
        {
            probe.AsyncDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
