//-----------------------------------------------------------------------
// <copyright file="MetricsController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Controllers
{
    using Models;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

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
            _operations = service.MetricsOperations;
        }

        #region Metric Operations

        [HttpGet]
        [Route(@"{application?}/{service?}/{partition:guid?}")]
        public async Task<HttpResponseMessage> GetMetricsByApplication([FromUri] string application = null, [FromUri] string service = null, [FromUri] Guid? partition = null)
        {
            var list = await _operations.GetMetricsAsync(application, service, partition);
            return Request.CreateResponse(HttpStatusCode.OK, list);
        }

        [HttpPost]
        [Route(@"{application}/{service?}/{partition:guid?}")]
        public async Task<HttpResponseMessage> PostPartitionMetric([FromUri] string application, [FromBody] string[] metrics, [FromUri] string service = null, [FromUri] Guid? partition = null)
        {
            // Check passed parameters.
            if (string.IsNullOrWhiteSpace(application))
                throw new ArgumentException("Argument is empty, null or whitespace", nameof(application));
            if (string.IsNullOrWhiteSpace(service))
                throw new ArgumentException("Argument is empty, null or whitespace", nameof(service));
            if ((null == metrics) || (0 == metrics.Length))
                throw new ArgumentNullException("Argument is null", nameof(metrics));

            // Add a MetricCheck instance.
            await _operations.AddMetricAsync(new MetricCheck(metrics, application, service, partition ?? default(Guid)));

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion

    }
}