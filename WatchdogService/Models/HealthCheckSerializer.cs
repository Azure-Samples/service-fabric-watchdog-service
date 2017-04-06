// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    using System.IO;
    using Bond;
    using Bond.IO.Unsafe;
    using Bond.Protocols;
    using Microsoft.ServiceFabric.Data;

    /// <summary>
    /// Custom serializer for HealthCheck instances.
    /// </summary>
    public sealed class HealthCheckSerializer : IStateSerializer<HealthCheck>
    {
        private static readonly Serializer<CompactBinaryWriter<OutputBuffer>> Serializer;
        private static readonly Deserializer<CompactBinaryReader<InputBuffer>> Deserializer;

        /// <summary>
        /// HealthCheckSerializer static constructor. Initialized serializers before the first call.
        /// </summary>
        static HealthCheckSerializer()
        {
            // Create the serializers and deserializers for FileMetadata.
            Serializer = new Serializer<CompactBinaryWriter<OutputBuffer>>(typeof(HealthCheck));
            Deserializer = new Deserializer<CompactBinaryReader<InputBuffer>>(typeof(HealthCheck));
        }

        /// <summary>
        /// HealthCheckSerializer constructor.
        /// </summary>
        public HealthCheckSerializer()
        {
        }

        /// <summary>
        /// Deserializes a binary input into a HealthCheck instance.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <returns></returns>
        public HealthCheck Read(BinaryReader binaryReader)
        {
            int count = binaryReader.ReadInt32();
            byte[] bytes = binaryReader.ReadBytes(count);

            InputBuffer input = new InputBuffer(bytes);
            CompactBinaryReader<InputBuffer> reader = new CompactBinaryReader<InputBuffer>(input);
            return Deserializer.Deserialize<HealthCheck>(reader);
        }

        public HealthCheck Read(HealthCheck baseValue, BinaryReader binaryReader)
        {
            return this.Read(binaryReader);
        }

        /// <summary>
        /// Serializes a HealthCheck instance into a binary stream.
        /// </summary>
        /// <param name="value">HealthCheck instance to serialize.</param>
        /// <param name="binaryReader">BinaryReader instance to serialize into.</param>
        public void Write(HealthCheck value, BinaryWriter binaryWriter)
        {
            OutputBuffer output = new OutputBuffer();
            CompactBinaryWriter<OutputBuffer> writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serializer.Serialize(value, writer);

            binaryWriter.Write(output.Data.Count);
            binaryWriter.Write(output.Data.Array, output.Data.Offset, output.Data.Count);
        }

        public void Write(HealthCheck baseValue, HealthCheck targetValue, BinaryWriter binaryWriter)
        {
            this.Write(targetValue, binaryWriter);
        }
    }
}