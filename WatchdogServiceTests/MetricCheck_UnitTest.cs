// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace WatchdogServiceTests
{
    using System;
    using System.IO;
    using Bond;
    using Bond.IO.Safe;
    using Bond.Protocols;
    using Microsoft.ServiceFabric.WatchdogService.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class MetricCheck_UnitTest
    {
        private static Guid partition1 = Guid.NewGuid();
        private static string[] metricNames = new string[] {"metric1", "metric2", "metric3", "metric4"};
        private static MetricCheck mc = new MetricCheck(metricNames, "appName", "svcName", partition1);

        [TestMethod]
        public void MetricCheck_ConstructorTest()
        {
            MetricCheck mc1 = new MetricCheck();
            Assert.IsNull(mc1.MetricNames);
            Assert.IsNull(mc1.Application);
            Assert.IsNull(mc1.Service);
            Assert.AreEqual(default(Guid), mc1.Partition);

            // Check this equals the default MetricCheck value.
            Assert.IsTrue(mc1.Equals(MetricCheck.Default));

            // Check the copy constructor.
            MetricCheck mc2 = new MetricCheck(mc1);
            Assert.IsTrue(mc1.Equals(mc2));
        }

        [TestMethod]
        public void MetricCheck_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            OutputBuffer output = new OutputBuffer();
            CompactBinaryWriter<OutputBuffer> writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, mc);

            // De-serialize from the binary output.
            InputBuffer input = new InputBuffer(output.Data);
            CompactBinaryReader<InputBuffer> reader = new CompactBinaryReader<InputBuffer>(input);
            MetricCheck hc1 = Deserialize<MetricCheck>.From(reader);
            Assert.IsTrue(mc.Equals(hc1));

            // Serialize as JSON using NewtonSoft.
            string json = JsonConvert.SerializeObject(mc, Formatting.None);
            hc1 = JsonConvert.DeserializeObject<MetricCheck>(json);
            Assert.IsTrue(mc.Equals(hc1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
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
        }

        [TestMethod]
        public void MetricCheck_EqualsTest()
        {
            string[] metricNames2 = new string[] {"metric4", "metric2", "metric3", "metric1"};

            MetricCheck mc1 = new MetricCheck(metricNames, "appName", "svcName", partition1);
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