using BrandUp.Transactions;

namespace BrandUp.Testing
{
    /// <summary>
    /// Fake <see cref="ITransactionFactory"/> recording the transaction lifecycle as an
    /// operation log ("begin", "commit", "abort"). Mirrors the nesting contract of real
    /// factories: a Begin inside an active transaction returns a child handle whose commit and
    /// abort are no-ops, so only the outermost handle is recorded.
    /// </summary>
    public class TestTransactionFactory : ITransactionFactory
    {
        readonly List<string> operations = [];
        bool active;

        /// <summary>
        /// The recorded lifecycle operations, in order.
        /// </summary>
        public IReadOnlyList<string> Operations
        {
            get
            {
                lock (operations)
                    return [.. operations];
            }
        }

        /// <inheritdoc/>
        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            lock (operations)
            {
                if (active)
                    return Task.FromResult<ITransaction>(new TestTransaction(this, isChild: true));

                active = true;
                operations.Add("begin");
                return Task.FromResult<ITransaction>(new TestTransaction(this, isChild: false));
            }
        }

        void End(string operation)
        {
            lock (operations)
            {
                operations.Add(operation);
                active = false;
            }
        }

        sealed class TestTransaction(TestTransactionFactory factory, bool isChild) : ITransaction
        {
            bool committed;
            bool disposed;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                if (!isChild && !committed)
                {
                    committed = true;
                    factory.End("commit");
                }

                return Task.CompletedTask;
            }

            public void Dispose()
            {
                if (!disposed && !isChild && !committed)
                    factory.End("abort");
                disposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
