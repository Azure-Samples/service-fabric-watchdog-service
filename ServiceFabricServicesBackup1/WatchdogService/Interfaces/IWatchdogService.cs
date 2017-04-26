// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Interfaces
{
    using System.Fabric;
    using System.Fabric.Description;
    using Microsoft.ServiceFabric.Data;

    /// <summary>
    /// IWatchdogService interface.
    /// </summary>
    public interface IWatchdogService
    {
        /// <summary>
        /// Service Fabric client instance with user level privledges.
        /// </summary>
        FabricClient Client { get; }

        /// <summary>
        /// Gets the Service Fabric StateManager instance.
        /// </summary>
        IReliableStateManager StateManager { get; }

        /// <summary>
        /// Get the ServiceContext.
        /// </summary>
        StatefulServiceContext Context { get; }

        /// <summary>
        /// Gets the read status of the partition.
        /// </summary>
        PartitionAccessStatus ReadStatus { get; }

        /// <summary>
        /// Gets the write status of the partition.
        /// </summary>
        PartitionAccessStatus WriteStatus { get; }

        /// <summary>
        /// Service Fabric configuration settings.
        /// </summary>
        ConfigurationSettings Settings { get; }

        /// <summary>
        /// Refreshes the FabricClient instance.
        /// </summary>
        void RefreshFabricClient();
    }
}