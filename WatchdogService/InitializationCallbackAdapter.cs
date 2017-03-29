//-----------------------------------------------------------------------
// <copyright file="InitializationCallbackAdapter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.WatchdogService.Models;
using System;
using System.Threading.Tasks;

namespace Microsoft.ServiceFabric.WatchdogService
{
    /// <summary>
    /// Enables configuration of the state manager.
    /// </summary>
    public sealed class InitializationCallbackAdapter
    {
        /// <summary>
        /// Initialized the adapter.
        /// </summary>
        /// <remarks>This is marked obsolete, but is supported. This interface is likely to change in the future.</remarks>
        [Obsolete("This method uses a method that is marked as obsolete.", false)]
        public Task OnInitialize()
        {
            // Add each of the types to be serialized.
            StateManager.TryAddStateSerializer(new BondCustomSerializer<HealthCheck>());
            StateManager.TryAddStateSerializer(new BondCustomSerializer<WatchdogScheduledItem>());
            return Task.FromResult(true);
        }

        public IReliableStateManager StateManager { get; set; }
    }
}

