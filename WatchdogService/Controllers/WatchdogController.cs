//-----------------------------------------------------------------------
// <copyright file="WatchdogController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Controllers
{
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

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
            _service = service;
        }

        #region Watchdog Health for Self Monitoring

        [HttpGet]
        [Route(@"health")]
        public async Task<HttpResponseMessage> GetWatchdogHealth()
        {
            // Check that the Watchdog service class exists.
            if (null == _service)
            {
                ServiceEventSource.Current.Error(nameof(GetWatchdogHealth), "WatchdogService instance is null.");
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check the HealthCheckOperation class exists.
            if (null == _service.HealthCheckOperations)
            {
                ServiceEventSource.Current.Error(nameof(GetWatchdogHealth), "HealthCheckOperations instance is null.");
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check the MetricsOperations class exists.
            if (null == _service.MetricsOperations)
            {
                ServiceEventSource.Current.Error(nameof(GetWatchdogHealth), "MetricsOperations instance is null.");
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check that there are items being monitored.
            var items = await _service.HealthCheckOperations.GetHealthChecks();
            if (0 == items.Count)
            {
                ServiceEventSource.Current.Warning(nameof(GetWatchdogHealth), "No HealthCheck have been registered with the watchdog.");
                return Request.CreateResponse(HttpStatusCode.NoContent);
            }

            // Return the status.
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion
    }
}