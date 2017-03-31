//-----------------------------------------------------------------------
// <copyright file="WatchdogService.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.WatchdogService.Interfaces;
using Microsoft.ServiceFabric.WatchdogService.Models;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Fabric.Health;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceFabric.WatchdogService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class WatchdogService : StatefulService, IWatchdogService
    {
        /// <summary>
        /// Constant values. The metrics names must match the values in the ServiceManifest.
        /// </summary>
        const string ObservedMetricCountMetricName = "ObservedMetricCount";
        const string HealthCheckCountMetricName = "HealthCheckCount";
        const string WatchdogConfigSectionName = "Watchdog";

        #region Members

        /// <summary>
        /// Service Fabric client instance.
        /// </summary>
        private static FabricClient _client = null;

        /// <summary>
        /// HealthCheckController operations class instance.
        /// </summary>
        private HealthCheckOperations _healthCheckOperations = null;

        /// <summary>
        /// MetricsController operations class instance.
        /// </summary>
        private MetricsOperations _metricsOperations = null;

        /// <summary>
        /// Cleanup operations. Not tied to a controller.
        /// </summary>
        private CleanupOperations _cleanupOperations = null;

        /// <summary>
        /// CancellationToken instance assigned in RunAsync.
        /// </summary>
        private CancellationToken _runAsyncCancellationToken = CancellationToken.None;

        /// <summary>
        /// Health report interval. Can be changed based on configuration.
        /// </summary>
        private TimeSpan HealthReportInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Configuration package instance.
        /// </summary>
        private ConfigurationSettings _settings = null;

        /// <summary>
        /// AI telemetry instance.
        /// </summary>
        private IWatchdogTelemetry _telemetry = null;

        #endregion

        #region Public Members

        /// <summary>
        /// Service Fabric client instance with user level privileges.
        /// </summary>
        public FabricClient Client => _client;

        /// <summary>
        /// Gets the read status of the partition.
        /// </summary>
        public PartitionAccessStatus ReadStatus => this.Partition.ReadStatus;

        /// <summary>
        /// Gets the write status of the partition.
        /// </summary>
        public PartitionAccessStatus WriteStatus => this.Partition.WriteStatus;

        /// <summary>
        /// HealthCheckController operations class instance.
        /// </summary>
        public HealthCheckOperations HealthCheckOperations => _healthCheckOperations;

        /// <summary>
        /// MetricsController operations class instance.
        /// </summary>
        public MetricsOperations MetricsOperations => _metricsOperations;

        /// <summary>
        /// Configuration settings.
        /// </summary>
        public ConfigurationSettings Settings => _settings;

        #endregion

        #region Constructors

        /// <summary>
        /// Static WatchdogService constructor.
        /// </summary>
        static WatchdogService()
        {
            _client = new FabricClient(FabricClientRole.User);
        }

        /// <summary>
        /// WatchdogService constructor.
        /// </summary>
        /// <param name="context">StatefulServiceContext instance.</param>
        /// <param name="stateManagerReplica">ReliableStateManagerReplica interface.</param>
        public WatchdogService(StatefulServiceContext context, InitializationCallbackAdapter adapter)
            : base(context, new ReliableStateManager(context, new ReliableStateManagerConfiguration(onInitializeStateSerializersEvent: adapter.OnInitialize)))
        {
            adapter.StateManager = this.StateManager;
        }

        #endregion

        #region Communication Listeners

        /// <summary>
        /// Optional override to create listeners (like TCP, HTTP) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            ServiceEventSource.Current.ServiceMessage(Context, "CreateServiceReplicaListeners called.");

            return new ServiceReplicaListener[]
            {
                new ServiceReplicaListener(serviceContext => new OwinCommunicationListener(this, serviceContext, "ServiceEndpoint", "api"))
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Registers health checks with the watchdog service.
        /// </summary>
        /// <param name="token">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        internal async Task RegisterHealthCheckAsync(CancellationToken token)
        {
            HttpClient client = new HttpClient();

            // Called from RunAsync, don't let an exception out so the service will start, but log the exception because the service won't work.
            try
            {
                // Use the reverse proxy to locate the service endpoint.
                string postUrl = "http://localhost:19081/Watchdog/WatchdogService/healthcheck";
                HealthCheck hc = new HealthCheck("Watchdog Health Check", Context.ServiceName, Context.PartitionId, "watchdog/health");
                var msg = await client.PostAsJsonAsync(postUrl, hc);

                // Log a success or error message based on the returned status code.
                if (HttpStatusCode.OK == msg.StatusCode)
                {
                    ServiceEventSource.Current.Trace(nameof(RegisterHealthCheckAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                }
                else
                {
                    ServiceEventSource.Current.Error(nameof(RegisterHealthCheckAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                }
            }
            catch (Exception ex) { ServiceEventSource.Current.Error($"Exception: {ex.Message} at {ex.StackTrace}."); }
        }

        /// <summary>
        /// Compares the proposed health state with the current value and returns the least healthy.
        /// </summary>
        /// <param name="current">Current health state.</param>
        /// <param name="proposed">Proposed health state.</param>
        /// <returns>Selected health state value.</returns>
        private HealthState CompareHealthState(HealthState current, HealthState proposed)
        {
            if ((HealthState.Ok == current) && ((HealthState.Warning == proposed) || (HealthState.Error == proposed)))
                return proposed;
            if ((HealthState.Warning == current) && (HealthState.Error == proposed))
                return proposed;
            if ((HealthState.Invalid == current) || (HealthState.Unknown == current))
                return proposed;

            return current;
        }

        /// <summary>
        /// Reports the service health to Service Fabric.
        /// </summary>
        private void ReportWatchdogHealth()
        {
            // Called from RunAsync, don't let an exception out so the service will start, but log the exception because the service won't work.
            try
            {
                // Collect the health information from the local service state.
                TimeSpan interval = HealthReportInterval.Add(TimeSpan.FromSeconds(30));
                StringBuilder sb = new StringBuilder();
                HealthState hs = CheckWatchdogHealth(sb);

                // Issue a health report for the watchdog service.
                HealthInformation hi = new HealthInformation(Context.ServiceName.AbsoluteUri, "WatchdogServiceHealth", hs)
                {
                    TimeToLive = interval,
                    Description = sb.ToString(),
                    RemoveWhenExpired = false,
                    SequenceNumber = HealthInformation.AutoSequenceNumber,
                };
                Partition.ReportPartitionHealth(hi);

                hi = new HealthInformation(Context.ServiceName.AbsoluteUri, "HealthCheckOperations", _healthCheckOperations.Health);
                hi.TimeToLive = interval;
                hi.RemoveWhenExpired = false;
                hi.SequenceNumber = HealthInformation.AutoSequenceNumber;
                Partition.ReportPartitionHealth(hi);

                hi = new HealthInformation(Context.ServiceName.AbsoluteUri, "MetricOperations", _metricsOperations.Health);
                hi.TimeToLive = interval;
                hi.RemoveWhenExpired = false;
                hi.SequenceNumber = HealthInformation.AutoSequenceNumber;
                Partition.ReportPartitionHealth(hi);

                hi = new HealthInformation(Context.ServiceName.AbsoluteUri, "CleanupOperations", _cleanupOperations.Health);
                hi.TimeToLive = interval;
                hi.RemoveWhenExpired = false;
                hi.SequenceNumber = HealthInformation.AutoSequenceNumber;
                Partition.ReportPartitionHealth(hi);
            }
            catch (Exception ex) { ServiceEventSource.Current.Error($"Exception: {ex.Message} at {ex.StackTrace}."); }
        }

        /// <summary>
        /// Reports the service load metrics to Service Fabric and the telemetry provider.
        /// </summary>
        /// <param name="cancellationToken"></param>
        private async Task ReportWatchdogMetricsAsync(CancellationToken token)
        {
            // Calculate the metric value.
            int omc = _metricsOperations?.MetricCount ?? -1;
            int hcc = _healthCheckOperations?.HealthCheckCount ?? -1;

            try
            {
                // Load the list of current metric values to report.
                List<LoadMetric> metrics = new List<LoadMetric>();
                metrics.Add(new LoadMetric(ObservedMetricCountMetricName, omc));
                metrics.Add(new LoadMetric(HealthCheckCountMetricName, hcc));

                // Report the metrics to Service Fabric.
                Partition.ReportLoad(metrics);

                // Report them to the telemetry provider also.
                await _telemetry.ReportMetricAsync(ObservedMetricCountMetricName, omc, token);
                await _telemetry.ReportMetricAsync(HealthCheckCountMetricName, hcc, token);
            }
            catch (Exception ex) { ServiceEventSource.Current.Error($"Exception: {ex.Message} at {ex.StackTrace}."); }
        }

        /// <summary>
        /// Reports the overall cluster health to the telemetry provider.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReportClusterHealthAsync(CancellationToken cancellationToken)
        {
            // Called from RunAsync, don't let an exception out so the service will start, but log the exception because the service won't work.
            try
            {
                ClusterHealth health = await _client.HealthManager.GetClusterHealthAsync(TimeSpan.FromSeconds(4), cancellationToken);
                if (null != health)
                {
                    // Report the aggregated cluster health.
                    await _telemetry.ReportHealthAsync(Context.ServiceName.AbsoluteUri,
                                                       Context.PartitionId.ToString(),
                                                       Context.ReplicaOrInstanceId.ToString(),
                                                       "Cluster", "Aggregated Cluster Health",
                                                       health.AggregatedHealthState,
                                                       cancellationToken);

                    // Get the state of each of the applications running within the cluster. Report anything that is unhealthy.
                    foreach (var appHealth in health.ApplicationHealthStates)
                    {
                        if (HealthState.Ok != appHealth.AggregatedHealthState)
                        {
                            await _telemetry.ReportHealthAsync(appHealth.ApplicationName.AbsoluteUri,
                                                               Context.ServiceName.AbsoluteUri,
                                                               Context.PartitionId.ToString(),
                                                               Context.ReplicaOrInstanceId.ToString(),
                                                               Context.NodeContext.NodeName,
                                                               appHealth.AggregatedHealthState,
                                                               cancellationToken);
                        }
                    }

                    // Get the state of each of the nodes running within the cluster.
                    foreach (var nodeHealth in health.NodeHealthStates)
                    {
                        if (HealthState.Ok != nodeHealth.AggregatedHealthState)
                        {
                            await _telemetry.ReportHealthAsync(Context.NodeContext.NodeName,
                                                               Context.ServiceName.AbsoluteUri,
                                                               Context.PartitionId.ToString(),
                                                               Context.NodeContext.NodeType,
                                                               Context.NodeContext.IPAddressOrFQDN,
                                                               nodeHealth.AggregatedHealthState,
                                                               cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex) { ServiceEventSource.Current.Error($"Exception: {ex.Message} at {ex.StackTrace}."); }
        }

        /// <summary>
        /// Called when a configuration package is modified.
        /// </summary>
        private void CodePackageActivationContext_ConfigurationPackageModifiedEvent(object sender, PackageModifiedEventArgs<ConfigurationPackage> e)
        {
            if ("Config" == e.NewPackage.Description.Name)
            {
                Interlocked.Exchange<ConfigurationSettings>(ref _settings, e.NewPackage.Settings);

                // Update the configured values.
                if (null != _telemetry)
                {
                    _telemetry.Key = Settings.Sections[WatchdogConfigSectionName].Parameters["AIKey"].Value;
                }

                HealthReportInterval = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "WatchdogHealthReportInterval", TimeSpan.FromSeconds(60));

                _healthCheckOperations.TimerInterval = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "HealthCheckInterval", TimeSpan.FromMinutes(5));
                _metricsOperations.TimerInterval = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "MetricInterval", TimeSpan.FromMinutes(5));
                _cleanupOperations.Endpoint = GetConfigValueAsString(WatchdogConfigSectionName, "DiagnosticEndpoint");
                _cleanupOperations.SasToken = GetConfigValueAsString(WatchdogConfigSectionName, "DiagnosticSasToken");
                _cleanupOperations.TimerInterval = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "DiagnosticInterval", TimeSpan.FromMinutes(2));
                _cleanupOperations.TimeToKeep = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "DiagnosticTimeToKeep", TimeSpan.FromDays(10));
                _cleanupOperations.TargetCount = GetConfigValueAsInteger(WatchdogConfigSectionName, "DiagnosticTargetCount", 8000);
            }
        }

        /// <summary>
        /// Gets a configuration value or the specified default value.
        /// </summary>
        /// <param name="sectionName">Name of the section containing the parameter.</param>
        /// <param name="parameterName">Name of the parameter containing the value.</param>
        /// <param name="value">Default value.</param>
        /// <returns>Configuraiton value or default.</returns>
        private string GetConfigValueAsString(string sectionName, string parameterName, string value = null)
        {
            if (null != Settings)
            {
                ConfigurationSection section = Settings.Sections[sectionName];
                if (null != section)
                {
                    ConfigurationProperty parameter = section.Parameters[parameterName];
                    if (null != parameter)
                    {
                        value = parameter.Value;
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Gets a configuration value or the specified default value.
        /// </summary>
        /// <param name="sectionName">Name of the section containing the parameter.</param>
        /// <param name="parameterName">Name of the parameter containing the value.</param>
        /// <param name="value">Default value.</param>
        /// <returns>Configuraiton value or default.</returns>
        private int GetConfigValueAsInteger(string sectionName, string parameterName, int value = 0)
        {
            if (null != Settings)
            {
                ConfigurationSection section = Settings.Sections[sectionName];
                if (null != section)
                {
                    ConfigurationProperty parameter = section.Parameters[parameterName];
                    if (null != parameter)
                    {
                        if (int.TryParse(parameter.Value, out int val))
                        {
                            value = val;
                        }
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Gets a configuration value or the specified default value.
        /// </summary>
        /// <param name="sectionName">Name of the section containing the parameter.</param>
        /// <param name="parameterName">Name of the parameter containing the value.</param>
        /// <param name="value">Default value.</param>
        /// <returns>Configuraiton value or default.</returns>
        private TimeSpan GetConfigValueAsTimeSpan(string sectionName, string parameterName, TimeSpan value = default(TimeSpan))
        {
            if (null != Settings)
            {
                ConfigurationSection section = Settings.Sections[sectionName];
                if (null != section)
                {
                    ConfigurationProperty parameter = section.Parameters[parameterName];
                    if (null != parameter)
                    {
                        if (TimeSpan.TryParse(parameter.Value, out TimeSpan val))
                        {
                            value = val;
                        }
                    }
                }
            }

            return value;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes the FabricClient instance.
        /// </summary>
        public void RefreshFabricClient()
        {
            FabricClient old = Interlocked.CompareExchange<FabricClient>(ref _client, new FabricClient(), _client);
            old?.Dispose();
        }

        /// <summary>
        /// Checks that the service has been initialized correctly and is health by all internal metrics.
        /// </summary>
        /// <param name="description">StringBuilder instance containing the description to return.</param>
        /// <returns>Reported health state and any errors encountered added to the StringBuilder instance.</returns>
        public HealthState CheckWatchdogHealth(StringBuilder description)
        {
            HealthState current = HealthState.Ok;
            if (null == ServiceEventSource.Current)
            {
                current = CompareHealthState(current, HealthState.Error);
                description.AppendLine("ServiceEventSource is null.");
            }

            if (null == _healthCheckOperations)
            {
                current = CompareHealthState(current, HealthState.Error);
                description.AppendLine("HealthCheckOperations is null.");
            }

            if (null == _metricsOperations)
            {
                current = CompareHealthState(current, HealthState.Error);
                description.AppendLine("MetricOperations is null.");
            }

            // Check the number of endpoints listening.
            if (0 == this.Context.CodePackageActivationContext.GetEndpoints().Count)
            {
                current = CompareHealthState(current, HealthState.Error);
                description.AppendLine("Endpoints listening is zero.");
            }

            return current;
        }

        #endregion

        #region Service Fabric Overrides

        /// <summary>
        /// Called when the service is started
        /// </summary>
        /// <param name="openMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellationToken)
        {
            // Get the configuration settings and monitor for changes.
            Context.CodePackageActivationContext.ConfigurationPackageModifiedEvent += CodePackageActivationContext_ConfigurationPackageModifiedEvent;

            var configPackage = this.Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
            if (null != configPackage)
            {
                Interlocked.Exchange(ref _settings, configPackage.Settings);
            }

            return base.OnOpenAsync(openMode, cancellationToken);
        }

        /// <summary>
        /// Services that want to implement a processing loop which runs 
        /// when it is primary and has write status, just override this method with their logic
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.ServiceMessage(Context, "RunAsync called");

            // Check if settings are null. If they are, throw.
            if (null == Settings)
            {
                throw new ArgumentNullException("Settings are null, check Config/Settings exist.");
            }
            
            // Create the operations classes.
            _telemetry = new AiTelemetry(GetConfigValueAsString(WatchdogConfigSectionName, "AIKey"));
            _healthCheckOperations = new HealthCheckOperations(this, _telemetry, GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "HealthCheckInterval", TimeSpan.FromMinutes(5)), cancellationToken);
            _metricsOperations = new MetricsOperations(this, _telemetry, GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "MetricInterval", TimeSpan.FromMinutes(5)), cancellationToken);
            _cleanupOperations = new CleanupOperations(_telemetry, TimeSpan.FromMinutes(2), cancellationToken)
            {
                Endpoint = GetConfigValueAsString(WatchdogConfigSectionName, "DiagnosticEndpoint"),
                SasToken = GetConfigValueAsString(WatchdogConfigSectionName, "DiagnosticSasToken"),
                TimeToKeep = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "DiagnosticTimeToKeep", TimeSpan.FromDays(10)),
                TimerInterval = GetConfigValueAsTimeSpan(WatchdogConfigSectionName, "DiagnosticInterval", TimeSpan.FromMinutes(2))
            };

            // Register the watchdog health check.
            await RegisterHealthCheckAsync(cancellationToken).ConfigureAwait(false);

            // Loop waiting for cancellation.
            while(false == cancellationToken.IsCancellationRequested)
            {
                // Report the health and metrics of the watchdog to Service Fabric.
                ReportWatchdogHealth();
                await ReportWatchdogMetricsAsync(cancellationToken);
                await ReportClusterHealthAsync(cancellationToken);

                // Delay up to the time for the next health report.
                await Task.Delay(HealthReportInterval, cancellationToken);
            }
        }

        /// <summary>
        /// This method is called during suspected data loss. You can override this method to restore the service in case of data loss.
        /// </summary>
        /// <param name="restoreCtx">A RestoreContext to be used to restore the service.</param>
        /// <param name="cancellationToken">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        protected override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.Error(nameof(OnDataLossAsync), $"OnDataLossAsync called for partition '{this.Partition.PartitionInfo.Id}'.");
            return base.OnDataLossAsync(restoreCtx, cancellationToken);
        }

        #endregion
    }
}
