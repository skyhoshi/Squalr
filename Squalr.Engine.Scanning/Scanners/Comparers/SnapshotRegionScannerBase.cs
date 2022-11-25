namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// A faster version of SnapshotElementComparer that takes advantage of vectorization/SSE instructions.
    /// </summary>
    internal unsafe abstract class SnapshotRegionScannerBase : ISnapshotRegionScanner
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionScannerBase" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionScannerBase(SnapshotRegion region, ScanConstraints constraints)
        {
            this.RunLengthEncoder = new SnapshotRegionRunLengthEncoder(region, constraints);
            this.Region = region;
            this.VectorSize = Vectors.VectorSize;
            this.VectorReadBase = this.Region.ReadGroupOffset - this.Region.ReadGroupOffset % this.VectorSize;
            this.VectorReadOffset = 0;
            this.DataType = constraints.ElementType;
            this.DataTypeSize = constraints.ElementType.Size;

            if (this.DataType is ByteArrayType)
            {
                this.Alignment = MemoryAlignment.Alignment1;
            }
            else
            {
                this.Alignment = constraints.Alignment == MemoryAlignment.Auto ? (MemoryAlignment)this.DataTypeSize : constraints.Alignment;
            }
        }

        /// <summary>
        /// Gets or sets the index from which the next vector is read.
        /// </summary>
        public Int32 VectorReadOffset { get; protected set; }

        /// <summary>
        /// Gets or sets the alignment offset, which is also used for reading the next vector.
        /// </summary>
        public Int32 AlignmentReadOffset { get; protected set; }

        /// <summary>
        /// Gets or sets the index from which the run length encoding is started.
        /// </summary>
        public Int32 RunLengthEncodeOffset { get; protected set; }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public UInt64 CurrentAddress
        {
            get
            {
                return Region.ReadGroup.BaseAddress + unchecked((UInt32)(this.VectorReadBase + this.VectorReadOffset + this.AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public Vector<Byte> CurrentValues
        {
            get
            {
                return new Vector<Byte>(this.Region.ReadGroup.CurrentValues, unchecked((Int32)(this.VectorReadBase + this.VectorReadOffset + this.AlignmentReadOffset)));
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index.
        /// </summary>
        public Vector<Byte> PreviousValues
        {
            get
            {
                return new Vector<Byte>(this.Region.ReadGroup.PreviousValues, unchecked((Int32)(this.VectorReadBase + this.VectorReadOffset + this.AlignmentReadOffset)));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public Vector<Byte> CurrentValuesArrayOfBytes
        {
            get
            {
                return new Vector<Byte>(this.Region.ReadGroup.CurrentValues, unchecked((Int32)(this.VectorReadBase + this.VectorReadOffset + this.ArrayOfBytesChunkIndex * this.VectorSize)));
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index.
        /// </summary>
        public Vector<Byte> PreviousValuesArrayOfBytes
        {
            get
            {
                return new Vector<Byte>(this.Region.ReadGroup.PreviousValues, unchecked((Int32)(this.VectorReadBase + this.VectorReadOffset + this.ArrayOfBytesChunkIndex * this.VectorSize)));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> CurrentValuesBigEndian16
        {
            get
            {
                Vector<Int16> result = Vector.AsVectorInt16(this.CurrentValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int16); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int16));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> PreviousValuesBigEndian16
        {
            get
            {
                Vector<Int16> result = Vector.AsVectorInt16(this.PreviousValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int16); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int16));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> CurrentValuesBigEndian32
        {
            get
            {
                Vector<Int32> result = Vector.AsVectorInt32(this.CurrentValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int32); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int32));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> PreviousValuesBigEndian32
        {
            get
            {
                Vector<Int32> result = Vector.AsVectorInt32(this.PreviousValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int32); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int32));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> CurrentValuesBigEndian64
        {
            get
            {
                Vector<Int64> result = Vector.AsVectorInt64(this.CurrentValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int64); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int64));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> PreviousValuesBigEndian64
        {
            get
            {
                Vector<Int64> result = Vector.AsVectorInt64(this.PreviousValues);

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int64); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(this.EndianStorage, index * sizeof(Int64));
                }

                return new Vector<Byte>(this.EndianStorage);
            }
        }

        /// <summary>
        /// Iterator for array of bytes vectorized chunks.
        /// </summary>
        protected Int32 ArrayOfBytesChunkIndex { get; set; }

        /// <summary>
        /// Temporary storage used to reverse the endianness of scanned values.
        /// </summary>
        protected Byte[] EndianStorage = new Byte[Vectors.VectorSize];

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        protected SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets a snapshot region run length encoder, which is used to create the snapshots from this scan.
        /// </summary>
        protected SnapshotRegionRunLengthEncoder RunLengthEncoder { get; private set; }

        /// <summary>
        /// Gets or sets the index of this element.
        /// </summary>
        protected Int32 VectorReadBase { get; set; }

        /// <summary>
        /// Gets or sets the SSE vector size on the machine.
        /// </summary>
        protected Int32 VectorSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        protected Int32 DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        protected MemoryAlignment Alignment { get; set; }

        /// <summary>
        /// Gets or sets the data type being compared.
        /// </summary>
        protected ScannableType DataType { get; set; }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public abstract IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints);
    }
    //// End class
}
//// End namespace
