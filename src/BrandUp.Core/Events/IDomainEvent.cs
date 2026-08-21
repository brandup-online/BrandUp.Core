namespace BrandUp.Events
{
    /// <summary>
    /// A fact that occurred in the domain. What the fact spawns — notifications, integrations,
    /// analytics — is known by its registered <see cref="IDomainEventHandler{TEvent}"/> handlers,
    /// not by the command that raised it.
    /// </summary>
    public interface IDomainEvent
    {
    }
}
