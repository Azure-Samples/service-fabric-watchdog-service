// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.ServiceFabric.WatchdogService.Models;

    /// <summary>
    /// MetricsController
    /// </summary>
    [RoutePrefix("metrics")]
    public sealed class MetricsController : ApiController
    {
        /// <summary>
        /// TelemetryService instance.
        /// </summary>
        private readonly MetricsOperations _operations = null;

        /// <summary>
        /// WatchdogController constructor.
        /// </summary><param name="service">Operations class instance.</param>
        internal MetricsController(WatchdogService service)
        {
            this._operations = service.MetricsOperations;
        }

        #region Metric Operations

        [HttpGet]
        [Route(@"{application?}/{service?}/{partition:guid?}")]
        public async Task<HttpResponseMessage> GetMetricsByApplication(
            [FromUri] string application = null, [FromUri] string service = null, [FromUri] Guid? partition = null)
        {
            IList<MetricCheck> list = await this._operations.GetMetricsAsync(application, service, partition);
            return this.Request.CreateResponse(HttpStatusCode.OK, list);
        }

        [HttpPost]
        [Route(@"{application}/{service?}/{partition:guid?}")]
        public async Task<HttpResponseMessage> PostPartitionMetric(
            [FromUri] string application, [FromBody] string[] metrics, [FromUri] string service = null, [FromUri] Guid? partition = null)
        {
            // Check passed parameters.
            if (string.IsNullOrWhiteSpace(application))
            {
                throw new ArgumentException("Argument is empty, null or whitespace", nameof(application));
            }
            if (string.IsNullOrWhiteSpace(service))
            {
                throw new ArgumentException("Argument is empty, null or whitespace", nameof(service));
            }
            if ((null == metrics) || (0 == metrics.Length))
            {
                throw new ArgumentNullException("Argument is null", nameof(metrics));
            }

            // Add a MetricCheck instance.
            await this._operations.AddMetricAsync(new MetricCheck(metrics, application, service, partition ?? default(Guid)));

            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion
    }
}