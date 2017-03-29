//-----------------------------------------------------------------------
// <copyright file="WatchdogScheduledItem_UnitTest.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Bond;
using Bond.Protocols;
using Bond.IO.Safe;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.ServiceFabric.WatchdogService.Models;
using System.IO;

namespace WatchdogServiceTests
{
    [TestClass]
    public class WatchdogScheduledItem_UnitTest
    {
        static string key = "fabric:/app/svc";
        static DateTimeOffset now = DateTimeOffset.UtcNow;
        static WatchdogScheduledItem hcs = new WatchdogScheduledItem(now, key);

        [TestMethod]
        public void WatchdogScheduledItem_ConstructorTest()
        {
            var hcsi = new WatchdogScheduledItem();
            Assert.IsNull(hcsi.Key);
            Assert.AreEqual(default(DateTimeOffset).UtcTicks, hcsi.ExecutionTicks);
        }

        [TestMethod]
        public void WatchdogScheduledItem_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            var output = new OutputBuffer();
            var writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, hcs);

            // De-serialize from the binary output.
            var input = new InputBuffer(output.Data);
            var reader = new CompactBinaryReader<InputBuffer>(input);
            var hcs1 = Deserialize<WatchdogScheduledItem>.From(reader);
            Assert.IsTrue(hcs.Equals(hcs1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                BondCustomSerializer<WatchdogScheduledItem> bcs = new BondCustomSerializer<WatchdogScheduledItem>();
                bcs.Write(hcs, bw);

                ms.Position = 0L;

                using (BinaryReader br = new BinaryReader(ms))
                {
                    hcs1 = bcs.Read(br);
                    Assert.IsTrue(hcs.Equals(hcs1));
                }
            }
        }

        [TestMethod]
        public void WatchdogScheduledItem_EqualsTest()
        {
            var hc1 = new WatchdogScheduledItem(now, key);
            Assert.IsTrue(hcs.Equals(hc1));

            hc1 = new WatchdogScheduledItem(DateTimeOffset.UtcNow, key);
            Assert.IsFalse(hcs.Equals(hc1));

            hc1 = new WatchdogScheduledItem(now, "fabric:/app/svc/0000000");
            Assert.IsFalse(hcs.Equals(hc1));
        }
    }
}
