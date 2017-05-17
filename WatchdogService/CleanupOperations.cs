// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Fabric.Health;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.WatchdogService.Interfaces;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// Watchdog monitoring clean up operations.
    /// </summary>
    public sealed class CleanupOperations : IDisposable
    {
        #region Constructors

        /// <summary>
        /// CleanupOperations constructor.
        /// </summary>
        /// <param name="telemetry">Reference to the WatchdogService ReportMetrics instance.</param>
        /// <param name="interval">Timer interval.</param>
        /// <param name="token">CancellationToken instance.</param>
        /// <param name="timeout">Default fabric operation timeout value.</param>
        public CleanupOperations(IWatchdogTelemetry telemetry, TimeSpan interval, CancellationToken token, TimeSpan timeout = default(TimeSpan))
        {
            ServiceEventSource.Current.Trace("CleanupOperations called");

            this._token = token;
            this._timeout = (default(TimeSpan) == timeout) ? TimeSpan.FromSeconds(5) : timeout;
            this._telemetry = telemetry ??
            throw new ArgumentNullException("Argument is null.", nameof(telemetry));

            // Create a timer that calls the local method.
            this._cleanupTimer = new Timer(
                async (o) =>
                {
                    try
                    {
                        await this.CleanupDiagnosticTablesAsync();
                    }
                    catch (Exception ex)
                    {
                        ServiceEventSource.Current.Exception($"Last chance exception: {ex.Message}", ex.GetType().Name, ex.StackTrace);
                    }
                },
                this._token,
                interval,
                interval.Add(TimeSpan.FromSeconds(30)));
        }

        #endregion

        #region Constants

        internal const int MaximumBatchSize = 100;
        internal const string PerfcounterTableName = "WADPerformanceCountersTable";
        internal const string SystemEventsTableName = "WADServiceFabricSystemEventTable";
        internal const string ReliableServicesTableName = "WADServiceFabricReliableServiceEventTable";

        #endregion

        #region Private Members

        /// <summary>
        /// IWatchdogTelemetry instance.
        /// </summary>
        internal readonly IWatchdogTelemetry _telemetry = null;

        /// <summary>
        /// Semaphore to prevent multiple background threads running at once.
        /// </summary>
        internal readonly SemaphoreSlim _mutex = null;

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
        internal Timer _cleanupTimer = null;

        /// <summary>
        /// Clean up timer interval.
        /// </summary>
        internal TimeSpan _cleanupTimerInterval;

        /// <summary>
        /// Table endpoint for the account.
        /// </summary>
        internal string _endpoint = null;

        /// <summary>
        /// SAS access token for the storage account.
        /// </summary>
        internal string _sasToken = null;

        /// <summary>
        /// Target number of items to remove per table per run.
        /// </summary>
        internal int _targetCount = 5000;

        /// <summary>
        /// Time interval to keep monitoring events. Anything older than this interval will be removed.
        /// </summary>
        internal TimeSpan _timeToKeep = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets the current health for the clean up operations.
        /// </summary>
        internal HealthState _healthState = System.Fabric.Health.HealthState.Ok;

        /// <summary>
        /// Tables to inspect.
        /// </summary>
        internal string[] _tablesToInspect = new string[] {PerfcounterTableName, SystemEventsTableName, ReliableServicesTableName};

        #endregion

        #region Public Properties

        /// <summary>
        /// Sets the metric interval.
        /// </summary>
        public TimeSpan TimerInterval
        {
            get { return this._cleanupTimerInterval; }
            set
            {
                ServiceEventSource.Current.Trace("TimerInterval changed to ", value.ToString());
                this._cleanupTimerInterval = value;
                this._cleanupTimer.Change(value, value.Add(TimeSpan.FromSeconds(30)));
            }
        }

        /// <summary>
        /// Sets the storage endpoint.
        /// </summary>
        public string Endpoint
        {
            set
            {
                ServiceEventSource.Current.Trace("Endpoint changed.");
                this._endpoint = value;
            }
        }

        /// <summary>
        /// Sets the SAS access token for the storage account.
        /// </summary>
        public string SasToken
        {
            set
            {
                ServiceEventSource.Current.Trace("SasToken changed.");
                this._sasToken = value;
            }
        }

        /// <summary>
        /// Sets the time interval to keep monitoring events. Anything older than this interval will be removed.
        /// </summary>
        public TimeSpan TimeToKeep
        {
            set
            {
                ServiceEventSource.Current.Trace("TimeToKeep changed.", value.ToString());
                this._timeToKeep = value;
            }
        }

        /// <summary>
        /// Sets the per table target count.
        /// </summary>
        public int TargetCount
        {
            set
            {
                ServiceEventSource.Current.Trace("TargetCount changed.", value.ToString());
                this._targetCount = value;
            }
        }

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

        #endregion

        #region Private Methods

        /// <summary>
        /// Inspects the Service Fabric diagnostic tables and removes old items when found.
        /// </summary>
        /// <returns></returns>
        internal async Task CleanupDiagnosticTablesAsync()
        {
            if ((string.IsNullOrWhiteSpace(this._endpoint)) || (string.IsNullOrWhiteSpace(this._sasToken)))
            {
                ServiceEventSource.Current.Error("Storage account information not set.");
                return;
            }

            try
            {
                // Create the storage credentials and connect to the storage account.
                // The SAS token must be a Table service SAS URL with permissions to read, delete and list table entries. HTTPS must be used.
                StorageCredentials sc = new StorageCredentials(this._sasToken);
                CloudTableClient client = new CloudTableClient(new StorageUri(new Uri(this._endpoint)), sc);

                // Inspect each table for items to be removed.
                foreach (string tableName in this._tablesToInspect)
                {
                    CloudTable table = client.GetTableReference(tableName);
                    if (true == await table.ExistsAsync(this._token))
                    {
                        await this.EnumerateTableItemsAsync(table);
                    }
                }

                this._healthState = HealthState.Ok;
            }
            catch (StorageException ex)
            {
                this._healthState = HealthState.Error;
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, ex.StackTrace);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, ex.StackTrace);
            }
        }

        /// <summary>
        /// Enumerates the items in a table looking for items with a timestamp over the time to keep.
        /// </summary>
        /// <param name="table">CloudTable instance.</param>
        /// <returns>Number of items removed.</returns>
        internal async Task<int> EnumerateTableItemsAsync(CloudTable table)
        {
            if (null == table)
            {
                throw new ArgumentNullException("Argument is null", nameof(this.EnumerateTableItemsAsync));
            }

            Stopwatch sw = Stopwatch.StartNew();
            ServiceEventSource.Current.Trace("EnumerateTableItemsAsync", table.Name);

            TableBatchOperation tbo = new TableBatchOperation();

            string pKey = null;
            int deleteCount = 0;
            TableContinuationToken tct = null;
            TableQuery query =
                new TableQuery().Where(
                    TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.LessThan, DateTimeOffset.Now.Subtract(this._timeToKeep)));

            do
            {
                // Execute the query.
                TableQuerySegment<DynamicTableEntity> queryResult = await table.ExecuteQuerySegmentedAsync(query, tct).ConfigureAwait(false);
                tct = queryResult.ContinuationToken;

                foreach (DynamicTableEntity item in queryResult.Results)
                {
                    // Remember the current partition key, creating a batch with the same key for efficient deletions.
                    if ((pKey != item.PartitionKey) || (MaximumBatchSize == tbo.Count))
                    {
                        // Remove the items, pause and then start the next batch.
                        deleteCount += await this.RemoveItemsAsync(table, tbo).ConfigureAwait(false);
                        await Task.Delay(100);
                        tbo.Clear();
                    }

                    pKey = item.PartitionKey;
                    tbo.Delete(item);
                }

                // Delete any last items.
                deleteCount += await this.RemoveItemsAsync(table, tbo).ConfigureAwait(false);
            } while ((null != tct) && (deleteCount < this._targetCount));

            ServiceEventSource.Current.Trace($"EnumerateTableItemsAsync removed {deleteCount} items from {table.Name} in {sw.ElapsedMilliseconds}ms.");
            return deleteCount;
        }

        /// <summary>
        /// Removes a batch of items from the table.
        /// </summary>
        /// <param name="table">CloudTable instance.</param>
        /// <param name="tbo">TableBatchOperations instance.</param>
        internal async Task<int> RemoveItemsAsync(CloudTable table, TableBatchOperation tbo)
        {
            if (null == table)
            {
                throw new ArgumentNullException("Argument is null", nameof(table));
            }
            if (null == tbo)
            {
                throw new ArgumentNullException("Argument is null", nameof(tbo));
            }

            int count = 0;
            const int maxRetryCount = 5;
            int retry = maxRetryCount;

            do
            {
                // Reset count in case the while was retried.
                count = 0;
                try
                {
                    TableRequestOptions tro = new TableRequestOptions()
                    {
                        MaximumExecutionTime = TimeSpan.FromSeconds(60),
                        ServerTimeout = TimeSpan.FromSeconds(5),
                        RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(1), 3)
                    };

                    // Ensure that the batch isn't empty, if it is, return the count.
                    if (0 == tbo.Count)
                    {
                        break;
                    }

                    // Execute the batch operations.
                    IList<TableResult> results = results = await table.ExecuteBatchAsync(tbo, tro, null, this._token);
                    if ((null != results) && (results.Count > 0))
                    {
                        int itemCount = 0, failureCount = 0;
                        foreach (TableResult result in results)
                        {
                            itemCount++;
                            if (false == ((HttpStatusCode) result.HttpStatusCode).IsSuccessCode())
                            {
                                failureCount++;
                            }
                        }

                        ServiceEventSource.Current.Trace($"Removed {itemCount - failureCount} of {itemCount} items from {table.Name}.");
                        count = itemCount - failureCount;
                    }
                }
                catch (StorageException ex)
                {
                    // ResourceNotFound is returned when one of the batch items isn't found. Need to remove it and try again.
                    if (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode.Contains("ResourceNotFound") ?? false)
                    {
                        // Get the index of the item within the batch.
                        if (false == int.TryParse(
                            ex.RequestInformation?.ExtendedErrorInformation?.ErrorMessage.Split(':')[0],
                            out int
                        index))
                        {
                            ServiceEventSource.Current.Trace("Unknown index, setting to 0", table.Name);
                            index = 0;
                        }

                        if (index < tbo.Count)
                        {
                            ServiceEventSource.Current.Trace($"StorageException: ResourceNotFound for item {index}", table.Name);
                            await Task.Delay(500);
                            tbo.RemoveAt(index);
                            retry--;
                        }
                        else
                        {
                            ServiceEventSource.Current.Trace("Abandoning batch.", table.Name);
                            break;
                        }
                    }
                    else
                    {
                        ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, ex.StackTrace);
                        break;
                    }
                }
            } while ((retry > 0) && (retry < maxRetryCount)); // Only retry if we hit a retryable exception or run out of retries.

            return count;
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
                    this._cleanupTimer.Dispose();
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