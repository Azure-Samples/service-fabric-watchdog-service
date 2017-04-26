// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.WatchdogService.Models;

    /// <summary>
    /// Enables configuration of the state manager.
    /// </summary>
    public sealed class InitializationCallbackAdapter
    {
        public IReliableStateManager StateManager { get; set; }

        /// <summary>
        /// Initialized the adapter.
        /// </summary>
        /// <remarks>This is marked obsolete, but is supported. This interface is likely to change in the future.</remarks>
        [Obsolete("This method uses a method that is marked as obsolete.", false)]
        public Task OnInitialize()
        {
            // Add each of the types to be serialized.
            this.StateManager.TryAddStateSerializer(new BondCustomSerializer<HealthCheck>());
            this.StateManager.TryAddStateSerializer(new BondCustomSerializer<WatchdogScheduledItem>());
            return Task.FromResult(true);
        }
    }
}