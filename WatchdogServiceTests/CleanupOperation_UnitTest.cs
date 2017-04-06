// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace WatchdogServiceTests
{
    using System;
    using System.Threading;
    using Microsoft.ServiceFabric.WatchdogService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using WatchdogServiceTests.Mocks;

    [TestClass]
    public class CleanupOperation_UnitTest
    {
        private MockWatchdogTelemetry telemetry = new MockWatchdogTelemetry();
        private string endpoint = "http://127.0.0.1:10002/devstoreaccount1";

        private string sasToken =
            "?sv=2015-04-05&sig=sjk4PClkLhNNsUZ3ks1rN9G8%2Bx3NKQyue%2BLVzpM3l8k%3D&st=2017-03-19T05%3A58%3A29Z&se=2017-03-19T06%3A58%3A29Z&srt=s&ss=t&sp=rdl";

        [TestMethod]
        public void CleanupOperation_ConstructorTest()
        {
            CleanupOperations cu = new CleanupOperations(this.telemetry, TimeSpan.FromSeconds(30), CancellationToken.None)
            {
                Endpoint = this.endpoint,
                SasToken = this.sasToken
            };
            Assert.IsNotNull(cu._cleanupTimer);
            Assert.AreEqual(this.telemetry, cu._telemetry);
            Assert.AreEqual(TimeSpan.FromSeconds(5), cu._timeout);
            Assert.AreEqual(this.sasToken, cu._sasToken);
            Assert.AreEqual(TimeSpan.FromDays(7), cu._timeToKeep);
            Assert.AreEqual(CancellationToken.None, cu._token);
            cu.Dispose();
        }

        #region Exception Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CleanupOperations_ConstructorArg1()
        {
            CleanupOperations cu = new CleanupOperations(null, TimeSpan.FromSeconds(30), CancellationToken.None)
            {
                Endpoint = this.endpoint,
                SasToken = this.sasToken
            };
        }

        #endregion
    }
}