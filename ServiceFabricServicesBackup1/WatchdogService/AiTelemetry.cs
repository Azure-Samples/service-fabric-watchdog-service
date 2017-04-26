// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Collections.Generic;
    using System.Fabric.Health;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ServiceFabric.WatchdogService.Interfaces;

    /// <summary>
    /// Abstracts the ApplicationInsights telemetry API calls allowing
    /// other telemetry providers to be plugged in.
    /// </summary>
    public class AiTelemetry : IWatchdogTelemetry
    {
        /// <summary>
        /// ApplicationInsights telemetry client.
        /// </summary>
        private TelemetryClient _client = null;

        /// <summary>
        /// AiTelemetry constructor.
        /// </summary>
        public AiTelemetry(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Argument is empty", nameof(key));
            }

            this._client = new TelemetryClient(new TelemetryConfiguration() {InstrumentationKey = key});
#if DEBUG
            // Expedites the flow of data through the pipeline.
            TelemetryConfiguration.Active.TelemetryChannel.DeveloperMode = true;
#endif
        }

        #region Public Properties

        /// <summary>
        /// Gets an indicator if the telemetry is enabled or not.
        /// </summary>
        public bool IsEnabled => this._client?.IsEnabled() ?? false;

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public string Key
        {
            get { return this._client?.InstrumentationKey; }
            set { this._client.InstrumentationKey = value; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calls AI to track the availability.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <param name="instance">Instance identifier.</param>
        /// <param name="testName">Availability test name.</param>
        /// <param name="captured">The time when the availability was captured.</param>
        /// <param name="duration">The time taken for the availability test to run.</param>
        /// <param name="location">Name of the location the availability test was run from.</param>
        /// <param name="success">True if the availability test ran successfully.</param>
        /// <param name="message">Error message on availability test run failure.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportAvailabilityAsync(
            string serviceName,
            string instance,
            string testName,
            DateTimeOffset captured,
            TimeSpan duration,
            string location,
            bool success,
            CancellationToken cancellationToken,
            string message = null)
        {
            if (this.IsEnabled)
            {
                AvailabilityTelemetry at = new AvailabilityTelemetry(testName, captured, duration, location, success, message);
                at.Properties.Add("Service", serviceName);
                at.Properties.Add("Instance", instance);
                this._client.TrackAvailability(at);
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Calls AI to report health.
        /// </summary>
        /// <param name="applicationName">Application name.</param>
        /// <param name="serviceName">Service name.</param>
        /// <param name="instance">Instance identifier.</param>
        /// <param name="source">Name of the health source.</param>
        /// <param name="property">Name of the health property.</param>
        /// <param name="state">HealthState.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportHealthAsync(
            string applicationName,
            string serviceName,
            string instance,
            string source,
            string property,
            HealthState state,
            CancellationToken cancellationToken)
        {
            if (this.IsEnabled)
            {
                SeverityLevel sev = (HealthState.Error == state)
                    ? SeverityLevel.Error
                    : (HealthState.Warning == state) ? SeverityLevel.Warning : SeverityLevel.Information;
                TraceTelemetry tt = new TraceTelemetry($"Health report: {source}:{property} is {Enum.GetName(typeof(HealthState), state)}", sev);
                tt.Context.Cloud.RoleName = serviceName;
                tt.Context.Cloud.RoleInstance = instance;
                this._client.TrackTrace(tt);
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Calls AI to report a metric.
        /// </summary>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value of the property.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportMetricAsync(string name, long value, CancellationToken cancellationToken)
        {
            if (this.IsEnabled)
            {
                this._client.TrackMetric(name, value);
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Calls AI to report a metric.
        /// </summary>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value of the property.</param>
        /// <param name="properties">IDictionary&lt;string&gt;,&lt;string&gt; containing name/value pairs of additional properties.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportMetricAsync(string name, long value, IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            if (this.IsEnabled)
            {
                this._client.TrackMetric(name, value, properties);
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Calls AI to report a metric.
        /// </summary>
        /// <param name="role">Name of the service.</param>
        /// <param name="partition">Guid of the partition.</param>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value if the metric.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportMetricAsync(string role, Guid partition, string name, long value, CancellationToken cancellationToken)
        {
            return this.ReportMetricAsync(role, partition.ToString(), name, value, 1, value, value, value, 0.0, null, cancellationToken);
        }

        /// <summary>
        /// Calls AI to report a metric.
        /// </summary>
        /// <param name="role">Name of the service.</param>
        /// <param name="id">Replica or Instance identifier.</param>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value if the metric.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportMetricAsync(string role, long id, string name, long value, CancellationToken cancellationToken)
        {
            return this.ReportMetricAsync(role, id.ToString(), name, value, 1, value, value, value, 0.0, null, cancellationToken);
        }

        /// <summary>
        /// Calls AI to report a metric.
        /// </summary>
        /// <param name="roleName">Name of the role. Usually the service name.</param>
        /// <param name="instance">Instance identifier.</param>
        /// <param name="name">Name of the metric.</param>
        /// <param name="value">Value if the metric.</param>
        /// <param name="count">Number of samples for this metric.</param>
        /// <param name="min">Minimum value of the samples.</param>
        /// <param name="max">Maximum value of the samples.</param>
        /// <param name="sum">Sum of all of the samples.</param>
        /// <param name="deviation">Standard deviation of the sample set.</param>
        /// <param name="properties">IDictionary&lt;string&gt;,&lt;string&gt; containing name/value pairs of additional properties.</param>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        public Task ReportMetricAsync(
            string roleName, string instance, string name, long value, int count, long min, long max, long sum, double deviation,
            IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            if (this.IsEnabled)
            {
                MetricTelemetry mt = new MetricTelemetry(name, value)
                {
                    Count = count,
                    Min = min,
                    Max = max,
                    StandardDeviation = deviation,
                };

                mt.Context.Cloud.RoleName = roleName;
                mt.Context.Cloud.RoleInstance = instance;

                // Set the properties.
                if (null != properties)
                {
                    foreach (KeyValuePair<string, string> prop in properties)
                    {
                        mt.Properties.Add(prop);
                    }
                }

                // Track the telemetry.
                this._client.TrackMetric(mt);
            }
            return Task.FromResult(0);
        }

        #endregion
    }
}