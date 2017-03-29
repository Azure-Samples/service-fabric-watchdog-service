//-----------------------------------------------------------------------
// <copyright file="HealthCheck_UnitTest.cs" company="Microsoft Corporation">
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
using System.Fabric;
using WatchdogServiceTests.Mocks;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.WatchdogService;
using Microsoft.ServiceFabric.Data.Collections;
using System.Threading;
using System.Net.Http;
using System.Net;

namespace WatchdogServiceTests
{
    [TestClass]
    public class HealthCheck_UnitTest
    {
        static Uri svcName = new Uri("fabric:/application/service");
        static string suffixPath = "root/item?version=1";
        static Guid partition1 = Guid.NewGuid();
        static Guid partition2 = Guid.NewGuid();
        static Guid partition3 = Guid.NewGuid();
        static List<int> warnings = new List<int>() { 400, 401, 403 };
        static List<int> errors = new List<int>() { 500, 501 };
        static Dictionary<string, string> headers = new Dictionary<string, string>() { { "header1", "value1" }, { "header2", "value2" } };
        static HealthCheck hc = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);

        static ICodePackageActivationContext codePackageContext = new MockCodePackageActivationContext(
            svcName.AbsoluteUri,
            "applicationType",
            "Code",
            "1.0.0.0",
            Guid.NewGuid().ToString(),
            @"C:\Log", @"C:\Temp", @"C:\Work",
            "ServiceManifest",
            "1.0.0.0");

        StatefulServiceContext context = new StatefulServiceContext(
            new NodeContext("Node0", new NodeId(0, 1), 0, "NodeType1", "TEST.MACHINE"),
            codePackageContext,
            "WatchdogService.WatchdogServiceType", svcName, null, partition1, long.MaxValue);

        [TestMethod]
        public void HealthCheck_ConstructorTest()
        {
            var hcDefault = new HealthCheck();
            Assert.IsNull(hcDefault.ServiceName);
            Assert.IsNull(hcDefault.SuffixPath);
            Assert.IsNull(hcDefault.Content);
            Assert.IsNull(hcDefault.MediaType);
            Assert.IsNull(hcDefault.Headers);
            Assert.IsNull(hcDefault.WarningStatusCodes);
            Assert.IsNull(hcDefault.ErrorStatusCodes);
            Assert.AreEqual(default(Guid), hcDefault.Partition);
            Assert.AreEqual(default(TimeSpan), hcDefault.Frequency);
            Assert.AreEqual(default(HttpMethod), hcDefault.Method);
            Assert.AreEqual(default(TimeSpan), hcDefault.ExpectedDuration);
            Assert.AreEqual(default(TimeSpan), hcDefault.MaximumDuration);
            Assert.AreEqual(default(DateTimeOffset), hcDefault.LastAttempt);
            Assert.AreEqual(default(long), hcDefault.FailureCount);
            Assert.AreEqual(default(long), hcDefault.Duration);
            Assert.AreEqual(default(HttpStatusCode), hcDefault.ResultCode);

            // Check this equals the default HealthCheck value.
            Assert.IsTrue(hcDefault.Equals(HealthCheck.Default));

            // Check the copy constructor.
            var hc1 = new HealthCheck(hc);
            Assert.IsTrue(hc.Equals(hc1));
        }

        [TestMethod]
        public void HealthCheck_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            var output = new OutputBuffer();
            var writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, hc);

            // De-serialize from the binary output.
            var input = new InputBuffer(output.Data);
            var reader = new CompactBinaryReader<InputBuffer>(input);
            var hc1 = Deserialize<HealthCheck>.From(reader);
            Assert.IsTrue(hc.Equals(hc1));

            // Serialize as JSON using NewtonSoft.
            string json = JsonConvert.SerializeObject(hc, Formatting.None);
            hc1 = JsonConvert.DeserializeObject<HealthCheck>(json);
            Assert.IsTrue(hc.Equals(hc1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                BondCustomSerializer<HealthCheck> bcs = new BondCustomSerializer<HealthCheck>();
                bcs.Write(hc, bw);

                ms.Position = 0L;

                using (BinaryReader br = new BinaryReader(ms))
                {
                    hc1 = bcs.Read(br);
                    Assert.IsTrue(hc.Equals(hc1));
                }
            }

        }

        [TestMethod]
        public void HealthCheck_EqualsTest()
        {
            var hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsTrue(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName1", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", new Uri("fabric:/app/svc"), partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, Guid.Empty, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP2", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content1", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "text/plain", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, "root", "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Put, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(6), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(90), TimeSpan.FromMinutes(1), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(2), headers, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), null, errors, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, null, warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck("UniqueName", svcName, partition1, suffixPath, "EP1", "Content", "application/json", HttpMethod.Get, TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1), headers, errors, null);
            Assert.IsFalse(hc.Equals(hc1));
        }

        #region Exception Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void HealthCheckOperations_ConstructorArg1()
        {
            new HealthCheckOperations(null, null, TimeSpan.FromSeconds(30), CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void HealthCheckOperations_ConstructorArg2()
        {
            MockReliableStateManager stateManager = new MockReliableStateManager();
            var svc = new WatchdogService(context, new InitializationCallbackAdapter());
            new HealthCheckOperations(svc, null, TimeSpan.FromSeconds(30), CancellationToken.None);
        }

        #endregion
    }
}
