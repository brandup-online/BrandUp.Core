using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;

namespace BrandUp.Testing
{
    /// <summary>
    /// <see cref="IDomain"/> extensions that dispatch and assert the outcome in one call:
    /// the success family returns the produced data, the error family returns the matched error.
    /// </summary>
    public static class DomainAssertExtensions
    {
        /// <summary>
        /// Dispatches the command and asserts it succeeded.
        /// </summary>
        /// <param name="domain">Domain instance.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="DomainAssertException">The command failed.</exception>
        public static async Task AssertSendAsync(this IDomain domain, ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            (await domain.SendAsync(command, cancellationToken)).AssertSuccess();
        }

        /// <summary>
        /// Dispatches the command, asserts it succeeded, optionally checks the produced data and returns it.
        /// </summary>
        /// <typeparam name="TResult">Type of the produced result data.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="check">Optional additional check over the produced data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The produced data.</returns>
        /// <exception cref="DomainAssertException">The command failed.</exception>
        public static async Task<TResult> AssertSendAsync<TResult>(this IDomain domain, ICommand<TResult> command, Action<TResult>? check = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.SendAsync(command, cancellationToken)).AssertSuccess(check);
        }

        /// <summary>
        /// Dispatches the item command and asserts it succeeded, optionally checking the item afterwards.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="checkItem">Optional check over the item after execution.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="DomainAssertException">The command failed.</exception>
        public static async Task AssertSendItemAsync<TId, TItem>(this IDomain domain, IItem<TId> item, IItemCommand<TItem> command, Action<TItem>? checkItem = null, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);

            (await domain.SendItemAsync(item, command, cancellationToken)).AssertSuccess();

            checkItem?.Invoke((TItem)item);
        }

        /// <summary>
        /// Dispatches the item command, asserts it succeeded, optionally checks data and item, and returns the data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <typeparam name="TResult">Type of the produced result data.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="check">Optional check over the produced data and the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The produced data.</returns>
        /// <exception cref="DomainAssertException">The command failed.</exception>
        public static async Task<TResult> AssertSendItemAsync<TId, TItem, TResult>(this IDomain domain, IItem<TId> item, IItemCommand<TItem, TResult> command, Action<TResult, TItem>? check = null, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);

            var data = (await domain.SendItemAsync(item, command, cancellationToken)).AssertSuccess();

            check?.Invoke(data, (TItem)item);

            return data;
        }

        /// <summary>
        /// Dispatches the item command, asserts it succeeded, runs an asynchronous check over data
        /// and item, and returns the data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <typeparam name="TResult">Type of the produced result data.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="checkAsync">Asynchronous check over the produced data and the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The produced data.</returns>
        /// <exception cref="DomainAssertException">The command failed.</exception>
        public static async Task<TResult> AssertSendItemAsync<TId, TItem, TResult>(this IDomain domain, IItem<TId> item, IItemCommand<TItem, TResult> command, Func<TResult, TItem, Task> checkAsync, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);
            ArgumentNullException.ThrowIfNull(checkAsync);

            var data = (await domain.SendItemAsync(item, command, cancellationToken)).AssertSuccess();

            await checkAsync(data, (TItem)item);

            return data;
        }

        /// <summary>
        /// Dispatches the command and asserts it failed, optionally with the given error code and kind.
        /// </summary>
        /// <param name="domain">Domain instance.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="code">Expected error code of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="kind">Expected error kind of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The matched error.</returns>
        /// <exception cref="DomainAssertException">The command succeeded, or no error matched.</exception>
        public static async Task<IError> AssertSendErrorAsync(this IDomain domain, ICommand command, string? code = null, ErrorKind? kind = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.SendAsync(command, cancellationToken)).AssertError(code, kind);
        }

        /// <summary>
        /// Dispatches the command with result data and asserts it failed, optionally with the
        /// given error code and kind.
        /// </summary>
        /// <typeparam name="TResult">Type of the result data the command would produce.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="code">Expected error code of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="kind">Expected error kind of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The matched error.</returns>
        /// <exception cref="DomainAssertException">The command succeeded, or no error matched.</exception>
        public static async Task<IError> AssertSendErrorAsync<TResult>(this IDomain domain, ICommand<TResult> command, string? code = null, ErrorKind? kind = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.SendAsync(command, cancellationToken)).AssertError(code, kind);
        }

        /// <summary>
        /// Dispatches the item command and asserts it failed, optionally with the given error code and kind.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="code">Expected error code of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="kind">Expected error kind of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The matched error.</returns>
        /// <exception cref="DomainAssertException">The command succeeded, or no error matched.</exception>
        public static async Task<IError> AssertSendItemErrorAsync<TId, TItem>(this IDomain domain, IItem<TId> item, IItemCommand<TItem> command, string? code = null, ErrorKind? kind = null, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.SendItemAsync(item, command, cancellationToken)).AssertError(code, kind);
        }

        /// <summary>
        /// Dispatches the list query, asserts it succeeded, optionally checks the rows and returns them.
        /// </summary>
        /// <typeparam name="TRow">Type of a single returned row.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="query">Query to execute.</param>
        /// <param name="check">Optional additional check over the rows.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The returned rows.</returns>
        /// <exception cref="DomainAssertException">The query failed.</exception>
        public static async Task<IList<TRow>> AssertQueryAsync<TRow>(this IDomain domain, IQuery<TRow> query, Action<IList<TRow>>? check = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.QueryAsync(query, cancellationToken)).AssertSuccess(check);
        }

        /// <summary>
        /// Dispatches the single-value query, asserts it succeeded, optionally checks the value and returns it.
        /// </summary>
        /// <typeparam name="TModel">Type of the produced value.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="query">Query to execute.</param>
        /// <param name="check">Optional additional check over the value.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The produced value.</returns>
        /// <exception cref="DomainAssertException">The query failed.</exception>
        public static async Task<TModel> AssertQueryAsync<TModel>(this IDomain domain, ISingleQuery<TModel> query, Action<TModel>? check = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domain);

            return (await domain.QueryAsync(query, cancellationToken)).AssertSuccess(check);
        }

        /// <summary>
        /// Finds the item by id, asserts it exists, optionally checks it and returns it.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="itemId">Identifier of the item.</param>
        /// <param name="check">Optional additional check over the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The found item.</returns>
        /// <exception cref="DomainAssertException">The item was not found.</exception>
        public static async Task<TItem> AssertItemAsync<TId, TItem>(this IDomain domain, TId itemId, Action<TItem>? check = null, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);

            var item = await domain.FindItemAsync<TId, TItem>(itemId, cancellationToken)
                ?? throw DomainAssert.Failure($"Expected item {typeof(TItem).Name} \"{itemId}\" to exist, but it was not found.");

            check?.Invoke(item);

            return item;
        }

        /// <summary>
        /// Asserts no item with the given id exists.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="itemId">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="DomainAssertException">The item exists.</exception>
        public static async Task AssertNoItemAsync<TId, TItem>(this IDomain domain, TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(domain);

            var item = await domain.FindItemAsync<TId, TItem>(itemId, cancellationToken);
            if (item != null)
                throw DomainAssert.Failure($"Expected item {typeof(TItem).Name} \"{itemId}\" to be absent, but it exists.");
        }
    }
}
