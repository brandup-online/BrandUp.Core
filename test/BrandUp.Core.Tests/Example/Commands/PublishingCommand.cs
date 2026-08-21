using System;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Example.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Example.Commands
{
    public class PublishingCommand : ICommand
    {
        public required string Phone { get; init; }
        public bool Fail { get; init; }
        public bool Throw { get; init; }
    }

    public class PublishingCommandHandler(IDomainEventPublisher eventPublisher, EventLog log) : ICommandHandler<PublishingCommand>
    {
        public async Task<Result> HandleAsync(PublishingCommand command, CancellationToken cancellationToken = default)
        {
            await eventPublisher.PublishAsync(new UserJoined { Phone = command.Phone }, cancellationToken);

            log.Add("command");

            if (command.Throw)
                throw new InvalidOperationException("Command failed.");
            if (command.Fail)
                return Result.Error("fail", "Command failed.");

            return Result.Success();
        }
    }

    public class NestingCommand : ICommand
    {
        public required string Phone { get; init; }
        public bool InnerFail { get; init; }
        public bool SwallowInnerError { get; init; }
    }

    public class NestingCommandHandler(IDomain domain, EventLog log) : ICommandHandler<NestingCommand>
    {
        public async Task<Result> HandleAsync(NestingCommand command, CancellationToken cancellationToken = default)
        {
            var innerResult = await domain.SendAsync(new PublishingCommand { Phone = command.Phone, Fail = command.InnerFail }, cancellationToken);

            log.Add("outer-command");

            return command.SwallowInnerError ? Result.Success() : innerResult;
        }
    }

	public interface INotRegisteredService
	{
	}

	public class MultiCtorCommand : ICommand
	{
	}

	// Two public constructors: registration must not throw, dispatch must pick the satisfiable one.
	public class MultiCtorCommandHandler : ICommandHandler<MultiCtorCommand>
	{
		public MultiCtorCommandHandler()
		{
		}

		public MultiCtorCommandHandler(INotRegisteredService service)
		{
		}

		public Task<Result> HandleAsync(MultiCtorCommand command, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Result.Success());
		}
	}

	public class ContextProbe
	{
		public System.Threading.ExecutionContext Context { get; set; }
	}

	// Captures the command's ExecutionContext (carrying its event scope) for post-command publishes.
	public class CaptureContextCommand : ICommand
	{
	}

	public class CaptureContextCommandHandler(ContextProbe probe) : ICommandHandler<CaptureContextCommand>
	{
		public Task<Result> HandleAsync(CaptureContextCommand command, CancellationToken cancellationToken = default)
		{
			probe.Context = System.Threading.ExecutionContext.Capture();
			return Task.FromResult(Result.Success());
		}
	}

	public class ThrowOnDisposeCommand : ICommand
	{
	}

	public class ThrowOnDisposeCommandHandler(IDomainEventPublisher eventPublisher, EventLog log) : ICommandHandler<ThrowOnDisposeCommand>, IDisposable
	{
		public async Task<Result> HandleAsync(ThrowOnDisposeCommand command, CancellationToken cancellationToken = default)
		{
			await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, cancellationToken);

			log.Add("command");

			return Result.Success();
		}

		public void Dispose()
		{
			throw new InvalidOperationException("Dispose failed.");
		}
	}

	public class CrossScopeCommand : ICommand
	{
	}

	// Publishes through a publisher from a different DI scope: a foreign command boundary.
	public class CrossScopeCommandHandler(IServiceScopeFactory scopeFactory, EventLog log) : ICommandHandler<CrossScopeCommand>
	{
		public async Task<Result> HandleAsync(CrossScopeCommand command, CancellationToken cancellationToken = default)
		{
			using var innerScope = scopeFactory.CreateScope();
			var innerPublisher = innerScope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

			await innerPublisher.PublishAsync(new UserJoined { Phone = "+inner" }, cancellationToken);

			log.Add("command");

			return Result.Success();
		}
	}
}
