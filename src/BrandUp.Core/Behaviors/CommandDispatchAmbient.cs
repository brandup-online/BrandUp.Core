namespace BrandUp.Behaviors
{
    /// <summary>
    /// Tracks whether the current async flow is inside a command dispatch. AsyncLocal mutations
    /// made inside an async method are contained by that method's flow, so entering needs no
    /// explicit reset — the flag dies with the dispatch that set it.
    /// </summary>
    internal static class CommandDispatchAmbient
    {
        static readonly AsyncLocal<bool> active = new();

        public static bool IsActive => active.Value;

        public static void Enter()
        {
            active.Value = true;
        }
    }
}
