// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AutoInstrumentation.Configurations.Otlp;
using OpenTelemetry.AutoInstrumentation.Logging;

namespace OpenTelemetry.AutoInstrumentation.Configurations;

/// <summary>
/// Metric Settings
/// </summary>
internal class MetricSettings : Settings
{
    private static readonly IOtelLogger Logger = OtelLogging.GetLogger();

    /// <summary>
    /// Gets a value indicating whether the metrics should be loaded by the profiler. Default is true.
    /// </summary>
    public bool MetricsEnabled { get; private set; }

    /// <summary>
    /// Gets the list of enabled metrics exporters.
    /// </summary>
    public IReadOnlyList<MetricsExporter> MetricExporters { get; private set; } = new List<MetricsExporter>();

    /// <summary>
    /// Gets the list of enabled meters.
    /// </summary>
    public IReadOnlyList<MetricInstrumentation> EnabledInstrumentations { get; private set; } = new List<MetricInstrumentation>();

    /// <summary>
    /// Gets the list of meters to be added to the MeterProvider at the startup.
    /// </summary>
    public IList<string> Meters { get; } = new List<string>();

    /// <summary>
    /// Gets metrics OTLP Settings.
    /// </summary>
    public OtlpSettings? OtlpSettings { get; private set; }

    protected override void OnLoad(Configuration configuration)
    {
        var consoleExporterEnabled = configuration.GetBool(ConfigurationKeys.Metrics.ConsoleExporterEnabled) ?? false;
        MetricExporters = ParseMetricExporter(configuration, consoleExporterEnabled);
        if (MetricExporters.Contains(MetricsExporter.Otlp))
        {
            OtlpSettings = new OtlpSettings(OtlpSignalType.Metrics, configuration);
        }

        var instrumentationEnabledByDefault =
            configuration.GetBool(ConfigurationKeys.Metrics.MetricsInstrumentationEnabled) ??
            configuration.GetBool(ConfigurationKeys.InstrumentationEnabled) ?? true;

        EnabledInstrumentations = configuration.ParseEnabledEnumList<MetricInstrumentation>(
            enabledByDefault: instrumentationEnabledByDefault,
            enabledConfigurationTemplate: ConfigurationKeys.Metrics.EnabledMetricsInstrumentationTemplate);

        var additionalSources = configuration.GetString(ConfigurationKeys.Metrics.AdditionalSources);
        if (additionalSources != null)
        {
            foreach (var sourceName in additionalSources.Split(Constants.ConfigurationValues.Separator))
            {
                Meters.Add(sourceName);
            }
        }

        MetricsEnabled = configuration.GetBool(ConfigurationKeys.Metrics.MetricsEnabled) ?? true;
    }

    private static IReadOnlyList<MetricsExporter> ParseMetricExporter(Configuration configuration, bool consoleExporterEnabled)
    {
        var metricsExporterEnvVar = configuration.GetString(ConfigurationKeys.Metrics.Exporter);
        var exporters = new List<MetricsExporter>();

        if (consoleExporterEnabled)
        {
            Logger.Warning($"The '{ConfigurationKeys.Metrics.ConsoleExporterEnabled}' environment variable is deprecated and " +
                "will be removed in the next minor release. " +
                "Please set the console exporter using OTEL_METRICS_EXPORTER environmental variable. " +
                "Refer to the updated documentation for details.");

            exporters.Add(MetricsExporter.Console);
        }

        if (string.IsNullOrEmpty(metricsExporterEnvVar))
        {
            exporters.Add(MetricsExporter.Otlp);
            return exporters;
        }

        var exporterNames = metricsExporterEnvVar!.Split(Constants.ConfigurationValues.Separator);

        foreach (var exporterName in exporterNames)
        {
            switch (exporterName)
            {
                case Constants.ConfigurationValues.Exporters.Otlp:
                    exporters.Add(MetricsExporter.Otlp);
                    break;
                case Constants.ConfigurationValues.Exporters.Prometheus:
                    exporters.Add(MetricsExporter.Prometheus);
                    break;
                case Constants.ConfigurationValues.Exporters.Console:
                    exporters.Add(MetricsExporter.Console);
                    break;
                case Constants.ConfigurationValues.None:
                    break;
                default:
                    if (configuration.FailFast)
                    {
                        var message = $"Metric exporter '{exporterName}' is not supported.";
                        Logger.Error(message);
                        throw new NotSupportedException(message);
                    }

                    Logger.Error($"Metric exporter '{exporterName}' is not supported.");
                    break;
            }
        }

        return exporters;
    }
}
