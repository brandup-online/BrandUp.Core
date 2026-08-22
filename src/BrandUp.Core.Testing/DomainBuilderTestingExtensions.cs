using BrandUp.Events;
using BrandUp.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Testing
{
    /// <summary>
    /// <see cref="IDomainBuilder"/> extensions plugging the test fakes in.
    /// </summary>
    public static class DomainBuilderTestingExtensions
    {
        /// <summary>
        /// Wraps command dispatch in <see cref="TestTransactionFactory"/> transactions; the
        /// recorded lifecycle is available via <see cref="DomainTestHost.Transactions"/> (or by
        /// resolving <see cref="TestTransactionFactory"/>).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddTestTransactions(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddScoped<TestTransactionFactory>();
            builder.Services.AddScoped<ITransactionFactory>(provider => provider.GetRequiredService<TestTransactionFactory>());

            return builder.AddTransactions();
        }

        /// <summary>
        /// Routes deferred events into <see cref="TestEventOutbox"/> instead of in-process
        /// execution; the queue is available via <see cref="DomainTestHost.Outbox"/> (or by
        /// resolving <see cref="TestEventOutbox"/>).
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddTestEventOutbox(this IDomainBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddSingleton<TestEventOutbox>();
            builder.Services.AddSingleton<IEventOutbox>(provider => provider.GetRequiredService<TestEventOutbox>());

            // The outbox rerouting is an explicit opt-in, not a registration side effect.
            return builder.UseEventOutbox();
        }
    }
}
