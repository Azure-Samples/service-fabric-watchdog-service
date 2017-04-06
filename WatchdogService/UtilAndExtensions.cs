// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Net;
    using Microsoft.ServiceFabric.Services.Communication;

    public static class Extensions
    {
        /// <summary>
        /// Compares two lists of type T and returns an indication of their equality. Order of the items in the list doesn't matter.
        /// </summary>
        /// <typeparam name="T">Type contained within the list.</typeparam>
        /// <param name="l1">First instance of list.</param>
        /// <param name="l2">Second instance of list.</param>
        /// <returns>True if the lists are equal, otherwise false.</returns>
        public static bool ListEquals<T>(IReadOnlyList<T> l1, IReadOnlyList<T> l2) where T : IEquatable<T>
        {
            // Check if they are the same list.
            if (ReferenceEquals(l1, l2))
            {
                return true;
            }

            // If either list is null, return the correct value.
            if (null == l1)
            {
                return false;
            }
            if (null == l2)
            {
                return false;
            }

            // Compare the number of items in each list.
            if (l1.Count != l2.Count)
            {
                return false;
            }

            // Compare each item in the list for equality. if they are not equal, return false.
            foreach (T l1Item in l1)
            {
                bool found = false;

                // Look through each item in the second list for the matching item in the first list.
                foreach (T l2item in l2)
                {
                    // If found, break;
                    if (l1Item.Equals(l2item))
                    {
                        found = true;
                        break;
                    }
                }

                // if not found, the lists are different.
                if (!found)
                {
                    return false;
                }
            }

            // The lists are equal.
            return true;
        }

        /// <summary>
        /// Compares two dictionaries of a type and returns an indication of their equality.
        /// </summary>
        /// <typeparam name="K">Type of the dictionary key.</typeparam>
        /// <typeparam name="V">Type of the dicitonary value.</typeparam>
        /// <param name="d1">First dictionary instance.</param>
        /// <param name="d2">Second dictionary instance.</param>
        /// <returns>True if the dictionaries are equal, otherwise false.</returns>
        public static bool DictionaryEquals<K, V>(IReadOnlyDictionary<K, V> d1, IReadOnlyDictionary<K, V> d2) where V : IEquatable<V>
        {
            // Check if they are the same list.
            if (ReferenceEquals(d1, d2))
            {
                return true;
            }

            // If either dictionary is null, return the correct value.
            if (null == d1)
            {
                return false;
            }
            if (null == d2)
            {
                return false;
            }

            // Compare the number of items in each dictionary.
            if (d1.Count != d2.Count)
            {
                return false;
            }

            // Compare each key value pair in the dictionary for equality. If they are not equal, return the value.
            foreach (KeyValuePair<K, V> kvp in d1)
            {
                // Check that the key is contained within the second list and that their values are the same.
                if (false == d2.ContainsKey(kvp.Key))
                {
                    return false;
                }
                if (false == kvp.Value.Equals(d2[kvp.Key]))
                {
                    return false;
                }
            }

            // They are equal.
            return true;
        }

        /// <summary>
        /// Gets an indicator of success or failure HttpStatusCode.
        /// </summary>
        /// <param name="code">HttpStatusCode to evaluate.</param>
        /// <returns>True if a success code, otherwise false.</returns>
        public static bool IsSuccessCode(this HttpStatusCode code)
        {
            // If the code is outside of the success code range of 200-299, return false.
            if (((int) code < 200) || ((int) code > 299))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the first endpoint from the array of endpoints within a ResolvedServiceEndpoint.
        /// </summary>
        /// <param name="rse">ResolvedServiceEndpoint instance.</param>
        /// <returns>String containing the replica address.</returns>
        /// <exception cref="InvalidProgramException">ResolvedServiceEndpoint address list coudln't be parsed or no endpoints exist.</exception>
        public static string GetFirstEndpoint(this ResolvedServiceEndpoint rse)
        {
            ServiceEndpointCollection sec = null;
            if (ServiceEndpointCollection.TryParseEndpointsString(rse.Address, out sec))
            {
                string replicaAddress;
                if (sec.TryGetFirstEndpointAddress(out replicaAddress))
                {
                    return replicaAddress;
                }
            }

            throw new InvalidProgramException("ResolvedServiceEndpoint had invalid address");
        }

        /// <summary>
        /// Gets the endpoint from the array of endpoints using the listener name.
        /// </summary>
        /// <param name="rse">ResolvedServiceEndpoint instance.</param>
        /// <param name="name">Listener name.</param>
        /// <returns>String containing the replica address.</returns>
        /// <exception cref="ArgumentException">ResolvedServiceEndpoint address list coudln't be parsed.</exception>
        /// <exception cref="InvalidProgramException">ResolvedServiceEndpoint address list coudln't be parsed.</exception>
        public static string GetEndpoint(this ResolvedServiceEndpoint rse, string name)
        {
            ServiceEndpointCollection sec = null;
            if (ServiceEndpointCollection.TryParseEndpointsString(rse.Address, out sec))
            {
                string replicaAddress;
                if (sec.TryGetEndpointAddress(name, out replicaAddress))
                {
                    return replicaAddress;
                }
                else
                {
                    throw new ArgumentException(nameof(name));
                }
            }
            else
            {
                throw new InvalidProgramException("ResolvedServiceEndpoint had invalid address");
            }
        }
    }
}