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

    public sealed class BondCustomSerializer<T> : IStateSerializer<T>
    {
        private static readonly Serializer<CompactBinaryWriter<OutputBuffer>> Serializer;
        private static readonly Deserializer<CompactBinaryReader<InputBuffer>> Deserializer;

        /// <summary>
        /// BondCustomSerializer static constructor. Initialized serializer/deserializers before the first call.
        /// </summary>
        static BondCustomSerializer()
        {
            // Create the serializers and deserializers for FileMetadata.
            Serializer = new Serializer<CompactBinaryWriter<OutputBuffer>>(typeof(T));
            Deserializer = new Deserializer<CompactBinaryReader<InputBuffer>>(typeof(T));
        }

        /// <summary>
        /// HealthCheckSerializer constructor.
        /// </summary>
        public BondCustomSerializer()
        {
        }

        /// <summary>
        /// Deserializes a binary input into a T instance.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <returns></returns>
        public T Read(BinaryReader binaryReader)
        {
            int count = binaryReader.ReadInt32();
            byte[] bytes = binaryReader.ReadBytes(count);

            InputBuffer input = new InputBuffer(bytes);
            CompactBinaryReader<InputBuffer> reader = new CompactBinaryReader<InputBuffer>(input);
            return Deserializer.Deserialize<T>(reader);
        }

        public T Read(T baseValue, BinaryReader binaryReader)
        {
            return this.Read(binaryReader);
        }

        /// <summary>
        /// Serializes a HealthCheck instance into a binary stream.
        /// </summary>
        /// <param name="value">HealthCheck instance to serialize.</param>
        /// <param name="binaryReader">BinaryReader instance to serialize into.</param>
        public void Write(T value, BinaryWriter binaryWriter)
        {
            OutputBuffer output = new OutputBuffer();
            CompactBinaryWriter<OutputBuffer> writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serializer.Serialize(value, writer);

            binaryWriter.Write(output.Data.Count);
            binaryWriter.Write(output.Data.Array, output.Data.Offset, output.Data.Count);
        }

        public void Write(T baseValue, T targetValue, BinaryWriter binaryWriter)
        {
            this.Write(targetValue, binaryWriter);
        }
    }
}