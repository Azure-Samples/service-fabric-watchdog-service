// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Fabric;
    using System.Fabric.Description;
    using System.Globalization;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Dispatcher;
    using global::Owin;
    using Microsoft.Owin.Hosting;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;

    /// <summary>
    /// OwinCommunicationListener class. Listens to an HTTP port and directs to the controller.
    /// </summary>
    internal class OwinCommunicationListener : ICommunicationListener, IHttpControllerActivator
    {
        private readonly WatchdogService _service;
        private readonly ServiceContext _serviceContext;
        private readonly string _endpointName;
        private readonly string _appRoot;

        private IDisposable _webApp;
        private string _publishAddress;
        private string _listeningAddress;

        /// <summary>
        /// OwinCommunicationsListener constructor.
        /// </summary>
        /// <param name="service">WatchdogService instance.</param>
        /// <param name="serviceContext">Service Fabric context instance.</param>
        /// <param name="logger">ILogger instance.</param>
        /// <param name="endpointName">String containing the endpoint name.</param>
        /// <param name="appRoot">String containing the name of the application root.</param>
        public OwinCommunicationListener(WatchdogService service, ServiceContext serviceContext, string endpointName, string appRoot)
        {
            if (null == service)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (null == serviceContext)
            {
                throw new ArgumentNullException(nameof(serviceContext));
            }
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException(nameof(endpointName));
            }
            if (string.IsNullOrWhiteSpace(appRoot))
            {
                throw new ArgumentException(nameof(appRoot));
            }

            this._service = service;
            this._serviceContext = serviceContext;
            this._endpointName = endpointName;
            this._appRoot = appRoot;
        }

        /// <summary>
        /// Called to start communication with the service.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken instance.</param>
        /// <returns>String containing the URI the service will be listening on.</returns>
        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            EndpointResourceDescription serviceEndpoint = this._serviceContext.CodePackageActivationContext.GetEndpoint(this._endpointName);
            string protocol = Enum.GetName(typeof(EndpointProtocol), serviceEndpoint.Protocol).ToLowerInvariant();
            int port = serviceEndpoint.Port;

            if (this._serviceContext is StatefulServiceContext)
            {
                StatefulServiceContext statefulServiceContext = this._serviceContext as StatefulServiceContext;

                this._listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}://+:{1}/{2}{3}/{4}/{5}",
                    protocol,
                    port,
                    string.IsNullOrWhiteSpace(this._appRoot)
                        ? string.Empty
                        : this._appRoot.TrimEnd('/') + '/',
                    statefulServiceContext.PartitionId,
                    statefulServiceContext.ReplicaId,
                    Guid.NewGuid());
            }
            else if (this._serviceContext is StatelessServiceContext)
            {
                this._listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}://+:{1}/{2}",
                    protocol,
                    port,
                    string.IsNullOrWhiteSpace(this._appRoot)
                        ? string.Empty
                        : this._appRoot.TrimEnd('/') + '/');
            }
            else
            {
                throw new InvalidOperationException();
            }

            this._publishAddress = this._listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);

            try
            {
                ServiceEventSource.Current.ServiceMessage(this._serviceContext, "Starting web server on " + this._listeningAddress);
                this._webApp = WebApp.Start(this._listeningAddress, appBuilder => this.StartupConfiguration(appBuilder));
                ServiceEventSource.Current.ServiceMessage(this._serviceContext, "Listening on " + this._publishAddress);
                return Task.FromResult(this._publishAddress);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(this._serviceContext, "Web server failed to open. " + ex.ToString());
                this.StopWebServer();
                throw;
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.ServiceMessage(this._serviceContext, "Closing web server");
            this.StopWebServer();
            return Task.FromResult(true);
        }

        public void Abort()
        {
            ServiceEventSource.Current.ServiceMessage(this._serviceContext, "Aborting web server");
            this.StopWebServer();
        }

        #region IHttpControllerActivator interface

        /// <summary>
        /// Called to activate an instance of HTTP controller in the WebAPI pipeline
        /// </summary>
        /// <param name="request">HTTP request that triggered</param>
        /// <param name="controllerDescriptor">Description of the controller that was selected</param>
        /// <param name="controllerType">The type of the controller that was selected for this request</param>
        /// <returns>An instance of the selected HTTP controller</returns>
        /// <remarks>This is a cheap way to avoid a framework such as Unity. If already using Unity, that is a better approach.</remarks>
        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            if (null == controllerDescriptor)
            {
                throw new ArgumentNullException(nameof(controllerDescriptor));
            }

            // If the controller defines a constructor with a single parameter of the type which implements the MetricsOperations type, create a new instance and inject the instance.
            ConstructorInfo ci = controllerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                CallingConventions.HasThis,
                new[] {typeof(WatchdogService)},
                new ParameterModifier[0]);
            if (null != ci)
            {
                return ci.Invoke(new object[1] {this._service}) as IHttpController;
            }

            // If no matching constructor was found, just call the default parameter-less constructor 
            return Activator.CreateInstance(controllerDescriptor.ControllerType) as IHttpController;
        }

        #endregion

        private void StopWebServer()
        {
            if (this._webApp != null)
            {
                try
                {
                    this._webApp.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // no-op
                }
            }
        }

        /// <summary>
        /// Called when starting up the OWIN-based application, can be used to override the default configuration.
        /// </summary>
        /// <param name="app">IAppBuilder instance.</param>
        private void StartupConfiguration(IAppBuilder app)
        {
            HttpConfiguration config = new HttpConfiguration();
            //FormatterConfig.ConfigureFormatters(config.Formatters);

            config.MapHttpAttributeRoutes();
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            // Replace the default controller activator (to support optional injection of the service interface into the controllers)
            config.Services.Replace(typeof(IHttpControllerActivator), this);
            config.EnsureInitialized();
            app.UseWebApi(config);
        }
    }
}