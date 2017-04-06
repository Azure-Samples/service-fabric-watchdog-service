// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace WatchdogServiceTests
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using Bond;
    using Bond.IO.Safe;
    using Bond.Protocols;
    using Microsoft.ServiceFabric.WatchdogService;
    using Microsoft.ServiceFabric.WatchdogService.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using WatchdogServiceTests.Mocks;

    [TestClass]
    public class HealthCheck_UnitTest
    {
        private static Uri svcName = new Uri("fabric:/application/service");
        private static string suffixPath = "root/item?version=1";
        private static Guid partition1 = Guid.NewGuid();
        private static Guid partition2 = Guid.NewGuid();
        private static Guid partition3 = Guid.NewGuid();
        private static List<int> warnings = new List<int>() {400, 401, 403};
        private static List<int> errors = new List<int>() {500, 501};
        private static Dictionary<string, string> headers = new Dictionary<string, string>() {{"header1", "value1"}, {"header2", "value2"}};

        private static HealthCheck hc = new HealthCheck(
            "UniqueName",
            svcName,
            partition1,
            suffixPath,
            "EP1",
            "Content",
            "application/json",
            HttpMethod.Get,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMinutes(1),
            headers,
            errors,
            warnings);

        private static ICodePackageActivationContext codePackageContext = new MockCodePackageActivationContext(
            svcName.AbsoluteUri,
            "applicationType",
            "Code",
            "1.0.0.0",
            Guid.NewGuid().ToString(),
            @"C:\Log",
            @"C:\Temp",
            @"C:\Work",
            "ServiceManifest",
            "1.0.0.0");

        private StatefulServiceContext context = new StatefulServiceContext(
            new NodeContext("Node0", new NodeId(0, 1), 0, "NodeType1", "TEST.MACHINE"),
            codePackageContext,
            "WatchdogService.WatchdogServiceType",
            svcName,
            null,
            partition1,
            long.MaxValue);

        [TestMethod]
        public void HealthCheck_ConstructorTest()
        {
            HealthCheck hcDefault = new HealthCheck();
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
            HealthCheck hc1 = new HealthCheck(hc);
            Assert.IsTrue(hc.Equals(hc1));
        }

        [TestMethod]
        public void HealthCheck_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            OutputBuffer output = new OutputBuffer();
            CompactBinaryWriter<OutputBuffer> writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, hc);

            // De-serialize from the binary output.
            InputBuffer input = new InputBuffer(output.Data);
            CompactBinaryReader<InputBuffer> reader = new CompactBinaryReader<InputBuffer>(input);
            HealthCheck hc1 = Deserialize<HealthCheck>.From(reader);
            Assert.IsTrue(hc.Equals(hc1));

            // Serialize as JSON using NewtonSoft.
            string json = JsonConvert.SerializeObject(hc, Formatting.None);
            hc1 = JsonConvert.DeserializeObject<HealthCheck>(json);
            Assert.IsTrue(hc.Equals(hc1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
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
        }

        [TestMethod]
        public void HealthCheck_EqualsTest()
        {
            HealthCheck hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsTrue(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName1",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                new Uri("fabric:/app/svc"),
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                Guid.Empty,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP2",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content1",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "text/plain",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                "root",
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Put,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(6),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(90),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(2),
                headers,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                null,
                errors,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                null,
                warnings);
            Assert.IsFalse(hc.Equals(hc1));

            hc1 = new HealthCheck(
                "UniqueName",
                svcName,
                partition1,
                suffixPath,
                "EP1",
                "Content",
                "application/json",
                HttpMethod.Get,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMinutes(1),
                headers,
                errors,
                null);
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
            WatchdogService svc = new WatchdogService(this.context, new InitializationCallbackAdapter());
            new HealthCheckOperations(svc, null, TimeSpan.FromSeconds(30), CancellationToken.None);
        }

        #endregion
    }
}