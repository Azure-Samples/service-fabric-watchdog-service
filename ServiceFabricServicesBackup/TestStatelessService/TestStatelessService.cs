// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace TestStatelessService
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;

    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class TestStatelessService : StatelessService
    {
        public TestStatelessService(StatelessServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(
                    serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, ServiceEventSource.Current, "ServiceEndpoint"))
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // Register the health check and metrics with the watchdog.
            bool healthRegistered = await this.RegisterHealthCheckAsync(cancellationToken);
            bool metricsRegistered = await this.RegisterMetricsAsync(cancellationToken);

            while (true)
            {
                // Report some fake metrics to Service Fabric.
                this.ReportFakeMetrics(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                // Check that registration was successful. Could also query the watchdog for additional safety.
                if (false == healthRegistered)
                {
                    healthRegistered = await this.RegisterHealthCheckAsync(cancellationToken);
                }
                if (false == metricsRegistered)
                {
                    metricsRegistered = await this.RegisterMetricsAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Registers health checks with the watchdog service.
        /// </summary>
        /// <param name="token">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        internal async Task<bool> RegisterHealthCheckAsync(CancellationToken token)
        {
            bool result = false;
            HttpClient client = new HttpClient();
            string jsonTemplate =
                "{{\"name\":\"UniqueHealthCheckName\",\"serviceName\": \"{0}\",\"partition\": \"{1}\",\"frequency\": \"{2}\",\"suffixPath\": \"api/values\",\"method\": {{ \"Method\": \"GET\" }}, \"expectedDuration\": \"00:00:00.2000000\",\"maximumDuration\": \"00:00:05\" }}";
            string json = string.Format(jsonTemplate, this.Context.ServiceName, this.Context.PartitionId, TimeSpan.FromMinutes(2));

            // Called from RunAsync, don't let an exception out so the service will start, but log the exception because the service won't work.
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:19081/Watchdog/WatchdogService/healthcheck");
                request.Content = new StringContent(json, Encoding.Default, "application/json");

                HttpResponseMessage msg = await client.SendAsync(request);

                // Log a success or error message based on the returned status code.
                if (HttpStatusCode.OK == msg.StatusCode)
                {
                    ServiceEventSource.Current.Trace(nameof(this.RegisterHealthCheckAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                    result = true;
                }
                else
                {
                    ServiceEventSource.Current.Error(nameof(this.RegisterHealthCheckAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                    ServiceEventSource.Current.Trace(nameof(this.RegisterHealthCheckAsync), json ?? "<null JSON>");
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Error($"Exception: {ex.Message} at {ex.StackTrace}.");
            }

            return result;
        }

        /// <summary>
        /// Registers metrics with the watchdog service.
        /// </summary>
        /// <param name="token">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        internal async Task<bool> RegisterMetricsAsync(CancellationToken token)
        {
            bool result = false;

            try
            {
                // The URI to register with the watchdog is important. There are two options:
                // The first URI will return the load metrics for a single partition. This is useful for a stateful partition where the primary
                // registered with the watchdog each time it starts. 
                // The second URI will report for each replica within a partition. This is useful for a stateless partition where it is desired to see 
                // the load for each replica within the partition.
                //string uri = $"http://localhost:19081/Watchdog/WatchdogService/metrics/TestStatelessApp/TestStatelessService/{Context.PartitionId}";
                string uri = $"http://localhost:19081/Watchdog/WatchdogService/metrics/TestStatelessApp/TestStatelessService";

                // Now register them with the watchdog service.
                HttpClient httpClient = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Content = new StringContent("[\"RPS\", \"Failures\", \"Latency\", \"ItemCount\"]", Encoding.Default, "application/json");

                HttpResponseMessage msg = await httpClient.SendAsync(request);

                // Log a success or error message based on the returned status code.
                if (HttpStatusCode.OK == msg.StatusCode)
                {
                    ServiceEventSource.Current.Trace(nameof(this.RegisterMetricsAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                    result = true;
                }
                else
                {
                    ServiceEventSource.Current.Error(nameof(this.RegisterMetricsAsync), Enum.GetName(typeof(HttpStatusCode), msg.StatusCode));
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Reports metrics that are fake.
        /// </summary>
        /// <param name="token">Cancellation token to monitor for cancellation requests.</param>
        /// <returns>A Task that represents outstanding operation.</returns>
        internal void ReportFakeMetrics(CancellationToken token)
        {
            Random rnd = new Random(DateTime.Now.Millisecond);

            // Load the list of current metric values to report.
            List<LoadMetric> metrics = new List<LoadMetric>()
            {
                new LoadMetric("RPS", rnd.Next(3000)),
                new LoadMetric("Failures", rnd.Next(0, 3)),
                new LoadMetric("Latency", rnd.Next(10, 500)),
                new LoadMetric("ItemCount", rnd.Next())
            };

            // Report the metrics to Service Fabric.
            this.Partition.ReportLoad(metrics);
        }
    }
}