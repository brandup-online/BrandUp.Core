using BrandUp.Commands;
using BrandUp.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp
{
    public interface IDomain
    {
        Task<Result<IList<TRow>>> ReadAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default);

        Task<Result> SendAsync(ICommand command, CancellationToken cancelationToken = default);

        Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancelationToken = default);
    }
}