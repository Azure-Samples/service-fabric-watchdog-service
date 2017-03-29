//-----------------------------------------------------------------------
// <copyright file="IWatchdogService.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceFabric.WatchdogService.Interfaces
{
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
        /// Refreshes the FabricClient instance.
        /// </summary>
        void RefreshFabricClient();

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
    }
}
