using System;
using Serilog.Core;
using Serilog.Events;

namespace XIVLauncher.Common.Support
{
    /// <summary>
    /// Serilog event sink.
    /// </summary>
    public class SerilogEventSink : ILogEventSink
    {
        private static SerilogEventSink instance;
        private readonly IFormatProvider formatProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerilogEventSink"/> class.
        /// </summary>
        /// <param name="formatProvider">Logging format provider.</param>
        private SerilogEventSink(IFormatProvider formatProvider)
        {
            this.formatProvider = formatProvider;
        }

        /// <summary>
        /// Event on a log line being emitted.
        /// </summary>
        public event EventHandler<(string Line, LogEventLevel Level, DateTimeOffset TimeStamp, Exception? Exception)>? LogLine;

        /// <summary>
        /// Gets the default instance.
        /// </summary>
        public static SerilogEventSink Instance => instance ??= new SerilogEventSink(null);

        /// <summary>
        /// Emit a log event.
        /// </summary>
        /// <param name="logEvent">Log event to be emitted.</param>
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(this.formatProvider);

            if (logEvent.Exception != null)
            {
                message += "\n" + logEvent.Exception;
            }

            this.LogLine?.Invoke(this, (message, logEvent.Level, logEvent.Timestamp, logEvent.Exception));
        }
    }
}