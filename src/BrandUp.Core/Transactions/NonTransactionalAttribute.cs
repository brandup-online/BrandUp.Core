namespace BrandUp.Transactions
{
    /// <summary>
    /// Excludes a command from the automatic transaction boundary of
    /// <see cref="TransactionBehavior"/>. Put it on commands that manage transactions themselves —
    /// e.g. long-running batch commands committing in chunks, which must not be wrapped into one
    /// outer transaction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class NonTransactionalAttribute : Attribute
    {
    }
}
