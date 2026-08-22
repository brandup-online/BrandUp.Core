using System.Diagnostics;
using System.Diagnostics.Metrics;
using BrandUp.Behaviors;
using BrandUp.Events;

namespace BrandUp
{
    /// <summary>
    /// OpenTelemetry-compatible instrumentation of domain dispatch. Subscribe a tracer to
    /// <see cref="ActivitySourceName"/> and a metric reader to <see cref="MeterName"/>; with no
    /// listeners attached the instrumentation is free.
    /// </summary>
    public static class DomainDiagnostics
    {
        /// <summary>
        /// Name of the <see cref="ActivitySource"/> emitting a span per query/command dispatch,
        /// per command transaction and per event handler execution. Cached-query dispatches also
        /// carry a <c>brandup.cache</c> tag (hit/miss/bypass) on the dispatch span.
        /// </summary>
        public const string ActivitySourceName = "BrandUp.Domain";

        /// <summary>
        /// Name of the <see cref="Meter"/> emitting dispatch and event handler duration histograms.
        /// </summary>
        public const string MeterName = "BrandUp.Domain";

        static readonly ActivitySource activitySource = new(ActivitySourceName);
        static readonly Meter meter = new(MeterName);
        static readonly Histogram<double> dispatchDuration = meter.CreateHistogram<double>("brandup.domain.dispatch.duration", unit: "ms",
            description: "Duration of domain query and command dispatches, including behaviors.");
        static readonly Histogram<double> eventHandlerDuration = meter.CreateHistogram<double>("brandup.domain.event_handler.duration", unit: "ms",
            description: "Duration of domain event handler executions.");

        internal static Activity? StartDispatch(DomainBehaviorContext context)
        {
            // Precomputed names: the no-listener path must stay allocation-free.
            var activity = activitySource.StartActivity(ActivityName(context.Kind));
            if (activity != null)
            {
                var requestType = context.Request.GetType();
                activity.DisplayName = $"{KindName(context.Kind)} {requestType.Name}";
                activity.SetTag("brandup.kind", KindName(context.Kind));
                activity.SetTag("brandup.request", requestType.FullName);
            }

            return activity;
        }

        internal static void EndDispatch(Activity? activity, DomainBehaviorContext context, long startTimestamp, Result? result = null, Exception? exception = null)
        {
            var status = DispatchStatus(result, exception);

            if (dispatchDuration.Enabled)
                dispatchDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    new KeyValuePair<string, object?>("brandup.kind", KindName(context.Kind)),
                    new KeyValuePair<string, object?>("brandup.request", context.Request.GetType().Name),
                    new KeyValuePair<string, object?>("brandup.status", status));

            if (activity != null)
            {
                activity.SetTag("brandup.status", status);
                if (status != "success")
                    activity.SetStatus(ActivityStatusCode.Error, exception?.Message);
            }
        }

        internal static Activity? StartTransaction(Type commandType)
        {
            var activity = activitySource.StartActivity("domain.transaction");
            if (activity != null)
            {
                activity.DisplayName = $"transaction {commandType.Name}";
                activity.SetTag("brandup.request", commandType.FullName);
            }

            return activity;
        }

        internal static void EndTransaction(Activity? activity, string outcome)
        {
            if (activity == null)
                return;

            activity.SetTag("brandup.outcome", outcome);
            if (outcome != "commit")
                activity.SetStatus(ActivityStatusCode.Error);
            activity.Dispose();
        }

        internal static Activity? StartEventHandler(EventMetadata eventMetadata)
        {
            var activity = activitySource.StartActivity("domain.event");
            if (activity != null)
            {
                activity.DisplayName = $"event {eventMetadata.EventType.Name} -> {eventMetadata.HandlerType.Name}";
                activity.SetTag("brandup.event", eventMetadata.EventType.FullName);
                activity.SetTag("brandup.handler", eventMetadata.HandlerType.FullName);
                activity.SetTag("brandup.deferred", eventMetadata.IsDeferred);
            }

            return activity;
        }

        internal static void EndEventHandler(Activity? activity, EventMetadata eventMetadata, long startTimestamp, Exception? exception = null)
        {
            var status = exception == null ? "success" : "exception";

            if (eventHandlerDuration.Enabled)
                eventHandlerDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    new KeyValuePair<string, object?>("brandup.event", eventMetadata.EventType.Name),
                    new KeyValuePair<string, object?>("brandup.handler", eventMetadata.HandlerType.Name),
                    new KeyValuePair<string, object?>("brandup.status", status));

            if (activity != null)
            {
                activity.SetTag("brandup.status", status);
                if (exception != null)
                    activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            }
        }

        static string DispatchStatus(Result? result, Exception? exception)
        {
            if (exception != null)
                return "exception";

            return result is { IsSuccess: true } ? "success" : "error";
        }

        static string KindName(DomainDispatchKind kind) => kind switch
        {
            DomainDispatchKind.Query => "query",
            DomainDispatchKind.SingleQuery => "single_query",
            DomainDispatchKind.Command => "command",
            DomainDispatchKind.ItemCommand => "item_command",
            _ => "unknown"
        };

        static string ActivityName(DomainDispatchKind kind) => kind switch
        {
            DomainDispatchKind.Query => "domain.query",
            DomainDispatchKind.SingleQuery => "domain.single_query",
            DomainDispatchKind.Command => "domain.command",
            DomainDispatchKind.ItemCommand => "domain.item_command",
            _ => "domain.unknown"
        };
    }
}
