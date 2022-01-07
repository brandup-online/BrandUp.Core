using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.CQRS
{
    public interface IDomain
    {
        Task NotoficationAsync(INotification notofication, CancellationToken cancelationToken = default);

        Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancelationToken = default);
    }
}