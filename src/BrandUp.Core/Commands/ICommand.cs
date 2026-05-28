namespace BrandUp.Commands
{
    /// <summary>
    /// Marker interface for a command that does not produce result data.
    /// </summary>
    public interface ICommand
    {

    }

    /// <summary>
    /// Marker interface for a command that produces result data of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">Type of the data returned by the command handler.</typeparam>
    public interface ICommand<out TResult> : ICommand
    {
    }
}
