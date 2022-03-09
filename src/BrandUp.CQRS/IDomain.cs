using BrandUp.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp
{
    public interface IDomain
    {
        Task<IResult> SendAsync(ICommand command, CancellationToken cancelationToken = default);

        Task<IResult<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancelationToken = default);
    }
}