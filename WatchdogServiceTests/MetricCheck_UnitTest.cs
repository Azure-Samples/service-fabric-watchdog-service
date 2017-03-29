//-----------------------------------------------------------------------
// <copyright file="MetricCheck_UnitTest.cs" company="Microsoft Corporation">
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
using Microsoft.ServiceFabric.WatchdogService;
using System.Threading;
using System.Net.Http;
using System.Net;

namespace WatchdogServiceTests
{
    [TestClass]
    public class MetricCheck_UnitTest
    {
        static Guid partition1 = Guid.NewGuid();
        static string[] metricNames = new string[] { "metric1", "metric2", "metric3", "metric4" };
        static MetricCheck mc = new MetricCheck(metricNames, "appName", "svcName", partition1);

        [TestMethod]
        public void MetricCheck_ConstructorTest()
        {
            var mc1 = new MetricCheck();
            Assert.IsNull(mc1.MetricNames);
            Assert.IsNull(mc1.Application);
            Assert.IsNull(mc1.Service);
            Assert.AreEqual(default(Guid), mc1.Partition);

            // Check this equals the default MetricCheck value.
            Assert.IsTrue(mc1.Equals(MetricCheck.Default));

            // Check the copy constructor.
            var mc2 = new MetricCheck(mc1);
            Assert.IsTrue(mc1.Equals(mc2));
        }

        [TestMethod]
        public void MetricCheck_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            var output = new OutputBuffer();
            var writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, mc);

            // De-serialize from the binary output.
            var input = new InputBuffer(output.Data);
            var reader = new CompactBinaryReader<InputBuffer>(input);
            var hc1 = Deserialize<MetricCheck>.From(reader);
            Assert.IsTrue(mc.Equals(hc1));

            // Serialize as JSON using NewtonSoft.
            string json = JsonConvert.SerializeObject(mc, Formatting.None);
            hc1 = JsonConvert.DeserializeObject<MetricCheck>(json);
            Assert.IsTrue(mc.Equals(hc1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                BondCustomSerializer<MetricCheck> bcs = new BondCustomSerializer<MetricCheck>();
                bcs.Write(mc, bw);

                ms.Position = 0L;

                using (BinaryReader br = new BinaryReader(ms))
                {
                    hc1 = bcs.Read(br);
                    Assert.IsTrue(mc.Equals(hc1));
                }
            }
        }

        [TestMethod]
        public void MetricCheck_EqualsTest()
        {
            string[] metricNames2 = new string[] { "metric4", "metric2", "metric3", "metric1" };

            var mc1 = new MetricCheck(metricNames, "appName", "svcName", partition1);
            Assert.IsTrue(mc.Equals(mc1));

            mc1 = new MetricCheck(new string[0], "appName", "svcName", partition1);
            Assert.IsFalse(mc.Equals(mc1));

            mc1 = new MetricCheck(metricNames, "app", "svcName", partition1);
            Assert.IsFalse(mc.Equals(mc1));

            mc1 = new MetricCheck(metricNames2, "appName", null, partition1);
            Assert.IsFalse(mc.Equals(mc1));

            mc1 = new MetricCheck(metricNames2, "appName", "svcName", Guid.Empty);
            Assert.IsFalse(mc.Equals(mc1));
        }

        #region Exception Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MetricCheck_ConstructorArg1()
        {
            new MetricCheck(null, "appName", "svcName", partition1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MetricCheck_ConstructorArg2a()
        {
            new MetricCheck(metricNames, null, "svcName", partition1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MetricCheck_ConstructorArg2b()
        {
            new MetricCheck(metricNames, "", "svcName", partition1);
        }

        #endregion
    }
}
