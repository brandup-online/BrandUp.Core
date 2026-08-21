namespace BrandUp.Behaviors
{
    /// <summary>
    /// Per-async-flow command dispatch scope: marks the flow as inside a command while the
    /// dispatch runs (see <see cref="DomainBehaviorContext.IsInsideCommand"/>) and collects
    /// actions deferred until the outermost command completes successfully — after its whole
    /// pipeline, including a transaction behavior's commit. A nested scope promotes its actions
    /// to the parent; a scope inherited by a flow that outlives the dispatch (a deferred event
    /// handler, a fire-and-forget task) reads as inactive once ended.
    /// </summary>
    internal sealed class CommandDispatchScope
    {
        static readonly AsyncLocal<CommandDispatchScope?> current = new();

        // Guards `completionActions` and `ended`: parallel nested commands promote into the
        // shared parent concurrently, and a task inheriting the scope may touch it after it ended.
        readonly object gate = new();
        List<Func<ValueTask>>? completionActions;
        bool ended;

        CommandDispatchScope(CommandDispatchScope? parent)
        {
            Parent = parent;
        }

        public CommandDispatchScope? Parent { get; }

        /// <summary>
        /// The active scope of the current async flow; an ended scope reads as none.
        /// </summary>
        public static CommandDispatchScope? Current
        {
            get
            {
                var scope = current.Value;
                if (scope == null)
                    return null;

                lock (scope.gate)
                    return scope.ended ? null : scope;
            }
        }

        public static CommandDispatchScope Begin()
        {
            var scope = new CommandDispatchScope(Current);
            current.Value = scope;
            return scope;
        }

        /// <summary>
        /// Queues work to run after the outermost command completes successfully. Discarded when
        /// this or any enclosing command fails.
        /// </summary>
        public void OnCompleted(Func<ValueTask> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (gate)
            {
                if (ended)
                    throw new InvalidOperationException("The command dispatch scope has already ended.");

                (completionActions ??= []).Add(action);
            }
        }

        /// <summary>
        /// Ends the scope. On success the queued actions are promoted to the parent scope (which
        /// may still fail and discard them) or, at the outermost command, executed; on failure
        /// they are discarded. An already-ended parent (a fire-and-forget nested command finishing
        /// late) cannot accept them — they run immediately instead.
        /// </summary>
        public async ValueTask CompleteAsync(bool success)
        {
            List<Func<ValueTask>>? pending;
            lock (gate)
            {
                ended = true;
                pending = completionActions;
                completionActions = null;
            }

            // Flows continuing after the completion (deferred event handlers) see the parent
            // scope, or none — they are no longer inside this command.
            current.Value = Parent;

            if (!success || pending == null)
                return;

            if (Parent != null && Parent.TryPromote(pending))
                return;

            foreach (var action in pending)
                await action().ConfigureAwait(false);
        }

        bool TryPromote(List<Func<ValueTask>> actions)
        {
            lock (gate)
            {
                if (ended)
                    return false;

                (completionActions ??= []).AddRange(actions);
                return true;
            }
        }
    }
}
