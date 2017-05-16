// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Controllers
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.ServiceFabric.WatchdogService.Models;

    /// <summary>
    /// MetricsController
    /// </summary>
    [RoutePrefix("watchdog")]
    public sealed class WatchdogController : ApiController
    {
        /// <summary>
        /// TelemetryService instance.
        /// </summary>
        private readonly WatchdogService _service = null;

        /// <summary>
        /// WatchdogController constructor.
        /// </summary><param name="service">Operations class instance.</param>
        internal WatchdogController(WatchdogService service)
        {
            this._service = service;
        }

        #region Watchdog Health for Self Monitoring

        [HttpGet]
        [Route(@"health")]
        public async Task<HttpResponseMessage> GetWatchdogHealth()
        {
            // Check that the Watchdog service class exists.
            if (null == this._service)
            {
                ServiceEventSource.Current.Error(nameof(this.GetWatchdogHealth), "WatchdogService instance is null.");
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check the HealthCheckOperation class exists.
            if (null == this._service.HealthCheckOperations)
            {
                ServiceEventSource.Current.Error(nameof(this.GetWatchdogHealth), "HealthCheckOperations instance is null.");
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check the MetricsOperations class exists.
            if (null == this._service.MetricsOperations)
            {
                ServiceEventSource.Current.Error(nameof(this.GetWatchdogHealth), "MetricsOperations instance is null.");
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check that there are items being monitored.
            IList<HealthCheck> items = await this._service.HealthCheckOperations.GetHealthChecksAsync();
            if (0 == items.Count)
            {
                ServiceEventSource.Current.Warning(nameof(this.GetWatchdogHealth), "No HealthCheck have been registered with the watchdog.");
                return this.Request.CreateResponse(HttpStatusCode.NoContent);
            }

            // Return the status.
            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion
    }
}