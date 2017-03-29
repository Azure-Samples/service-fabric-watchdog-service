//-----------------------------------------------------------------------
// <copyright file="MockWatchdogTelemetry.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.WatchdogService.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Fabric.Health;
using System.Threading;

namespace WatchdogServiceTests.Mocks
{
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
        IDictionary<string, string> dict = null;

        public string Key { get => key; set => key = value; }

        private void DefaultValues()
        {
            sVal1 = null;
            sVal2 = null;
            sVal3 = null;
            sVal4 = null;
            sVal5 = null;
            guid = Guid.Empty;
            dtoVal = DateTimeOffset.MinValue;
            tsVal = TimeSpan.Zero;
            bVal = false;
            iVal = 0;
            lVal1 = 0L;
            lVal2 = 0L;
            lVal3 = 0L;
            lVal4 = 0L;
            hs = HealthState.Invalid;
            dict = null;
        }

        public Task ReportAvailabilityAsync(string serviceUri, string instance, string testName, DateTimeOffset captured, TimeSpan duration, string location, bool success, CancellationToken cancellationToken, string message = null)
        {
            DefaultValues();

            sVal1 = serviceUri;
            sVal2 = instance;
            sVal3 = testName;
            sVal4 = location;
            sVal5 = message;
            dtoVal = captured;
            tsVal = duration;
            bVal = success;

            return Task.FromResult(0);
        }

        public Task ReportHealthAsync(string applicationName, string serviceName, string instance, string source, string property, HealthState state, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = applicationName;
            sVal2 = serviceName;
            sVal3 = instance;
            sVal4 = source;
            sVal5 = property;
            hs = state;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string name, long value, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = name;
            lVal1 = value;
            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string name, long value, IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = name;
            lVal1 = value;
            dict = properties;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string service, Guid partition, string name, long value, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = service;
            sVal2 = name;
            guid = partition;
            lVal1 = value;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string role, long id, string name, long value, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = role;
            lVal1 = id;
            sVal2 = name;
            lVal2 = value;

            return Task.FromResult(0);
        }

        public Task ReportMetricAsync(string roleName, string instance, string name, long value, int count, long min, long max, long sum, double deviation, IDictionary<string, string> properties, CancellationToken cancellationToken)
        {
            DefaultValues();

            sVal1 = roleName;
            sVal2 = instance;
            sVal3 = name;
            lVal1 = value;
            iVal = count;
            lVal2 = min;
            lVal3 = max;
            lVal4 = sum;
            dVal = deviation;
            dict = properties;

            return Task.FromResult(0);
        }
    }
}
