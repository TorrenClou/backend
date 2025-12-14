using Datadog.Trace;

namespace TorreClou.Infrastructure.Tracing
{
    /// <summary>
    /// Static helper class for Datadog distributed tracing.
    /// Provides a clean API for creating and managing spans.
    /// </summary>
    public static class Tracing
    {
        /// <summary>
        /// Starts a new active span that will be automatically closed when disposed.
        /// </summary>
        /// <param name="operationName">Name of the operation (e.g., "job.download.execute")</param>
        /// <param name="resourceName">Optional resource name (e.g., "Job 123")</param>
        /// <returns>A TracingScope that wraps the Datadog scope</returns>
        public static TracingScope StartSpan(string operationName, string? resourceName = null)
        {
            var scope = Tracer.Instance.StartActive(operationName);
            if (resourceName != null)
            {
                scope.Span.ResourceName = resourceName;
            }
            return new TracingScope(scope);
        }

        /// <summary>
        /// Starts a new child span for the specified operation.
        /// Use this for sub-operations within a parent span.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <returns>A TracingScope that wraps the Datadog scope</returns>
        public static TracingScope StartChildSpan(string operationName)
        {
            return new TracingScope(Tracer.Instance.StartActive(operationName));
        }

        /// <summary>
        /// Gets the current active span, if any.
        /// </summary>
        public static ISpan? CurrentSpan => Tracer.Instance.ActiveScope?.Span;

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, string? value)
        {
            CurrentSpan?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, int value)
        {
            CurrentSpan?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, double value)
        {
            CurrentSpan?.SetTag(key, value);
        }

        /// <summary>
        /// Sets a tag on the current active span.
        /// </summary>
        public static void SetTag(string key, bool value)
        {
            CurrentSpan?.SetTag(key, value.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Marks the current span as an error.
        /// </summary>
        public static void SetError(Exception ex)
        {
            var span = CurrentSpan;
            if (span != null)
            {
                span.Error = true;
                span.SetException(ex);
            }
        }

        /// <summary>
        /// Marks the current span as an error with a message.
        /// </summary>
        public static void SetError(string message)
        {
            var span = CurrentSpan;
            if (span != null)
            {
                span.Error = true;
                span.SetTag("error.message", message);
            }
        }
    }

    /// <summary>
    /// Wrapper around Datadog's IScope that provides a fluent API for span operations.
    /// </summary>
    public sealed class TracingScope : IDisposable
    {
        private readonly IScope _scope;
        private bool _disposed;

        internal TracingScope(IScope scope)
        {
            _scope = scope;
        }

        /// <summary>
        /// The underlying Datadog span.
        /// </summary>
        public ISpan Span => _scope.Span;

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, string? value)
        {
            _scope.Span.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, int value)
        {
            _scope.Span.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, double value)
        {
            _scope.Span.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a tag on this span. Fluent API.
        /// </summary>
        public TracingScope WithTag(string key, bool value)
        {
            _scope.Span.SetTag(key, value.ToString().ToLowerInvariant());
            return this;
        }

        /// <summary>
        /// Sets the resource name for this span. Fluent API.
        /// </summary>
        public TracingScope WithResource(string resourceName)
        {
            _scope.Span.ResourceName = resourceName;
            return this;
        }

        /// <summary>
        /// Marks this span as an error. Fluent API.
        /// </summary>
        public TracingScope AsError()
        {
            _scope.Span.Error = true;
            return this;
        }

        /// <summary>
        /// Marks this span as an error with exception details. Fluent API.
        /// </summary>
        public TracingScope WithException(Exception ex)
        {
            _scope.Span.Error = true;
            _scope.Span.SetException(ex);
            return this;
        }

        /// <summary>
        /// Sets a status tag on this span. Common pattern for job/operation status.
        /// </summary>
        public TracingScope WithStatus(string status)
        {
            _scope.Span.SetTag("status", status);
            return this;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _scope.Dispose();
                _disposed = true;
            }
        }
    }
}
