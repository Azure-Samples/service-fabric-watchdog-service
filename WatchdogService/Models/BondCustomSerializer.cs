//-----------------------------------------------------------------------
// <copyright file="BondCustomSerializer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.ServiceFabric.Data;
using System.IO;
using Bond;
using Bond.Protocols;
using Bond.IO.Unsafe;

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    public sealed class BondCustomSerializer<T> : IStateSerializer<T>
    {
        private readonly static Serializer<CompactBinaryWriter<OutputBuffer>> Serializer;
        private readonly static Deserializer<CompactBinaryReader<InputBuffer>> Deserializer;

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

            var input = new InputBuffer(bytes);
            var reader = new CompactBinaryReader<InputBuffer>(input);
            return Deserializer.Deserialize<T>(reader);
        }

        public T Read(T baseValue, BinaryReader binaryReader)
        {
            return Read(binaryReader);
        }

        /// <summary>
        /// Serializes a HealthCheck instance into a binary stream.
        /// </summary>
        /// <param name="value">HealthCheck instance to serialize.</param>
        /// <param name="binaryReader">BinaryReader instance to serialize into.</param>
        public void Write(T value, BinaryWriter binaryWriter)
        {
            var output = new OutputBuffer();
            var writer = new CompactBinaryWriter<OutputBuffer>(output);
            Serializer.Serialize(value, writer);

            binaryWriter.Write(output.Data.Count);
            binaryWriter.Write(output.Data.Array, output.Data.Offset, output.Data.Count);
        }

        public void Write(T baseValue, T targetValue, BinaryWriter binaryWriter)
        {
            Write(targetValue, binaryWriter);
        }

    }
}
