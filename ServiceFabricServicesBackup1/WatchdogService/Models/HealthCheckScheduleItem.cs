// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    using System;
    using Bond;
    using Bond.IO;

    /// <summary>
    /// WatchdogScheduledItem structure used to hold the items scheduled to run.
    /// </summary>
    [Schema]
    public struct WatchdogScheduledItem : IEquatable<WatchdogScheduledItem>, ICloneable<WatchdogScheduledItem>
    {
        #region Public Fields

        /// <summary>
        /// Gets the time of the execution in Ticks.
        /// </summary>
        [Id(10)]
        public long ExecutionTicks { get; private set; }

        /// <summary>
        /// Gets the key used to retrieve the related health check item.
        /// </summary>
        [Id(20)]
        public string Key { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// WatchdogScheduledItem constructor.
        /// </summary>
        /// <param name="time">Time the item is scheduled to run.</param>
        /// <param name="key">Key to retrieve the item.</param>
        /// <param name="uri">Uri </param>
        public WatchdogScheduledItem(DateTimeOffset time, string key)
        {
            // Check required parameters.
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.ExecutionTicks = time.UtcTicks;
            this.Key = key;
        }

        #endregion

        #region IEquatable Interface Methods

        /// <summary>
        /// Compares to HealthCheck instances for equality.
        /// </summary>
        /// <param name="other">HealthCheck instance to compare.</param>
        /// <returns>True if they are equal, otherwise false.</returns>
        public bool Equals(WatchdogScheduledItem other)
        {
            if (this.ExecutionTicks != other.ExecutionTicks)
            {
                return false;
            }
            if (this.Key != other.Key)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region IClonable Interface Methods

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>New HealthCheck instance containing the same data as the current instance.</returns>
        public WatchdogScheduledItem Clone()
        {
            return Clone<WatchdogScheduledItem>.From(this);
        }

        #endregion
    }
}