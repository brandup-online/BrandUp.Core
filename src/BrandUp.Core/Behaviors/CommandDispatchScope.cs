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
    /// <remarks>
    /// This scope and <c>DomainEventPublisher.CommandScope</c> are two deliberate instances of
    /// the same "defer until the outermost command succeeds" primitive, kept separate on purpose:
    /// this one is process-wide (<see cref="DomainBehaviorContext.IsInsideCommand"/> must hold
    /// across container boundaries) and runs opaque completion actions, while the event scope is
    /// owner-checked per publisher and flushed with per-handler logging. When fixing an edge case
    /// here (late-finishing nested command, dead-scope fallback), check the event scope for the
    /// same case.
    /// </remarks>
    internal sealed class CommandDispatchScope
    {
        static readonly AsyncLocal<CommandDispatchScope?> current = new();

        // `lock (this)` guards `completionActions` and the ended transition: parallel nested
        // commands promote into the shared parent concurrently, and a task inheriting the scope
        // may touch it after it ended (internal type - no external code can lock it). `ended` is
        // volatile so the hot read path (Current, once per dispatch) needs no lock.
        List<Func<ValueTask>>? completionActions;
        volatile bool ended;

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
                return scope == null || scope.ended ? null : scope;
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
        /// this or any enclosing command fails. An action's exception is swallowed at execution:
        /// the command has already succeeded and committed, so post-completion work must never
        /// turn it into an error for the caller (the rule deferred event handlers follow too) -
        /// log failures inside the action.
        /// </summary>
        public void OnCompleted(Func<ValueTask> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (this)
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
            lock (this)
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
            {
                try
                {
                    await action().ConfigureAwait(false);
                }
                catch
                {
                    // The command already completed successfully: a completion action failure
                    // must not surface it as an error to the caller. Registrars log their own
                    // failures (see QueryCacheBehavior); this catch is the last-resort guarantee.
                }
            }
        }

        bool TryPromote(List<Func<ValueTask>> actions)
        {
            lock (this)
            {
                if (ended)
                    return false;

                (completionActions ??= []).AddRange(actions);
                return true;
            }
        }
    }
}
