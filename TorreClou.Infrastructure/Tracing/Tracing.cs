using System.Diagnostics;

namespace TorreClou.Infrastructure.Tracing
{
    /// <summary>
    /// Static helper class for OpenTelemetry distributed tracing.
    /// Provides a clean API for creating and managing spans.
    /// </summary>
    public static class Tracing
    {
        private static readonly ActivitySource ActivitySource = new("TorreClou");

        /// <summary>
        /// Starts a new active span that will be automatically closed when disposed.
        /// </summary>
        /// <param name="operationName">Name of the operation (e.g., "job.download.execute")</param>
        /// <param name="resourceName">Optional resource name (e.g., "Job 123")</param>
        /// <returns>A TracingScope that wraps the OpenTelemetry Activity</returns>
        public static TracingScope StartSpan(string operationName, string? resourceName = null)
        {
            var activity = ActivitySource.StartActivity(operationName);
            if (activity == null)
            {
                // Return a no-op scope if activity creation fails
                return new TracingScope(null);
            }

            if (resourceName != null)
            {
                activity.DisplayName = resourceName;
            }

            return new TracingScope(activity);
        }

        /// <summary>
        /// Starts a new child span for the specified operation.
        /// Use this for sub-operations within a parent span.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <returns>A TracingScope that wraps the OpenTelemetry Activity</returns>
        public static TracingScope StartChildSpan(string operationName)
        {
            var activity = ActivitySource.StartActivity(operationName);
            return activity != null ? new TracingScope(activity) : new TracingScope(null);
        }

        /// <summary>
        /// Gets the current active span, if any.
        /// </summary>
        public static Activity? CurrentSpan => Activity.Current;

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, string? value)
        {
            Activity.Current?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, int value)
        {
            Activity.Current?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, double value)
        {
            Activity.Current?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, bool value)
        {
            Activity.Current?.SetTag(key, value);
        }

        /// <summary>
        /// Marks the current span as an error.
        /// </summary>
        public static void SetError(Exception ex)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetTag("error.type", ex.GetType().Name);
            }
        }

        /// <summary>
        /// Marks the current span as an error with a message.
        /// </summary>
        public static void SetError(string message)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, message);
                activity.SetTag("error", true);
                activity.SetTag("error.message", message);
            }
        }
    }

    /// <summary>
    /// Wrapper around OpenTelemetry's Activity that provides a fluent API for span operations.
    /// </summary>
    public sealed class TracingScope : IDisposable
    {
        private readonly Activity? _activity;
        private bool _disposed;

        internal TracingScope(Activity? activity)
        {
            _activity = activity;
        }

        /// <summary>
        /// The underlying OpenTelemetry Activity.
        /// </summary>
        public Activity? Span => _activity;

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, string? value)
        {
            _activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, int value)
        {
            _activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, double value)
        {
            _activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, bool value)
        {
            _activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets the resource name for this span. Fluent API.
        /// </summary>
        public TracingScope WithResource(string resourceName)
        {
            if (_activity != null)
            {
                _activity.DisplayName = resourceName;
            }
            return this;
        }

        /// <summary>
        /// Marks this span as an error. Fluent API.
        /// </summary>
        public TracingScope AsError()
        {
            if (_activity != null)
            {
                _activity.SetStatus(ActivityStatusCode.Error);
                _activity.SetTag("error", true);
            }
            return this;
        }

        /// <summary>
        /// Marks this span as an error with exception details. Fluent API.
        /// </summary>
        public TracingScope WithException(Exception ex)
        {
            if (_activity != null)
            {
                _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                _activity.SetTag("error", true);
                _activity.SetTag("error.message", ex.Message);
                _activity.SetTag("error.type", ex.GetType().Name);
            }
            return this;
        }

        /// <summary>
        /// Sets a status tag on this span. Common pattern for job/operation status.
        /// </summary>
        public TracingScope WithStatus(string status)
        {
            _activity?.SetTag("status", status);
            return this;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _activity?.Dispose();
                _disposed = true;
            }
        }
    }
}
