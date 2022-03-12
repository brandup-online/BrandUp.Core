namespace BrandUp.Items
{
    public interface IItem<out TId>
    {
        TId Id { get; }
    }
}