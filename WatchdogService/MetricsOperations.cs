//-----------------------------------------------------------------------
// <copyright file="MetricsOperations.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.WatchdogService.Interfaces;
using Microsoft.ServiceFabric.WatchdogService.Models;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceFabric.WatchdogService
{
    /// <summary>
    /// Watchdog metric service operations.
    /// This is intended to separate the service logic from the controller and stateful service code.
    /// </summary>
    internal sealed class MetricsOperations : IDisposable
    {
        #region Private Members

        /// <summary>
        /// WatchdogService instance.
        /// </summary>
        internal readonly IWatchdogService _service = null;

        /// <summary>
        /// Service Fabric client.
        /// </summary>
        internal FabricClient Client => _service.Client;

        /// <summary>
        /// IWatchdogTelemetry instance.
        /// </summary>
        internal readonly IWatchdogTelemetry _telemetry = null;

        /// <summary>
        /// Gets the timeout interval for individual fabric operations.
        /// </summary>
        internal readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// CancellationToken instance.
        /// </summary>
        internal CancellationToken _token = CancellationToken.None;

        /// <summary>
        /// Gets the metric callback timer.
        /// </summary>
        internal Timer _metricTimer = null;

        /// <summary>
        /// Timer interval.
        /// </summary>
        internal TimeSpan _metricTimerInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Cached count of metrics being observed.
        /// </summary>
        internal int _metricCount = 0;

        /// <summary>
        /// Health state of the operations.
        /// </summary>
        internal HealthState _healthState = HealthState.Ok;

        #endregion

        #region Public Properties

        /// <summary>
        /// Number of metrics being tracked by the watchdog service.
        /// </summary>
        public int MetricCount => Volatile.Read(ref _metricCount);

        /// <summary>
        /// Gets the health state of the operations.
        /// </summary>
        public HealthState Health
        {
            get
            {
                ServiceEventSource.Current.Trace("HealthState", Enum.GetName(typeof(HealthState), _healthState));
                return _healthState;
            }
        }

        /// <summary>
        /// Sets the metric interval.
        /// </summary>
        public TimeSpan TimerInterval
        {
            set { _metricTimer.Change(value, value.Add(TimeSpan.FromSeconds(30))); }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// MetricOperations constructor.
        /// </summary>
        /// <param name="svc">Reference to IWatchdogService service instance.</param>
        /// <param name="telemetry">Reference to the IWatchdogTelemetry instance.</param>
        /// <param name="token">CancellationToken instance.</param>
        /// <param name="timeout">Default fabric operation timeout value.</param>
        public MetricsOperations(IWatchdogService svc, IWatchdogTelemetry telemetry, TimeSpan interval, CancellationToken token, TimeSpan timeout = default(TimeSpan))
        {
            if (null == svc) throw new ArgumentNullException("Argument is null.", nameof(svc));
            if (null == telemetry) throw new ArgumentNullException("Argument is null.", nameof(telemetry));

            ServiceEventSource.Current.ServiceMessage(svc.Context, "MetricsOperations.Constructor");

            _token = token;
            _service = svc;
            _timeout = (default(TimeSpan) == timeout) ? TimeSpan.FromSeconds(5) : timeout;
            _telemetry = telemetry;

            // Create a timer that calls the local method every 30 seconds start 1 minute from now.
            _metricTimer = new Timer(async (o) =>
            {
                try
                {
                    await EnumerateMetricsAsync();
                }
                catch(Exception ex)
                {
                    _healthState = HealthState.Error;
                    ServiceEventSource.Current.ServiceMessage(_service.Context, "Exception {0} in {1}", ex.Message, "MetricTimer method.");
                }
            }, _token, interval, interval.Add(TimeSpan.FromSeconds(30)));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the MetricCheck dictionary. If it doesn't already exist it is created.
        /// </summary>
        private async Task<IReliableDictionary<string, MetricCheck>> GetMetricCheckDictionaryAsync()
        {
            return await _service.StateManager.GetOrAddAsync<IReliableDictionary<string, MetricCheck>>("metricCheckDictionary").ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the complete list of partitions for a service. 
        /// </summary>
        /// <param name="serviceUri">Uri of the service.</param>
        /// <param name="partitionFilter">Optional partition filter. Default is not filter is applied.</param>
        /// <returns>List of Partition instances.</returns>
        private async Task<IList<Partition>> GetCompletePartitionListAsync(Uri serviceUri, Guid? partitionFilter = null)
        {
            if (null == serviceUri)
                throw new ArgumentNullException("Argument is null", nameof(serviceUri));

            string ct = null;
            int retryCount = 5;
            List<Partition> items = new List<Partition>();

            do
            {
                try
                {
                    // Get the list of partitions and add them to the list of returned partitions.
                    var pList = await Client.QueryManager.GetPartitionListAsync(serviceUri, partitionFilter, ct, _timeout, _token).ConfigureAwait(false);
                    if (null != pList)
                    {
                        ct = pList.ContinuationToken;
                        items.AddRange(pList);
                    }
                    else
                        retryCount--;
                }
                catch (TimeoutException) { retryCount--; }
                catch (FabricTransientException) { retryCount--; }
            } while (null != ct & retryCount > 0);

            return items;
        }

        /// <summary>
        /// Get the complete list of replicas for a partition.
        /// </summary>
        /// <param name="partition">Guid containing the partition identifier.</param>
        /// <returns>List of Replica instances.</returns>
        private async Task<IList<Replica>> GetCompleteReplicaListAsync(Guid partition)
        {
            string ct = null;
            int retryCount = 5;
            List<Replica> items = new List<Replica>();

            do
            {
                try
                { 
                // Get the list of replicas and add them to the item list.
                var rList = await Client.QueryManager.GetReplicaListAsync(partition, ct, _timeout, _token).ConfigureAwait(false);
                if (null != rList)
                {
                    ct = rList.ContinuationToken;
                    items.AddRange(rList);
                }
                else
                    retryCount--;
                }
                catch (TimeoutException) { retryCount--; }
                catch (FabricTransientException) { retryCount--; }
            } while (null != ct && retryCount > 0);

            return items;
        }

        /// <summary>
        /// Enumerates metric requests.
        /// </summary>
        internal async Task EnumerateMetricsAsync()
        {
            // Check if the partition is readable/writable.
            if (PartitionAccessStatus.Granted != _service.ReadStatus || PartitionAccessStatus.Granted != _service.WriteStatus)
                return;

            // Get the health check schedule items.
            var mDict = await GetMetricCheckDictionaryAsync();

            // Create a transaction for the enumeration.
            using (ITransaction tx = _service.StateManager.CreateTransaction())
            {
                bool result = false;

                // Create the AsyncEnumerator.
                var ae = (await mDict.CreateEnumerableAsync(tx, EnumerationMode.Ordered)).GetAsyncEnumerator();
                while (await ae.MoveNextAsync(_token))
                {
                    MetricCheck mc = ae.Current.Value;

                    if ((false == string.IsNullOrWhiteSpace(mc.Service)) && (default(Guid) != mc.Partition))
                    {
                        result = await ReportPartitionMetric(mc).ConfigureAwait(false);
                    }
                    else if ((false == string.IsNullOrWhiteSpace(mc.Service)) && (default(Guid) == mc.Partition))
                    {
                        result = await ReportServiceMetric(mc).ConfigureAwait(false);
                    }
                    else
                    {
                        result = await ReportApplicationMetric(mc).ConfigureAwait(false);
                    }
                }
            }

            _healthState = HealthState.Ok;
        }

        /// <summary>
        /// Reports metrics specificed for an application
        /// </summary>
        /// <param name="metric"></param>
        /// <returns>Indicator of success.</returns>
        internal async Task<bool> ReportApplicationMetric(MetricCheck metric)
        {
            ServiceEventSource.Current.ServiceRequestStart(nameof(ReportServiceMetric));

            try
            {
                var loadInfo = await Client.QueryManager.GetApplicationLoadInformationAsync(metric.Application, _timeout, _token);
                foreach (var item in loadInfo.ApplicationLoadMetricInformation)
                {
                    if (metric.MetricNames.Contains(item.Name))
                    {
                        await _telemetry.ReportMetricAsync(metric.Service, metric.Partition, item.Name, item.ApplicationLoad, _token).ConfigureAwait(false);
                    }
                }
            }
            catch (FabricElementNotFoundException) { return false; }
            catch (TimeoutException ex) { ServiceEventSource.Current.ServiceMessage(_service.Context, "Timeout in ReportServiceMetric at {0}", ex.StackTrace); }
            catch (FabricObjectClosedException) { _service.RefreshFabricClient(); ServiceEventSource.Current.ServiceMessage(_service.Context, "FabricClient closed"); }

            ServiceEventSource.Current.ServiceRequestStop(nameof(ReportServiceMetric));
            return true;
        }

        /// <summary>
        /// Reports metrics specificed for an application
        /// </summary>
        /// <param name="metric"></param>
        /// <returns>Indicator of success.</returns>
        internal async Task<bool> ReportServiceMetric(MetricCheck metric)
        {
            ServiceEventSource.Current.ServiceRequestStart(nameof(ReportServiceMetric));
            
            try
            {
                Uri serviceUri = new Uri($"fabric:/{metric.Application}/{metric.Service}");
                var pList = await GetCompletePartitionListAsync(serviceUri).ConfigureAwait(false);
                foreach (var p in pList)
                {
                    // Check that the partition is ready.
                    if (p.PartitionStatus == ServicePartitionStatus.Ready)
                    {
                        // Get the list of replicas within the partition.
                        var rList = await GetCompleteReplicaListAsync(p.PartitionInformation.Id);
                        foreach (Replica r in rList)
                        {
                            // Only query for load information if the replica is ready.
                            if (r.ReplicaStatus == ServiceReplicaStatus.Ready)
                            {
                                // Get the load information from the replica.
                                var loadInfo = await Client.QueryManager.GetReplicaLoadInformationAsync(p.PartitionInformation.Id, r.Id).ConfigureAwait(false);
                                foreach (var item in loadInfo.LoadMetricReports)
                                {
                                    // If it contains one of the names requested, report the load.
                                    if (metric.MetricNames.Contains(item.Name))
                                    {
                                        await _telemetry.ReportMetricAsync(metric.Service, r.Id, item.Name, item.Value, _token).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (FabricElementNotFoundException) { return false; }
            catch (TimeoutException ex) { ServiceEventSource.Current.ServiceMessage(_service.Context, "Timeout in ReportServiceMetric at {0}", ex.StackTrace); }
            catch (FabricObjectClosedException) { _service.RefreshFabricClient(); ServiceEventSource.Current.ServiceMessage(_service.Context, "FabricClient closed"); }

            ServiceEventSource.Current.ServiceRequestStop(nameof(ReportServiceMetric));
            return true;
        }

        /// <summary>
        /// Reports metrics specificed for an application
        /// </summary>
        /// <param name="metric"></param>
        /// <returns>Indicator of success.</returns>
        internal async Task<bool> ReportPartitionMetric(MetricCheck metric)
        {
            ServiceEventSource.Current.ServiceRequestStart(nameof(ReportServiceMetric));

            try
            { 
                var loadInfo = await Client.QueryManager.GetPartitionLoadInformationAsync(metric.Partition, _timeout, _token);
                foreach (var item in loadInfo.PrimaryLoadMetricReports)
                {
                    await _telemetry.ReportMetricAsync(metric.Service, metric.Partition, item.Name, item.Value, _token).ConfigureAwait(false);
                }
            }
            catch (FabricElementNotFoundException) { return false; }
            catch (TimeoutException ex) { ServiceEventSource.Current.ServiceMessage(_service.Context, "Timeout in ReportServiceMetric at {0}", ex.StackTrace); }
            catch (FabricObjectClosedException) { _service.RefreshFabricClient(); ServiceEventSource.Current.ServiceMessage(_service.Context, "FabricClient closed"); }

            ServiceEventSource.Current.ServiceRequestStop(nameof(ReportServiceMetric));
            return true;
        }

        #endregion

        #region Public Operation Methods       

        /// <summary>
        /// Called to add a metric to the watchdog.
        /// </summary>
        /// <param name="mcm">MetricCheck metric.</param>
        /// <returns>Task instance.</returns>
        /// <exception cref="ArgumentException">ServiceName parameter within the HealthCheck instance does not exist.</exception>
        public async Task<bool> AddMetricAsync(MetricCheck mcm)
        {
            // Get the required dictionaries.
            var hcDict = await GetMetricCheckDictionaryAsync().ConfigureAwait(false);

            try
            {
                // Create a transaction.
                using (ITransaction tx = _service.StateManager.CreateTransaction())
                {
                    // Add or update the HealthCheck item in the dictionary.
                    await hcDict.AddOrUpdateAsync(tx, mcm.Key, mcm, (k, v) => { return mcm; }, _timeout, _token).ConfigureAwait(false);
                    await tx.CommitAsync();
                    Interlocked.Increment(ref _metricCount);
                }
            }
            catch (FabricObjectClosedException) { _service.RefreshFabricClient(); return true; }
            catch (TimeoutException ex) { ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(AddMetricAsync)); }
            catch (FabricTransientException ex) { ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(AddMetricAsync)); }

            return false;
        }

        /// <summary>
        /// Gets a filtered set of MetricCheck items.
        /// </summary>
        /// <param name="application">Optional application filter.</param>
        /// <param name="service">Optional service filter.</param>
        /// <param name="partition">Optional partition filter.</param>
        /// <returns>List containing the set of matching HealthCheck items.</returns>
        /// <remarks>If application is null or empty, the values passed in service and partition will be ignored.
        /// If service is null or empty, the values passed in partition will be ignored.
        /// </remarks>
        public async Task<IList<MetricCheck>> GetMetricsAsync(string application = null, string service = null, Guid? partition = null)
        {
            string filter = string.Empty;
            List<MetricCheck> items = new List<MetricCheck>();
            
            // Get the required dictionaries.
            var hcDict = await GetMetricCheckDictionaryAsync().ConfigureAwait(false);

            if ((false == string.IsNullOrWhiteSpace(application)) && (false == string.IsNullOrWhiteSpace(service)) && (partition.HasValue))
            {
                filter = $"fabric:/{application}/{service}/{partition}";
            }
            else if ((false == string.IsNullOrWhiteSpace(application)) && (false == string.IsNullOrWhiteSpace(service)))
            {
                filter = $"fabric:/{application}/{service}";
            }
            else if (false == string.IsNullOrWhiteSpace(application))
            {
                filter = $"fabric:/{application}";
            }

            try
            {
                using (ITransaction tx = _service.StateManager.CreateTransaction())
                {
                    // Query the dictionary for an order list filtered by however much the user specified.
                    IAsyncEnumerable<KeyValuePair<string, MetricCheck>> list = null;
                    if (string.IsNullOrWhiteSpace(filter))
                    {
                        list = await hcDict.CreateEnumerableAsync(tx, EnumerationMode.Ordered).ConfigureAwait(false);
                    }
                    else
                    {
                        list = await hcDict.CreateEnumerableAsync(tx, (s) => { return s.StartsWith(filter); }, EnumerationMode.Ordered).ConfigureAwait(false);
                    }

                    var asyncEnumerator = list.GetAsyncEnumerator();
                    while (await asyncEnumerator.MoveNextAsync(_token).ConfigureAwait(false))
                    {
                        if (_token.IsCancellationRequested)
                            break;

                        items.Add(asyncEnumerator.Current.Value);
                    }
                }

                // Update the metric count
                Interlocked.CompareExchange(ref _metricCount, items.Count, _metricCount);
            }
            catch (FabricObjectClosedException) { _service.RefreshFabricClient(); }
            catch (TimeoutException ex) { ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(GetMetricsAsync)); }
            catch (FabricTransientException ex) { ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(GetMetricsAsync)); }

            // Return the list of HealthCheck items.
            return items;
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _metricTimer.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}
