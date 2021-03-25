﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Fabric;
    using System.Fabric.Description;
    using System.Fabric.Health;
    using System.Fabric.Query;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.ServiceFabric.Services.Client;
    using Microsoft.ServiceFabric.WatchdogService.Interfaces;
    using Microsoft.ServiceFabric.WatchdogService.Models;

    /// <summary>
    /// Watchdog health check service operations.
    /// This is intended to separate the service logic from the controller and stateful service code.
    /// </summary>
    internal sealed class HealthCheckOperations : IDisposable
    {
        #region Constructors

        /// <summary>
        /// HealthCheckOperations constructor.
        /// </summary>
        /// <param name="svc">Reference to WatchdogService stateless service instance.</param>
        /// <param name="telemetry">Reference to the WatchdogService ReportMetrics instance.</param>
        /// <param name="interval">TimeSpan of the reporting interval.</param>
        /// <param name="token">CancellationToken instance.</param>
        /// <param name="timeout">Default fabric operation timeout value.</param>
        public HealthCheckOperations(
            IWatchdogService svc, IWatchdogTelemetry telemetry, TimeSpan interval, CancellationToken token, TimeSpan timeout = default(TimeSpan))
        {
            if (null == svc)
            {
                throw new ArgumentNullException("Argument 'svc' is null.");
            }
            if (null == telemetry)
            {
                throw new ArgumentNullException("Argument 'telemetry' is null.");
            }

            ServiceEventSource.Current.ServiceMessage(svc.Context, "HealthCheckOperations.Constructor");

            this._token = token;
            this._service = svc;
            this._timeout = (default(TimeSpan) == timeout) ? TimeSpan.FromSeconds(5) : timeout;
            this._telemetry = telemetry;
            this._http = new HttpClient();

            // Create a timer that calls the local method every 30 seconds starting 1 minute from now.
            this._healthCheckTimer = new Timer(
                async (o) =>
                {
                    try
                    {
                        await this.EnumerateHealthChecksAsync();
                    }
                    catch (Exception ex)
                    {
                        this._healthState = HealthState.Error;
                        ServiceEventSource.Current.ServiceMessage(this._service.Context, "Exception {0} in {1}", ex.Message, "HealthCheckTimer method");
                    }
                },
                this._token,
                interval,
                interval.Add(TimeSpan.FromSeconds(30)));
        }

        #endregion

        #region Private Members

        /// <summary>
        /// WatchdogService instance.
        /// </summary>
        internal readonly IWatchdogService _service = null;

        /// <summary>
        /// Service Fabric client.
        /// </summary>
        internal FabricClient Client => this._service.Client;

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
        /// Cached count of health check instances.
        /// </summary>
        internal int _healthCheckCount = 0;

        /// <summary>
        /// Timer used to call for health checks.
        /// </summary>
        internal Timer _healthCheckTimer = null;

        /// <summary>
        /// Timer interval.
        /// </summary>
        internal TimeSpan _healthCheckTimerInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Health state of the operations.
        /// </summary>
        internal HealthState _healthState = HealthState.Ok;

        /// <summary>
        /// HttpClient instance to make outbound calls.
        /// </summary>
        internal HttpClient _http = null;

        #endregion

        #region Public Properties

        /// <summary>
        /// Number of health checks registered with the watchdog.
        /// </summary>
        public int HealthCheckCount => Volatile.Read(ref this._healthCheckCount);

        /// <summary>
        /// Gets the health state of the cleanup operation.
        /// </summary>
        public HealthState Health
        {
            get
            {
                ServiceEventSource.Current.Trace("HealthState", Enum.GetName(typeof(HealthState), this._healthState));
                return this._healthState;
            }
        }

        /// <summary>
        /// Sets the health check interval.
        /// </summary>
        public TimeSpan TimerInterval
        {
            set { this._healthCheckTimer.Change(value, value.Add(TimeSpan.FromSeconds(30))); }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the HealthCheck dictionary. If it doesn't already  xist it is created.
        /// </summary>
        private async Task<IReliableDictionary<string, HealthCheck>> GetHealthCheckDictionaryAsync()
        {
            return await this._service.StateManager.GetOrAddAsync<IReliableDictionary<string, HealthCheck>>("healthCheckDictionary").ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the HealthCheckSchedule dictionary. If it doesn't already exist it is created.
        /// </summary>
        private async Task<IReliableDictionary<long, WatchdogScheduledItem>> GetHealthCheckScheduleDictionaryAsync()
        {
            return
                await
                    this._service.StateManager.GetOrAddAsync<IReliableDictionary<long, WatchdogScheduledItem>>("healthCheckScheduleDictionary")
                        .ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that a service name and optionally, a partition, exists within the cluster.
        /// </summary>
        /// <param name="serviceName">Uri containing the name of the service.</param>
        /// <param name="partition">Service partition. Default to Guid.Empty.</param>
        /// <returns>True if the service name and partition exist, otherwise false.</returns>
        internal async Task<bool> ValidateServiceExistsAsync(Uri serviceName, Guid partition = default(Guid))
        {
            try
            {
                // Check that the service name exists by retrieving the ServiceDescription.
                ServiceDescription sd =
                    await this.Client.ServiceManager.GetServiceDescriptionAsync(serviceName, this._timeout, this._token).ConfigureAwait(false);
                if (null != sd)
                {
                    // If a partition was not specified, return true.
                    if (Guid.Empty == partition)
                    {
                        return true;
                    }

                    // A partition was specified, attempt to look up the definition. If at least one row is returned, return success.
                    ServicePartitionList list = await this.Client.QueryManager.GetPartitionAsync(partition, this._timeout, this._token).ConfigureAwait(false);
                    if (list.Count > 0)
                    {
                        return true;
                    }
                }
            }
            catch (FabricObjectClosedException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ValidateServiceExistsAsync));
            }
            catch (TimeoutException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ValidateServiceExistsAsync));
            }
            catch (FabricTransientException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ValidateServiceExistsAsync));
            }
            catch (FabricException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ValidateServiceExistsAsync));
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ValidateServiceExistsAsync));
            }

            return false;
        }

        /// <summary>
        /// Saves a HealthCheckScheduleItem to the dictionary.
        /// </summary>
        /// <param name="tx">ITransaction instance.</param>
        /// <param name="item">HealthCheckScheuleItem instance.</param>
        /// <returns>True if successful, otherwise false.</returns>
        /// <remarks>Separate method to address possible key collisions.</remarks>
        internal async Task<bool> SaveAsync(ITransaction tx, WatchdogScheduledItem item)
        {
            int retryCount = 5;

            // Get the schedule dictionary.
            IReliableDictionary<long, WatchdogScheduledItem> schedDict = await this.GetHealthCheckScheduleDictionaryAsync().ConfigureAwait(false);

            // Get a copy of the desired execution time.
            long key = item.ExecutionTicks;

            while (retryCount-- >= 0)
            {
                // Attempt to add the item. There may be a collision because the key already exists. 
                // If it doesn't succeed try again with a new key.
                if (await schedDict.TryAddAsync(tx, key, item, this._timeout, this._token).ConfigureAwait(false))
                {
                    return true;
                }

                // Increment the key value.
                key++;
            }

            // Exhausted the number of allowed retries.
            return false;
        }

        /// <summary>
        /// Enumerates scheduled health checks.
        /// </summary>
        internal async Task EnumerateHealthChecksAsync()
        {
            // Check if the partition is readable/writable.
            if (PartitionAccessStatus.Granted != this._service.ReadStatus || PartitionAccessStatus.Granted != this._service.WriteStatus)
            {
                return;
            }

            // Get the health check schedule items.
            IReliableDictionary<long, WatchdogScheduledItem> scheduleDict = await this.GetHealthCheckScheduleDictionaryAsync();

            // Create a transaction for the enumeration.
            using (ITransaction eTx = this._service.StateManager.CreateTransaction())
            {
                // Create the AsyncEnumerator.
                Data.IAsyncEnumerator<KeyValuePair<long, WatchdogScheduledItem>> ae =
                    (await scheduleDict.CreateEnumerableAsync(eTx, EnumerationMode.Ordered)).GetAsyncEnumerator();
                while (await ae.MoveNextAsync(this._token))
                {
                    // Compare the times, if this item is due for execution
                    if (ae.Current.Value.ExecutionTicks < DateTimeOffset.UtcNow.UtcTicks)
                    {
                        await this.PerformItemHealthCheckAsync(ae.Current.Value);
                    }
                }

                await eTx.CommitAsync();
            }

            this._healthState = HealthState.Ok;
        }

        /// <summary>
        /// Performs a HealthCheck for a scheduled item.
        /// </summary>
        /// <param name="item">WatchdogScheduledItem instance.</param>
        internal async Task PerformItemHealthCheckAsync(WatchdogScheduledItem item)
        {
            // Get the health check dictionaries.
            IReliableDictionary<string, HealthCheck> dict = await this.GetHealthCheckDictionaryAsync();
            IReliableDictionary<long, WatchdogScheduledItem> scheduleDict = await this.GetHealthCheckScheduleDictionaryAsync();

            // Create a transaction.
            using (ITransaction tx = this._service.StateManager.CreateTransaction())
            {
                // Attempt to get the HealthCheck instance for the key. If not return.
                ConditionalValue<HealthCheck> cv = await dict.TryGetValueAsync(tx, item.Key, LockMode.Update);
                if (cv.HasValue)
                {
                    HealthCheck hc = cv.Value;

                    try
                    {
                        // Find the partition information that matches the partition identifier.
                        // If the partition isn't found, remove the health check item.
                        Partition partition = await this.FindMatchingPartitionAsync(hc.Partition);
                        if (null == partition)
                        {
                            await dict.TryRemoveAsync(tx, hc.Key, this._timeout, this._token);
                        }
                        else
                        {
                            // Execute the check and evaluate the results returned in the new HealthCheck instance.
                            hc = await this.ExecuteHealthCheckAsync(hc, partition);

                            // Update the value of the HealthCheck to store the results of the test.
                            await dict.TryUpdateAsync(tx, item.Key, hc, cv.Value);

                            // Remove the current scheduled item.
                            await scheduleDict.TryRemoveAsync(tx, item.ExecutionTicks);

                            // Add the new scheduled item.
                            WatchdogScheduledItem newItem = new WatchdogScheduledItem(hc.LastAttempt.Add(hc.Frequency), hc.Key);
                            await (scheduleDict.TryAddAsync(tx, newItem.ExecutionTicks, newItem));
                        }

                        // Commit the transaction.
                        await tx.CommitAsync();
                    }
                    catch (TimeoutException ex)
                    {
                        ServiceEventSource.Current.ServiceMessage(this._service.Context, ex.Message);
                    }
                    catch (FabricNotPrimaryException ex)
                    {
                        ServiceEventSource.Current.ServiceMessage(this._service.Context, ex.Message);
                        return;
                    }
                    catch (Exception ex)
                    {
                        ServiceEventSource.Current.ServiceMessage(this._service.Context, ex.Message);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Execute the HealthCheck request.
        /// </summary>
        /// <param name="hc">HealthCheck description.</param>
        /// <param name="partition">Partition instance.</param>
        /// <returns>HealthCheck instance.</returns>
        internal async Task<HealthCheck> ExecuteHealthCheckAsync(HealthCheck hc, Partition partition)
        {
            // Check passed parameters.
            if ((null == partition) || (default(HealthCheck) == hc))
            {
                return default(HealthCheck);
            }

            // Get the service endpoint of the service being tested.
            ResolvedServiceEndpoint rse = await this.GetServiceEndpointAsync(hc.ServiceName, partition);
            if (null == rse)
            {
                return default(HealthCheck);
            }

            // If an endpoint name was specified, search for that name within the ResolvedServiceEndpoint instance.
            string baseAddress = (string.IsNullOrWhiteSpace(hc.Endpoint)) ? rse.GetFirstEndpoint() : rse.GetEndpoint(hc.Endpoint);
            Uri uri = new Uri($"{baseAddress}/{hc.SuffixPath}");

            // Create the HttpRequest message.
            HttpRequestMessage request = this.CreateRequestMessage(hc, uri);

            try
            {
                bool success = true;
                HealthState hs = HealthState.Ok;

                // Make the request to the service being tested.
                Stopwatch sw = Stopwatch.StartNew();
                HttpResponseMessage response = await this._http.SendAsync(request, HttpCompletionOption.ResponseContentRead, this._token);
                sw.Stop();

                // Evaluate the result of the request. If specific codes were provided, check each of the code arrays to find the result code.
                if ((null != hc.WarningStatusCodes) && (hc.WarningStatusCodes.Contains((int) response.StatusCode)))
                {
                    hs = HealthState.Warning;
                    success = false;
                }
                else if ((null != hc.ErrorStatusCodes) && (hc.ErrorStatusCodes.Contains((int) response.StatusCode)))
                {
                    hs = HealthState.Error;
                    success = false;
                }
                else if (false == response.StatusCode.IsSuccessCode())
                {
                    hs = HealthState.Error;
                    success = false;
                }

                // Report health result to Service Fabric.
                this.Client.HealthManager.ReportHealth(new PartitionHealthReport(hc.Partition, new HealthInformation("Watchdog Health Check", hc.Name, hs)));

                // Report the availability of the tested service to the telemetry provider.
                await
                    this._telemetry.ReportAvailabilityAsync(
                        hc.ServiceName.AbsoluteUri,
                        hc.Partition.ToString(),
                        hc.Name,
                        hc.LastAttempt,
                        TimeSpan.FromMilliseconds(hc.Duration),
                        null,
                        success,
                        this._token);

                // Return a new HealthCheck instance containing the results of the request.
                long count = (success) ? 0 : hc.FailureCount + 1;
                return hc.UpdateWith(DateTime.UtcNow, count, sw.ElapsedMilliseconds, response.StatusCode);
            }
            catch (FabricTransientException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ExecuteHealthCheckAsync));
                return hc.UpdateWith(DateTime.UtcNow, hc.FailureCount + 1, -1, System.Net.HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.ExecuteHealthCheckAsync));
                throw;
            }
        }

        /// <summary>
        /// Creates an HttpRequestMessage based on the HealthCheck information.
        /// </summary>
        /// <param name="hc">HealthCheck instance.</param>
        /// <param name="address">Uri containing the full address.</param>
        /// <returns>HttpRequestMessage instance.</returns>
        internal HttpRequestMessage CreateRequestMessage(HealthCheck hc, Uri address)
        {
            // Create the HttpRequestMessage initialized with the values configured for the HealthCheck.
            HttpRequestMessage request = new HttpRequestMessage() {RequestUri = address, Method = hc.Method};

            // If headers are specified, add them to the request.
            if (null != hc.Headers)
            {
                foreach (KeyValuePair<string, string> header in hc.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            // If content was specified, add it to the request.
            if (null != hc.Content)
            {
                request.Content = new StringContent(hc.Content, Encoding.Default, hc.MediaType);
            }

            return request;
        }

        /// <summary>
        /// Builds a list of partitions to execute the health check against.
        /// </summary>
        internal async Task<List<Guid>> GetPartitionListAsync(HealthCheck hc)
        {
            List<Guid> partitions = new List<System.Guid>();

            // If the partition was specified, don't build a list of partitions
            // return a list containing the single partition.
            if (Guid.Empty != hc.Partition)
            {
                partitions.Add(hc.Partition);
            }
            else
            {
                // Could be expanded to get all of the partitions for a service.
                // The manner is which the results are store would have to be changed, couldn't update the HealthCheck for each partition
                await Task.Delay(0);
                throw new NotImplementedException();
            }

            return partitions;
        }

        /// <summary>
        /// Finds the matching partition and returns the partition information.
        /// </summary>
        /// <param name="partition">GUID containing the partition filter.</param>
        /// <returns>Partition instance if found, otherwise null.</returns>
        internal async Task<Partition> FindMatchingPartitionAsync(Guid partition)
        {
            // Get a list of partitions matching the filter. Should be a single one.
            ServicePartitionList spList = await this.Client.QueryManager.GetPartitionAsync(partition);

            // Enumerate the list looking for the matching partition identifier.
            foreach (Partition p in spList)
            {
                if (p.PartitionInformation.Id == partition)
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a ResolvedServiceEndpoing for the specified service and partition.
        /// </summary>
        /// <param name="service">ServiceName URI. </param>
        /// <param name="pId">GUID partition identifier. This must exist.</param>
        /// <returns>ResolvedServiceEndpoint instance if found, otherwise null.</returns>
        internal async Task<ResolvedServiceEndpoint> GetServiceEndpointAsync(Uri service, Guid pId)
        {
            Partition partition = await this.FindMatchingPartitionAsync(pId);
            if (null != partition)
            {
                return await this.GetServiceEndpointAsync(service, partition);
            }

            ServiceEventSource.Current.Error(
                nameof(GetServiceEndpointAsync),
                $"Could not find partition. Service: {service.AbsoluteUri} Partition: {partition}.");

            return null;
        }

        /// <summary>
        /// Gets a ResolvedServiceEndpoing for the specified service and partition.
        /// </summary>
        /// <param name="service">ServiceName URI. </param>
        /// <param name="partition">Partition instance of the service.</param>
        /// <returns>ResolvedServiceEndpoint instance if found, otherwise null.</returns>
        internal async Task<ResolvedServiceEndpoint> GetServiceEndpointAsync(Uri service, Partition partition)
        {
            // Check passed parameters.
            if (null == service)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (null == partition)
            {
                throw new ArgumentNullException(nameof(partition));
            }

            ServiceEventSource.Current.Trace(nameof(GetServiceEndpointAsync), $"Service: {service.AbsoluteUri} Partition: {partition}.");

            try
            {
                ServicePartitionKey key = null;

                // Get the partition key based on the partition type of the service.
                if (partition.PartitionInformation.Kind == ServicePartitionKind.Singleton)
                {
                    key = new ServicePartitionKey();
                }
                else if (partition.PartitionInformation.Kind == ServicePartitionKind.Int64Range)
                {
                    // Choose the LowKey as the partition value to look up.
                    key = new ServicePartitionKey(((Int64RangePartitionInformation) partition.PartitionInformation).LowKey);
                }
                else if (partition.PartitionInformation.Kind == ServicePartitionKind.Named)
                {
                    key = new ServicePartitionKey(((NamedPartitionInformation) partition.PartitionInformation).Name);
                }
                else
                {
                    string msg = $"Invalid PartitionKind '{Enum.GetName(typeof(ServicePartitionKind), partition.PartitionInformation.Kind)}'.";
                    ServiceEventSource.Current.Error(nameof(GetServiceEndpointAsync), msg);
                    throw new InvalidProgramException(msg);
                }

                // Resolve the partition, then enumerate the endpoints looking for the primary or stateless role.
                ResolvedServicePartition rsp = await ServicePartitionResolver.GetDefault().ResolveAsync(service, key, this._token);
                foreach (ResolvedServiceEndpoint ep in rsp.Endpoints)
                {
                    if ((ServiceEndpointRole.StatefulPrimary == ep.Role) || (ServiceEndpointRole.Stateless == ep.Role))
                    {
                        ServiceEventSource.Current.Trace($"{nameof(GetServiceEndpointAsync)} returning {ep.Address}.");
                        return ep;
                    }
                }

                ServiceEventSource.Current.Trace($"{nameof(GetServiceEndpointAsync)} no endpoint was found, returning null.");
                return null;
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(GetServiceEndpointAsync));
                throw;
            }
        }

        #endregion

        #region Public Operation Methods       

        /// <summary>
        /// Called to add a health check to the watchdog.
        /// </summary>
        /// <param name="hcm"></param>
        /// <returns>Task instance.</returns>
        /// <exception cref="ArgumentException">ServiceName parameter within the HealthCheck instance does not exist.</exception>
        public async Task<bool> AddHealthCheckAsync(HealthCheck hcm)
        {
            // Validate that the service name actually exists within the cluster. If it doesn't, throw an error.
            if (false == await this.ValidateServiceExistsAsync(hcm.ServiceName, hcm.Partition).ConfigureAwait(false))
            {
                throw new ArgumentException($"Service '{hcm.ServiceName?.AbsoluteUri}' does not exist within the cluster.", nameof(hcm.ServiceName));
            }

            // Get the required dictionaries.
            IReliableDictionary<string, HealthCheck> hcDict = await this.GetHealthCheckDictionaryAsync().ConfigureAwait(false);

            try
            {
                // Create a transaction.
                using (ITransaction tx = this._service.StateManager.CreateTransaction())
                {
                    // Add or update the HealthCheck item in the dictionary.
                    await hcDict.AddOrUpdateAsync(tx, hcm.Key, hcm, (k, v) => { return hcm; }, this._timeout, this._token).ConfigureAwait(false);
                    Interlocked.Increment(ref this._healthCheckCount);

                    // Create the HealthCheckScheduleItem instance and save it.
                    if (await this.SaveAsync(tx, new WatchdogScheduledItem(DateTimeOffset.UtcNow, hcm.Key)).ConfigureAwait(false))
                    {
                        await tx.CommitAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (FabricObjectClosedException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.AddHealthCheckAsync));
            }
            catch (TimeoutException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.AddHealthCheckAsync));
            }
            catch (FabricTransientException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.AddHealthCheckAsync));
            }
            catch (FabricException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.AddHealthCheckAsync));
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.AddHealthCheckAsync));
            }

            return false;
        }

        /// <summary>
        /// Gets a filtered set of HealthCheck items.
        /// </summary>
        /// <param name="application">Optional application filter.</param>
        /// <param name="service">Optional service filter.</param>
        /// <param name="partition">Optional partition filter.</param>
        /// <returns>List containing the set of matching HealthCheck items.</returns>
        /// <remarks>If application is null or empty, the values passed in service and partition will be ignored.
        /// If service is null or empty, the values passed in partition will be ignored.
        /// </remarks>
        public async Task<IList<HealthCheck>> GetHealthChecksAsync(string application = null, string service = null, Guid? partition = null)
        {
            string filter = string.Empty;
            List<HealthCheck> items = new List<HealthCheck>();

            // Get the required dictionaries.
            IReliableDictionary<string, HealthCheck> hcDict = await this.GetHealthCheckDictionaryAsync().ConfigureAwait(false);

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
                using (ITransaction tx = this._service.StateManager.CreateTransaction())
                {
                    // Query the dictionary for an order list filtered by however much the user specified.
                    Data.IAsyncEnumerable<KeyValuePair<string, HealthCheck>> list =
                        await hcDict.CreateEnumerableAsync(tx, (s) => { return s.StartsWith(filter); }, EnumerationMode.Ordered).ConfigureAwait(false);
                    Data.IAsyncEnumerator<KeyValuePair<string, HealthCheck>> asyncEnumerator = list.GetAsyncEnumerator();
                    while (await asyncEnumerator.MoveNextAsync(this._token).ConfigureAwait(false))
                    {
                        items.Add(asyncEnumerator.Current.Value);
                    }
                }
            }
            catch (FabricObjectClosedException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthChecksAsync));
            }
            catch (TimeoutException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthChecksAsync));
            }
            catch (FabricTransientException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthChecksAsync));
            }
            catch (FabricException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthChecksAsync));
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthChecksAsync));
            }

            // Update the cached count and return the list of HealthCheck items.
            Interlocked.Exchange(ref this._healthCheckCount, items.Count);
            return items;
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        private void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this._http.Dispose();
                    this._healthCheckTimer.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }

        #endregion
    }
}