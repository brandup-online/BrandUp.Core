using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.CQRS
{
    public class Domain : IDomain
    {
        readonly IDomainStore store;

        public Domain(IDomainStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task NotoficationAsync(INotification notofication, CancellationToken cancelationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancelationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}