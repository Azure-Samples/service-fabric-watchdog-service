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

    [TestClass]
    public class WatchdogScheduledItem_UnitTest
    {
        private static string key = "fabric:/app/svc";
        private static DateTimeOffset now = DateTimeOffset.UtcNow;
        private static WatchdogScheduledItem hcs = new WatchdogScheduledItem(now, key);

        [TestMethod]
        public void WatchdogScheduledItem_ConstructorTest()
        {
            WatchdogScheduledItem hcsi = new WatchdogScheduledItem();
            Assert.IsNull(hcsi.Key);
            Assert.AreEqual(default(DateTimeOffset).UtcTicks, hcsi.ExecutionTicks);
        }

        [TestMethod]
        public void WatchdogScheduledItem_SerializationTest()
        {
            // Serialize to the output buffer as binary.
            OutputBuffer output = new OutputBuffer();
            CompactBinaryWriter<OutputBuffer> writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, hcs);

            // De-serialize from the binary output.
            InputBuffer input = new InputBuffer(output.Data);
            CompactBinaryReader<InputBuffer> reader = new CompactBinaryReader<InputBuffer>(input);
            WatchdogScheduledItem hcs1 = Deserialize<WatchdogScheduledItem>.From(reader);
            Assert.IsTrue(hcs.Equals(hcs1));

            // Using the generic BondCustomSerializer.
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
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
        }

        [TestMethod]
        public void WatchdogScheduledItem_EqualsTest()
        {
            WatchdogScheduledItem hc1 = new WatchdogScheduledItem(now, key);
            Assert.IsTrue(hcs.Equals(hc1));

            hc1 = new WatchdogScheduledItem(DateTimeOffset.UtcNow, key);
            Assert.IsFalse(hcs.Equals(hc1));

            hc1 = new WatchdogScheduledItem(now, "fabric:/app/svc/0000000");
            Assert.IsFalse(hcs.Equals(hc1));
        }
    }
}