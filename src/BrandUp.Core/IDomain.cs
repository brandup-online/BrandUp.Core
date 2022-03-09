using BrandUp.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp
{
    public interface IDomain
    {
        Task<Result> SendAsync(ICommand command, CancellationToken cancelationToken = default);

        Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancelationToken = default);
    }
}