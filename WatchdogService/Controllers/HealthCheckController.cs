//-----------------------------------------------------------------------
// <copyright file="HealthCheckController.cs" company="Microsoft Corporation">
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
            _operations = service.HealthCheckOperations;
        }

        #region Watchdog Health for Self Monitoring

        [HttpGet]
        [Route(@"health")]
        public async Task<HttpResponseMessage> GetWatchdogHealth()
        {
            // Check that an operations class exists.
            if (null == _operations)
                return Request.CreateResponse(HttpStatusCode.InternalServerError);

            // Check that there are items being monitored.
            var items = await _operations.GetHealthChecks();
            if (0 == items.Count)
                return Request.CreateResponse(HttpStatusCode.NoContent);

            // Return the status.
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        #endregion

        #region Health Check Operations

        [HttpGet]
        [Route(@"{application?}/{service?}/{partition=guid?}")]
        public async Task<HttpResponseMessage> GetHealthCheck(string application = null, string service = null, Guid? partition = null)
        {
            try
            {
                ServiceEventSource.Current.ServiceRequestStart(nameof(GetHealthCheck));

                // Get the list of health check items.
                IList<HealthCheck> items = await _operations.GetHealthChecks(application, service, partition);
                ServiceEventSource.Current.ServiceRequestStop(nameof(GetHealthCheck));

                return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch(Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(GetHealthCheck));
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]
        [Route(@"")]
        public async Task<HttpResponseMessage> PostHealthCheck([FromBody] HealthCheck hcm)
        {
            // Check required parameters.
            if (hcm.Equals(HealthCheck.Default))
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            if (null == _operations)
                return Request.CreateResponse(HttpStatusCode.InternalServerError);

            try
            {
                ServiceEventSource.Current.ServiceRequestStart(nameof(PostHealthCheck));

                // Call the operations class to add the health check.
                await _operations.AddHealthCheckAsync(hcm);

                ServiceEventSource.Current.ServiceRequestStop(nameof(PostHealthCheck));
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch(ArgumentException ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(PostHealthCheck));
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            catch(Exception ex)
            {
                ServiceEventSource.Current.Exception(ex.Message, ex.GetType().Name, nameof(PostHealthCheck));
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion
    }
}