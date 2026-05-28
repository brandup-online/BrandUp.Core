using BrandUp.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Commands
{
    public interface ICounter
    {
        int Value { get; }
    }

    public class Counter : ICounter
    {
        public int Value => 42;
    }

    public class CounterCommand : ICommand<int>
    {
    }

    public class CounterCommandHandler : ICommandHandler<CounterCommand, int>
    {
        readonly ICounter counter;

        public CounterCommandHandler(ICounter counter)
        {
            this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        public Task<Result<int>> HandleAsync(CounterCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(counter.Value));
        }
    }

    public class SquareCounterCommand : ICommand<int>
    {
    }

    public class SquareCounterCommandHandler : ICommandHandler<SquareCounterCommand, int>
    {
        readonly ICounter counter;

        public SquareCounterCommandHandler(ICounter counter)
        {
            this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        public Task<Result<int>> HandleAsync(SquareCounterCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(counter.Value * counter.Value));
        }
    }

    public class FailingCommand : ICommand
    {
    }

    public class FailingCommandHandler : ICommandHandler<FailingCommand>
    {
        public Task<Result> HandleAsync(FailingCommand command, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    // Second handler for the same command type (CounterCommand) - used to test duplicate registration.
    public class AnotherCounterCommandHandler : ICommandHandler<CounterCommand, int>
    {
        public Task<Result<int>> HandleAsync(CounterCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(0));
        }
    }

    // Command declares a result, but its handler does not produce one - used to test the generic/non-generic guard.
    public class MixedCommand : ICommand<int>
    {
    }

    public class MixedCommandHandler : ICommandHandler<MixedCommand>
    {
        public Task<Result> HandleAsync(MixedCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    public class PingCommand : ICommand
    {
    }

    public class PingCommandHandler : ICommandHandler<PingCommand>
    {
        public Task<Result> HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }
}
