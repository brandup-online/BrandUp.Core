using System;
using BrandUp.Example.Behaviors;
using EventLog = BrandUp.Example.Events.EventLog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrandUp
{
    public class ValidateOnStartTest
    {
        static DomainOptions Resolve(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IOptions<DomainOptions>>().Value;
        }

        [Fact]
        public void OutboxWithoutStore_FailsValidation()
        {
            var serviceCollection = new ServiceCollection();

            // ValidateOnStart before the misconfiguration: the check sees the final registrations.
            serviceCollection.AddDomain()
                .ValidateOnStart()
                .UseEventOutbox();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            var exception = Assert.Throws<OptionsValidationException>(() => Resolve(serviceProvider));
            Assert.Contains("IEventOutbox", exception.Message);
        }

        [Fact]
        public void TransactionsWithoutFactory_FailsValidation()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain()
                .ValidateOnStart()
                .AddTransactions();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            var exception = Assert.Throws<OptionsValidationException>(() => Resolve(serviceProvider));
            Assert.Contains("ITransactionFactory", exception.Message);
        }

        [Fact]
        public void CachedQueryWithoutCaching_FailsValidation()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => options.AddQuery<CachedCountQueryHandler>())
                .ValidateOnStart();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            var exception = Assert.Throws<OptionsValidationException>(() => Resolve(serviceProvider));
            Assert.Contains(nameof(CachedCountQuery), exception.Message);
        }

        [Fact]
        public void IdempotentCommandWithoutIdempotency_FailsValidation()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => options.AddCommand<IdempotencyTest.PayCommandHandler>())
                .ValidateOnStart();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            var exception = Assert.Throws<OptionsValidationException>(() => Resolve(serviceProvider));
            Assert.Contains("IIdempotentCommand", exception.Message);
        }

        [Fact]
        public void TimedRequestWithoutTimeouts_FailsValidation()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => options.AddCommand<TimeoutTest.SlowCommandHandler>())
                .ValidateOnStart();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            var exception = Assert.Throws<OptionsValidationException>(() => Resolve(serviceProvider));
            Assert.Contains("IDispatchTimeout", exception.Message);
        }

        [Fact]
        public void ValidConfiguration_Passes()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<EventLog>();

            serviceCollection.AddDomain(options => options.AddQuery<CachedCountQueryHandler>())
                .ValidateOnStart()
                .AddQueryCaching()
                .AddTransactions<FakeTransactionFactory>()
                .AddEventOutbox<FakeEventOutbox>();

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            Assert.NotNull(Resolve(serviceProvider));
        }
    }
}
