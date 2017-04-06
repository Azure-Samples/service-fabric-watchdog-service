// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace WatchdogServiceTests
{
    using System;
    using Microsoft.ServiceFabric.WatchdogService.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BondTypeAliasConverter_UnitTest
    {
        [TestMethod]
        public void BondTypeAliasConverter_GuidConverterTest()
        {
            int offset = DateTime.Now.Second;

            // Allocate the GUID and prepare the arrays.
            Guid g1 = Guid.NewGuid();
            byte[] exactSizeArray = g1.ToByteArray();
            byte[] largeArray = new byte[128];
            Array.ConstrainedCopy(exactSizeArray, 0, largeArray, offset, exactSizeArray.Length);

            // Try with an exact 16 byte array.
            ArraySegment<byte> array = new ArraySegment<byte>(exactSizeArray);
            Guid guid = BondTypeAliasConverter.Convert(array, Guid.Empty);
            Assert.AreEqual(g1, guid);

            // Try with a buffer where the GUID is only part of the overall array.
            array = new ArraySegment<byte>(largeArray, offset, exactSizeArray.Length);
            guid = BondTypeAliasConverter.Convert(array, Guid.Empty);
            Assert.AreEqual(g1, guid);

            // Now the reverse direction.
            array = BondTypeAliasConverter.Convert(g1, array);
            Assert.AreEqual(exactSizeArray.Length, array.Count);

            offset = 0;
            foreach (byte b in array)
            {
                Assert.AreEqual(exactSizeArray[offset++], b);
            }
        }

        [TestMethod]
        public void BondTypeAliasConverter_TimeSpanConverterTest()
        {
            Assert.AreEqual(0L, BondTypeAliasConverter.Convert(TimeSpan.Zero, 1L));

            TimeSpan ts = TimeSpan.FromSeconds(DateTime.Now.Millisecond);
            long l1 = BondTypeAliasConverter.Convert(ts, 1L);
            Assert.AreEqual(ts.Ticks, l1);

            TimeSpan ts2 = BondTypeAliasConverter.Convert(l1, TimeSpan.Zero);
            Assert.AreEqual(ts, ts2);
        }

        [TestMethod]
        public void BondTypeAliasConverter_DateTimeOffsetConverterTest()
        {
            Assert.AreEqual(DateTimeOffset.MinValue.UtcTicks, BondTypeAliasConverter.Convert(DateTimeOffset.MinValue, 1L));

            DateTimeOffset dt = DateTimeOffset.Now;
            long l1 = BondTypeAliasConverter.Convert(dt, 1L);
            Assert.AreEqual(dt.UtcTicks, l1);

            DateTimeOffset dt2 = BondTypeAliasConverter.Convert(l1, DateTimeOffset.MinValue);
            Assert.AreEqual(dt, dt2);
        }

        [TestMethod]
        public void BondTypeAliasConverter_NullableDateTimeOffsetConverterTest()
        {
            Assert.AreEqual(0L, BondTypeAliasConverter.Convert(null, 1L));
            Assert.AreEqual(DateTimeOffset.MinValue.UtcTicks, BondTypeAliasConverter.Convert(DateTimeOffset.MinValue, 1L));

            DateTimeOffset? dt = DateTimeOffset.Now;
            long l1 = BondTypeAliasConverter.Convert(dt, 1L);
            Assert.AreEqual(dt.Value.UtcTicks, l1);

            DateTimeOffset? dt2 = BondTypeAliasConverter.Convert(l1, dt);
            Assert.AreEqual(dt, dt2);
            dt2 = BondTypeAliasConverter.Convert(0L, dt);
            Assert.IsFalse(dt2.HasValue);
        }

        [TestMethod]
        public void BondTypeAliasConverter_UriConverterTest()
        {
            Uri u1 = new Uri("http://tempuri.org");
            Uri u2 = new Uri("http://www.microsoft.com");
            Uri u3 = new Uri("fabric:/application/service");

            string s1 = BondTypeAliasConverter.Convert(u1, string.Empty);
            Assert.AreEqual(u1.AbsoluteUri, s1);
            string s2 = BondTypeAliasConverter.Convert(u2, string.Empty);
            Assert.AreEqual(u2.AbsoluteUri, s2);
            string s3 = BondTypeAliasConverter.Convert(u3, string.Empty);
            Assert.AreEqual(u3.AbsoluteUri, s3);

            Uri u = BondTypeAliasConverter.Convert(s1, u1);
            Assert.AreEqual(u1, u);
            u = BondTypeAliasConverter.Convert(s2, u1);
            Assert.AreEqual(u2, u);
            u = BondTypeAliasConverter.Convert(s3, u1);
            Assert.AreEqual(u3, u);
        }
    }
}