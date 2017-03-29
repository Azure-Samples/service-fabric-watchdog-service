//-----------------------------------------------------------------------
// <copyright file="MockTransaction.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using System;
using System.Threading.Tasks;

namespace WatchdogServiceTests.Mocks
{
    public class MockTransaction : ITransaction
    {
        public Task CommitAsync()
        {
            return Task.FromResult(true);
        }

        public void Abort()
        {
        }

        public long TransactionId
        {
            get { return 0L; }
        }

        public long CommitSequenceNumber
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
        }

        public Task<long> GetVisibilitySequenceNumberAsync()
        {
            return Task.FromResult(0L);
        }
    }
}
