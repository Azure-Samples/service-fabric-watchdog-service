// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace WatchdogServiceTests.Mocks
{
    using System;
    using System.Collections.Generic;
    using System.Fabric.Health;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.WatchdogService.Interfaces;

    /// <summary>
    /// Mock that implements IWatchdogTelemetry.
    /// </summary>
    public class MockWatchdogTelemetry : IWatchdogTelemetry
    {
        // Public test values.
        public string key = "testkey";
        public string sVal1;
        public string sVal2;
        public string sVal3;
        public string sVal4;
        public string sVal5;
        public Guid guid;
        public DateTimeOffset dtoVal;
        public TimeSpan tsVal;
        public bool bVal;
        public int iVal;
        public double dVal;
        public long lVal1;
        public long lVal2;
        public long lVal3;
        public long lVal4;
        public HealthState hs;
        private IDictionary<string, string> dict = null;

        public string Key { get  => key; set  => key  = value; }

        public Task ReportAvailabilityAsync(
            string serviceUri, string instance, string testName, DateTimeOffset captured, TimeSpan duration, string location, bool success,
            CancellationToken cancellationToken, string message = null)
        {
            this.DefaultValues();

            this.sVal1 = serviceUri;
            this.sVal2 = instance;
            this.sVal3 = testName;
            this.sVal4 = location;
            this.sVal5 = message;
            this.dtoVal = captured;
            this.tsVal = duration;
            this.bVal = success;

            return Task.FromResult(0);
        }

        public Task ReportHealthAsync(
            string applicationName, string serviceName, string instance, string source, string property, HealthState state, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = applicationName;
            this.sVal2 = serviceName;
            this.sVal3 = instance;
            this.sVal4 = source;
            this.sVal5 = property;
            this.hs = state;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string name, long value, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = name;
            this.lVal1 = value;
            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string name, long value, IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = name;
            this.lVal1 = value;
            this.dict = properties;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string service, Guid partition, string name, long value, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = service;
            this.sVal2 = name;
            this.guid = partition;
            this.lVal1 = value;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string role, long id, string name, long value, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = role;
            this.lVal1 = id;
            this.sVal2 = name;
            this.lVal2 = value;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(
            string roleName, string instance, string name, long value, int count, long min, long max, long sum, double deviation,
            IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            this.DefaultValues();

            this.sVal1 = roleName;
            this.sVal2 = instance;
            this.sVal3 = name;
            this.lVal1 = value;
            this.iVal = count;
            this.lVal2 = min;
            this.lVal3 = max;
            this.lVal4 = sum;
            this.dVal = deviation;
            this.dict = properties;

            return Task.FromResult(0);
        }

        private void DefaultValues()
        {
            this.sVal1 = null;
            this.sVal2 = null;
            this.sVal3 = null;
            this.sVal4 = null;
            this.sVal5 = null;
            this.guid = Guid.Empty;
            this.dtoVal = DateTimeOffset.MinValue;
            this.tsVal = TimeSpan.Zero;
            this.bVal = false;
            this.iVal = 0;
            this.lVal1 = 0L;
            this.lVal2 = 0L;
            this.lVal3 = 0L;
            this.lVal4 = 0L;
            this.hs = HealthState.Invalid;
            this.dict = null;
        }
    }
}