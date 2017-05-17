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
    /// HealthCheckController.
    /// </summary>
    [RoutePrefix("healthcheck")]
    public sealed class HealthCheckController : ApiController
    {
        /// <summary>
        /// TelemetryService instance.
        /// </summary>
        private readonly HealthCheckOperations _operations = null;

        /// <summary>
        /// HealthCheckController constructor.
        /// </summary>
        /// <param name="service">WatchdogService class instance.</param>
        internal HealthCheckController(WatchdogService service)
        {
            this._operations = service.HealthCheckOperations;
        }

        #region Watchdog Health for Self Monitoring

        [HttpGet]
        [Route(@"health")]
        public async Task<HttpResponseMessage> GetWatchdogHealth()
        {
            // Check that an operations class exists.
            if (null == this._operations)
            {
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // Check that there are items being monitored.
            IList<HealthCheck> items = await this._operations.GetHealthChecksAsync();
            if (0 == items.Count)
            {
                return this.Request.CreateResponse(HttpStatusCode.NoContent);
            }

            // Return the status.
            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion

        #region Health Check Operations

        [HttpGet]
        [Route(@"{application?}/{service?}/{partition=guid?}")]
        public async Task<HttpResponseMessage> GetHealthCheck(string application = null, string service = null, Guid? partition = null)
        {
            try
            {
                ServiceEventSource.Current.ServiceRequestStart(nameof(this.GetHealthCheck));

                // Get the list of health check items.
                IList<HealthCheck> items = await this._operations.GetHealthChecksAsync(application, service, partition);
                ServiceEventSource.Current.ServiceRequestStop(nameof(this.GetHealthCheck));

                return this.Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.GetHealthCheck));
                return this.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]
        [Route(@"")]
        public async Task<HttpResponseMessage> PostHealthCheck([FromBody] HealthCheck hcm)
        {
            // Check required parameters.
            if (hcm.Equals(HealthCheck.Default))
            {
                return this.Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            if (null == this._operations)
            {
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError);
            }

            try
            {
                ServiceEventSource.Current.ServiceRequestStart(nameof(this.PostHealthCheck));

                // Call the operations class to add the health check.
                await this._operations.AddHealthCheckAsync(hcm);

                ServiceEventSource.Current.ServiceRequestStop(nameof(this.PostHealthCheck));
                return this.Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (ArgumentException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.PostHealthCheck));
                return this.Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(this.PostHealthCheck));
                return this.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion
    }
}