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
}
