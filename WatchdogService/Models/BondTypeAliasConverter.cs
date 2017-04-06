// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Bond type alias converter.
    /// </summary>
    public static class BondTypeAliasConverter
    {
        #region GUID converter

        /// <summary>
        /// Converts an ArraySegment of bytes to a GUID.
        /// </summary>
        /// <param name="value">ArraySegment of bytes.</param>
        /// <param name="unused">Type placeholder.</param>
        /// <returns>GUID instance.</returns>
        public static Guid Convert(ArraySegment<byte> value, Guid unused)
        {
            if (null == value)
            {
                throw new ArgumentNullException(nameof(value));
            }

            byte[] bytes = new byte[16];
            for (int i = 0; i < value.Count; i++)
            {
                bytes[i] = value.Array[i + value.Offset];
            }

            return new Guid(bytes);
        }

        /// <summary>
        /// Converts a GUID into an ArraySegment of bytes.
        /// </summary>
        /// <param name="value">GUID to convert.</param>
        /// <param name="unused">Type placeholder.</param>
        /// <returns>ArraySegment of bytes that make up the GUID value.</returns>
        public static ArraySegment<byte> Convert(Guid value, ArraySegment<byte> unused)
        {
            return new ArraySegment<byte>(value.ToByteArray());
        }

        #endregion

        #region TimeSpan converter

        /// <summary>
        /// Converts a TimeSpan to a long.
        /// </summary>
        /// <param name="value">Long value to convert.</param>
        /// <returns>TimeSpan value.</returns>
        public static TimeSpan Convert(long value, TimeSpan unused)
        {
            return TimeSpan.FromTicks(value);
        }

        /// <summary>
        /// Converts a TimeSpan to a long value.
        /// </summary>
        /// <param name="value">TimeSpan value to convert.</param>
        /// <returns>Long value.</returns>
        public static long Convert(TimeSpan value, long unused)
        {
            return value.Ticks;
        }

        #endregion

        #region DateTimeOffset converter

        /// <summary>
        /// Converts a DateTimeOffset to a long.
        /// </summary>
        /// <param name="value">Long value to convert.</param>
        /// <returns>DateTimeOffset value.</returns>
        public static DateTimeOffset Convert(long value, DateTimeOffset unused)
        {
            return new DateTimeOffset(value, TimeSpan.Zero);
        }

        /// <summary>
        /// Converts a DateTimeOffset to a long value.
        /// </summary>
        /// <param name="value">DateTimeOffset value to convert.</param>
        /// <returns>Long value.</returns>
        public static long Convert(DateTimeOffset value, long unused)
        {
            return value.UtcTicks;
        }

        #endregion

        #region DateTimeOffset? converter

        /// <summary>
        /// Converts a DateTimeOffset? to a long.
        /// </summary>
        /// <param name="value">Long value to convert.</param>
        /// <returns>DateTimeOffset? value.</returns>
        public static DateTimeOffset? Convert(long value, DateTimeOffset? unused)
        {
            if (0 == value)
            {
                return null;
            }

            return new DateTimeOffset(value, TimeSpan.Zero);
        }

        /// <summary>
        /// Converts a DateTimeOffset to a long value.
        /// </summary>
        /// <param name="value">DateTimeOffset value to convert.</param>
        /// <returns>Long value.</returns>
        public static long Convert(DateTimeOffset? value, long unused)
        {
            if (value.HasValue)
            {
                return value.Value.UtcTicks;
            }
            else
            {
                return 0L;
            }
        }

        #endregion

        #region Uri converter

        public static Uri Convert(string value, Uri unused)
        {
            return new Uri(value);
        }

        public static string Convert(Uri value, string unused)
        {
            return value.AbsoluteUri;
        }

        #endregion

        #region HttpMethod converter

        public static HttpMethod Convert(string value, HttpMethod unused)
        {
            return new HttpMethod(value);
        }

        public static string Convert(HttpMethod value, string unused)
        {
            return value.Method;
        }

        #endregion
    }
}