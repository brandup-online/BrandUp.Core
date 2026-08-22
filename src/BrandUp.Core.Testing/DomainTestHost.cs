using BrandUp.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Testing
{
    /// <summary>
    /// One-line test harness around <c>AddDomain</c>: builds a service provider (with scope
    /// validation on), opens a scope and exposes the domain services — replacing the
    /// ServiceCollection boilerplate every domain test otherwise repeats. Dispose the host to
    /// dispose the scope and the provider.
    /// </summary>
    public sealed class DomainTestHost : IDisposable, IAsyncDisposable
    {
        readonly ServiceProvider rootProvider;
        readonly AsyncServiceScope scope;

        DomainTestHost(ServiceProvider rootProvider, AsyncServiceScope scope)
        {
            this.rootProvider = rootProvider;
            this.scope = scope;
        }

        /// <summary>
        /// Service provider of the host's scope.
        /// </summary>
        public IServiceProvider Services => scope.ServiceProvider;

        /// <summary>
        /// The domain under test.
        /// </summary>
        public IDomain Domain => Services.GetRequiredService<IDomain>();

        /// <summary>
        /// The event publisher of the host's scope.
        /// </summary>
        public IDomainEventPublisher EventPublisher => Services.GetRequiredService<IDomainEventPublisher>();

        /// <summary>
        /// The event dispatcher of the host's scope (delivery side of an outbox).
        /// </summary>
        public IDomainEventDispatcher EventDispatcher => Services.GetRequiredService<IDomainEventDispatcher>();

        /// <summary>
        /// Events captured via <see cref="DomainOptionsTestingExtensions.CaptureEvent{TEvent}"/>.
        /// </summary>
        public EventCapture Events => Services.GetRequiredService<EventCapture>();

        /// <summary>
        /// The fake transaction factory; requires
        /// <see cref="DomainBuilderTestingExtensions.AddTestTransactions"/>.
        /// </summary>
        public TestTransactionFactory Transactions => Services.GetService<TestTransactionFactory>()
            ?? throw new InvalidOperationException($"Test transactions are not enabled. Call builder.{nameof(DomainBuilderTestingExtensions.AddTestTransactions)}() when creating the host.");

        /// <summary>
        /// The fake event outbox; requires
        /// <see cref="DomainBuilderTestingExtensions.AddTestEventOutbox"/>.
        /// </summary>
        public TestEventOutbox Outbox => Services.GetService<TestEventOutbox>()
            ?? throw new InvalidOperationException($"Test event outbox is not enabled. Call builder.{nameof(DomainBuilderTestingExtensions.AddTestEventOutbox)}() when creating the host.");

        /// <summary>
        /// Resolves a required service from the host's scope.
        /// </summary>
        /// <typeparam name="TService">Service type.</typeparam>
        public TService GetRequiredService<TService>()
            where TService : notnull
        {
            return Services.GetRequiredService<TService>();
        }

        /// <summary>
        /// Creates a host: registers <see cref="EventCapture"/>, configures the domain, applies
        /// the callbacks and opens a scope.
        /// </summary>
        /// <param name="configureDomain">Registers query, command and event handlers.</param>
        /// <param name="configureBuilder">Configures the domain builder (behaviors, validators, providers, test fakes).</param>
        /// <param name="configureServices">Registers additional services the handlers depend on.</param>
        public static DomainTestHost Create(Action<DomainOptions>? configureDomain = null, Action<IDomainBuilder>? configureBuilder = null, Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<EventCapture>();

            var builder = services.AddDomain(configureDomain);
            configureBuilder?.Invoke(builder);
            configureServices?.Invoke(services);

            var rootProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = rootProvider.CreateAsyncScope();

            return new DomainTestHost(rootProvider, scope);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            scope.Dispose();
            rootProvider.Dispose();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            await rootProvider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
