// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric.Health;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microsoft.ServiceFabric.WatchdogService.Interfaces;

namespace Microsoft.ServiceFabric.WatchdogService
{
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
                AzureSasCredential sc = new AzureSasCredential(this._sasToken);
                TableServiceClient client = new TableServiceClient(new Uri(this._endpoint), sc);

                // Inspect each table for items to be removed.
                foreach (string tableName in this._tablesToInspect)
                {
                    await foreach (var table in client.QueryAsync(t => t.Name == tableName)) {
                        var tableClient = client.GetTableClient(table.Name);
                        await this.EnumerateTableItemsAsync(table, client);
                    }
                }

                this._healthState = HealthState.Ok;
            }
            catch (RequestFailedException ex)
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
        internal async Task<int> EnumerateTableItemsAsync(TableClient client)
        {
            if (null == table)
            {
                throw new ArgumentNullException("Argument is null", nameof(this.EnumerateTableItemsAsync));
            }

            Stopwatch sw = Stopwatch.StartNew();
            ServiceEventSource.Current.Trace("EnumerateTableItemsAsync", tableClient.Name);
            TableClient tableClient = client.GetTableClient(table.Name);

            int deleteCount = 0;
            var timeStamp = DateTimeOffset.Now.Subtract(this._timeToKeep);
            // Execute the query.
            AsyncPageable<TableEntity> queryResult = tableClient.QueryAsync<TableEntity>(e => e.Timestamp < timeStamp);

            await foreach (TableEntity item in queryResult)
            {
                await tableClient.DeleteEntityAsync(item.PartitionKey,item.RowKey);
                deleteCount++;
            }

            ServiceEventSource.Current.Trace($"EnumerateTableItemsAsync removed {deleteCount} items from {table.Name} in {sw.ElapsedMilliseconds}ms.");
            return deleteCount;
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