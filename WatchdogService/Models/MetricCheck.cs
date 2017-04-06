// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    using System;
    using System.Collections.Generic;
    using Bond;
    using Bond.IO;
    using Bond.Tag;
    using Newtonsoft.Json;

    /// <summary>
    /// MetricCheck structure definition.
    /// </summary>
    [Schema]
    public struct MetricCheck : IEquatable<MetricCheck>, ICloneable<MetricCheck>
    {
        /// <summary>
        /// Gets the default instance of MetricCheck.
        /// </summary>
        public static readonly MetricCheck Default = new MetricCheck();

        #region Public Properties

        /// <summary>
        /// Gets the metric name to monitor.
        /// </summary>
        [Id(10), Type(typeof(SortedSet<string>))]
        [JsonProperty(PropertyName = "metricName", Order = 10, Required = Required.Always)]
        public ISet<string> MetricNames { get; private set; }

        /// <summary>
        /// Gets the application name to which the metric belongs.
        /// </summary>
        [Id(20)]
        [JsonProperty(PropertyName = "application", Order = 20, Required = Required.Always)]
        public string Application { get; private set; }

        /// <summary>
        /// Gets the service name to which the metric belongs.
        /// </summary>
        [Id(30)]
        [JsonProperty(PropertyName = "service", Order = 30, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Service { get; private set; }

        /// <summary>
        /// Gets the partition identifier to which the metric belongs.
        /// </summary>
        [Id(40), Type(typeof(blob))]
        [JsonProperty(PropertyName = "partition", Order = 40, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Guid Partition { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// MetricCheck constructor.
        /// </summary>
        /// <param name="metricNames">Name of the metric to monitor.</param>
        /// <param name="application">Name of the application</param>
        /// <param name="service">Optional name of the service.</param>
        /// <param name="partition">Optional GUID containing the partition identifier.</param>
        public MetricCheck(
            string[] metricNames,
            string application,
            string service = null,
            Guid partition = default(Guid))
        {
            // Check required parameters.
            if (null == metricNames)
            {
                throw new ArgumentException(nameof(metricNames));
            }
            if (string.IsNullOrWhiteSpace(application))
            {
                throw new ArgumentException(nameof(application));
            }

            this.MetricNames = new SortedSet<string>(metricNames);
            this.Application = application;
            this.Service = service;
            this.Partition = partition;
        }

        /// <summary>
        /// MetricCheck constructor.
        /// </summary>
        /// <param name="other">Copies the values of an already initialized MetricCheck instance.</param>
        public MetricCheck(MetricCheck other)
        {
            this.MetricNames = other.MetricNames;
            this.Application = other.Application;
            this.Service = other.Service;
            this.Partition = other.Partition;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get the key for the MetricCheck instance.
        /// </summary>
        public string Key
        {
            get
            {
                // If both service and partition have values include them.
                if ((false == string.IsNullOrWhiteSpace(this.Service)) && (default(Guid) != this.Partition))
                {
                    return $"{this.Application}/{this.Service}/{this.Partition}";
                }
                else if (false == string.IsNullOrWhiteSpace(this.Service))
                {
                    return $"{this.Application}/{this.Service}";
                }

                return $"{this.Application}";
            }
        }

        #endregion

        #region IEquatable Interface Methods

        /// <summary>
        /// Compares to MetricCheck instances for equality.
        /// </summary>
        /// <param name="other">MetricCheck instance to compare.</param>
        /// <returns>True if they are equal, otherwise false.</returns>
        public bool Equals(MetricCheck other)
        {
            if (this.MetricNames?.Count != other.MetricNames?.Count)
            {
                return false;
            }
            if (this.Application != other.Application)
            {
                return false;
            }
            if (this.Service != other.Service)
            {
                return false;
            }
            if (this.Partition != other.Partition)
            {
                return false;
            }

            if ((null == this.MetricNames) && (null == other.MetricNames))
            {
                return true;
            }
            if ((null == this.MetricNames) || (null == other.MetricNames))
            {
                return false;
            }

            foreach (string s in this.MetricNames)
            {
                if (false == other.MetricNames.Contains(s))
                {
                    return false;
                }
            }


            return true;
        }

        #endregion

        #region IClonable Interface Methods

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>New MetricCheck instance containing the same data as the current instance.</returns>
        public MetricCheck Clone()
        {
            return Clone<MetricCheck>.From(this);
        }

        #endregion
    }
}