//-----------------------------------------------------------------------
// <copyright file="CleanupOperation_UnitTest.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Bond;
using Bond.Protocols;
using Bond.IO.Safe;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.ServiceFabric.WatchdogService.Models;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Net;
using Microsoft.ServiceFabric.WatchdogService;
using WatchdogServiceTests.Mocks;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace WatchdogServiceTests
{
    [TestClass]
    public class CleanupOperation_UnitTest
    {
        MockWatchdogTelemetry telemetry = new MockWatchdogTelemetry();
        string endpoint = "http://127.0.0.1:10002/devstoreaccount1";
        string sasToken = "?sv=2015-04-05&sig=sjk4PClkLhNNsUZ3ks1rN9G8%2Bx3NKQyue%2BLVzpM3l8k%3D&st=2017-03-19T05%3A58%3A29Z&se=2017-03-19T06%3A58%3A29Z&srt=s&ss=t&sp=rdl";

        [TestMethod]
        public void CleanupOperation_ConstructorTest()
        {
            var cu = new CleanupOperations(telemetry, TimeSpan.FromSeconds(30), CancellationToken.None)
            {
                Endpoint = endpoint,
                SasToken = sasToken
            };
            Assert.IsNotNull(cu._cleanupTimer);
            Assert.AreEqual(telemetry, cu._telemetry);
            Assert.AreEqual(TimeSpan.FromSeconds(5), cu._timeout);
            Assert.AreEqual(sasToken, cu._sasToken);
            Assert.AreEqual(TimeSpan.FromDays(7), cu._timeToKeep);
            Assert.AreEqual(CancellationToken.None, cu._token);
            cu.Dispose();
        }

        #region Exception Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CleanupOperations_ConstructorArg1()
        {
            var cu = new CleanupOperations(null, TimeSpan.FromSeconds(30), CancellationToken.None)
            {
                Endpoint = endpoint,
                SasToken = sasToken
            };
        }

        #endregion
    }
}
