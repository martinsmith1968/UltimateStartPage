using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell.Interop;

namespace UltimateStartPage.Logging
{
    /// <summary>Routes <see cref="ILogger"/> output to the "Ultimate Start Page" pane of the Output window.</summary>
    internal sealed class OutputPaneLoggerProvider : ILoggerProvider
    {
        private readonly IVsOutputWindowPane _pane;
        private readonly Func<LogLevel> _minimumLevel;

        public OutputPaneLoggerProvider(IVsOutputWindowPane pane, Func<LogLevel> minimumLevel)
        {
            _pane = pane ?? throw new ArgumentNullException(nameof(pane));
            _minimumLevel = minimumLevel ?? throw new ArgumentNullException(nameof(minimumLevel));
        }

        public ILogger CreateLogger(string categoryName) => new OutputPaneLogger(_pane, categoryName, _minimumLevel);

        public void Dispose()
        {
        }

        private sealed class OutputPaneLogger : ILogger
        {
            private readonly IVsOutputWindowPane _pane;
            private readonly string _category;
            private readonly Func<LogLevel> _minimumLevel;

            public OutputPaneLogger(IVsOutputWindowPane pane, string categoryName, Func<LogLevel> minimumLevel)
            {
                _pane = pane;
                _minimumLevel = minimumLevel;

                // "UltimateStartPage.Core.Services.JsonLayoutStore" -> "JsonLayoutStore"
                var lastDot = categoryName.LastIndexOf('.');
                _category = lastDot >= 0 ? categoryName.Substring(lastDot + 1) : categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minimumLevel();

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var message = $"{DateTime.Now:HH:mm:ss.fff} [{ShortName(logLevel)}] {_category}: {formatter(state, exception)}";
                if (exception != null)
                {
                    message += Environment.NewLine + exception;
                }

                // OutputStringThreadSafe marshals to the UI thread itself, so loggers can be used from any thread.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
                _pane.OutputStringThreadSafe(message + Environment.NewLine);
#pragma warning restore VSTHRD010
            }

            private static string ShortName(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Trace: return "trce";
                    case LogLevel.Debug: return "dbug";
                    case LogLevel.Information: return "info";
                    case LogLevel.Warning: return "warn";
                    case LogLevel.Error: return "fail";
                    case LogLevel.Critical: return "crit";
                    default: return level.ToString();
                }
            }
        }
    }
}
