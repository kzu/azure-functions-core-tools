using Azure.Functions.Cli.Common;
using Azure.Functions.Cli.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Functions.Cli
{
    public class LoggingFilterHelper
    {
        private const string DefaultLogLevelKey = "default";
        private IConfigurationRoot _hostJsonConfig = null;

        // CI EnvironmentSettings
        // https://github.com/watson/ci-info/blob/master/index.js#L52-L59
        public const string Ci = "CI"; // Travis CI, CircleCI, Cirrus CI, Gitlab CI, Appveyor, CodeShip, dsari
        public const string Ci_Continuous_Integration = "CONTINUOUS_INTEGRATION";  // Travis CI, Cirrus CI
        public const string Ci_Build_Number = "BUILD_NUMBER";  // Travis CI, Cirrus CI
        public const string Ci_Run_Id = "RUN_ID"; // TaskCluster, dsari

        public LoggingFilterHelper(IConfigurationRoot hostJsonConfig, bool? verboseLogging)
        {
            _hostJsonConfig = hostJsonConfig;
            VerboseLogging = verboseLogging.HasValue && verboseLogging.Value;

            if (IsCiEnvironment(verboseLogging.HasValue))
            {
                VerboseLogging = true;
            }
            if (VerboseLogging)
            {
                SystemLogDefaultLogLevel = LogLevel.Information;
            }
            if (Utilities.LogLevelExists(hostJsonConfig, DefaultLogLevelKey, out LogLevel logLevel))
            {
                SystemLogDefaultLogLevel = logLevel;
                UserLogDefaultLogLevel = logLevel;
            }
        }

        /// <summary>
        /// Default level for system logs
        /// </summary>
        public LogLevel SystemLogDefaultLogLevel { get; } = LogLevel.Warning;

        /// <summary>
        /// Default level for user logs
        /// </summary>
        public LogLevel UserLogDefaultLogLevel { get; } = LogLevel.Information;

        /// <summary>
        /// Default log level set in host.json. If not present, deafaults to Information
        /// </summary>
        public LogLevel DefaultLogLevel { get; private set; } = LogLevel.Information;

        /// <summary>
        /// Is set to true if `func start` is started with `--verbose` flag. If set, SystemLogDefaultLogLevel is set to Information
        /// </summary>
        public bool VerboseLogging { get; private set; }

        internal void AddConsoleLoggingProvider(ILoggingBuilder loggingBuilder)
        {
            // Filter is needed to force all the System Logs coming from jobhost level
            loggingBuilder.AddFilter<ColoredConsoleLoggerProvider>((category, level) => Filter(category, level)).AddProvider(new ColoredConsoleLoggerProvider(this));
        }

        private bool Filter(string category, LogLevel actualLevel)
        {
            if (Utilities.IsSystemLogCategory(category))
            {
                return true;
            }
            if (_hostJsonConfig != null && Utilities.LogLevelExists(_hostJsonConfig, category, out LogLevel userLogLevel))
            {
                return actualLevel >= userLogLevel;
            }
            return actualLevel >= UserLogDefaultLogLevel;
        }

        internal bool IsEnabled(string category, LogLevel logLevel)
        {
            if (_hostJsonConfig != null && Utilities.LogLevelExists(_hostJsonConfig, category, out LogLevel userLogLevel))
            {
                // If category exists in `loglevel` section, ensure defaults do not apply.
                return Utilities.UserLoggingFilter(logLevel, userLogLevel);
            }
            if (DefaultLogLevel == LogLevel.None)
            {
                return false;
            }
            return Utilities.DefaultLoggingFilter(category, logLevel, UserLogDefaultLogLevel, SystemLogDefaultLogLevel);
        }

        internal bool IsCiEnvironment(bool verboseLoggingArgExists)
        {
            if (verboseLoggingArgExists)
            {
                return VerboseLogging;
            }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Ci)) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Ci_Continuous_Integration)) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Ci_Build_Number)) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Ci_Run_Id)))
            {
                return true;
            }
            return false;
        }
    }
}
