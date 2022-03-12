namespace BrandUp.Commands
{
    public interface IItemCommand<in TItem> : ICommand
    {
    }

    public interface IItemCommand<in TItem, out TResult> : IItemCommand<TItem>
    {
    }
}