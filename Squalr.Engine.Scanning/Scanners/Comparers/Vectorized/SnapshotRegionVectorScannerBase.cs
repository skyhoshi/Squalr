namespace Squalr.Engine.Scanning.Scanners.Comparers.Vectorized
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Buffers.Binary;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A faster version of SnapshotElementComparer that takes advantage of vectorization/SSE instructions.
    /// </summary>
    internal unsafe abstract class SnapshotRegionVectorScannerBase : SnapshotRegionScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionVectorScannerBase" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionVectorScannerBase() : base()
        {
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
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<Byte> CurrentValuesBigEndian16
        {
            get
            {
                Vector<Int16> result = Vector.AsVectorInt16(this.CurrentValues);
                Span<Int16> endianStorage = stackalloc Int16[Vectors.VectorSize / sizeof(Int16)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int16); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int16>(endianStorage));
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
                Span<Int16> endianStorage = stackalloc Int16[Vectors.VectorSize / sizeof(Int16)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int16); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int16>(endianStorage));
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
                Span<Int32> endianStorage = stackalloc Int32[Vectors.VectorSize / sizeof(Int32)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int32); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int32>(endianStorage));
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
                Span<Int32> endianStorage = stackalloc Int32[Vectors.VectorSize / sizeof(Int32)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int32); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int32>(endianStorage));
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
                Span<Int64> endianStorage = stackalloc Int64[Vectors.VectorSize / sizeof(Int64)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int64); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int64>(endianStorage));
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
                Span<Int64> endianStorage = stackalloc Int64[Vectors.VectorSize / sizeof(Int64)];

                for (Int32 index = 0; index < Vectors.VectorSize / sizeof(Int64); index++)
                {
                    endianStorage[index] = BinaryPrimitives.ReverseEndianness(result[index]);
                }

                return Vector.AsVectorByte(new Vector<Int64>(endianStorage));
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
        /// Gets the current values at the current vector read index.
        /// </summary>
        public UInt64 CurrentAddress
        {
            get
            {
                return Region.ReadGroup.BaseAddress + unchecked((UInt64)(this.VectorReadBase + this.VectorReadOffset + this.AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Gets or sets the base address from which vectors are read.
        /// </summary>
        protected Int32 VectorReadBase { get; set; }

        /// <summary>
        /// Gets the vector misalignment of the region being scanned.
        /// </summary>
        protected Int32 VectorMisalignment { get; private set; }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        protected Func<Vector<Byte>> VectorCompare { get; private set; }

        public override void Initialize(SnapshotRegion region, ScanConstraints constraints)
        {
            base.Initialize(region: region, constraints: constraints);

            this.VectorCompare = this.BuildCompareActions(constraints);
            this.VectorMisalignment = this.Region.GetByteCount(this.DataTypeSize) % Vectors.VectorSize;
            this.VectorReadBase = this.Region.ReadGroupOffset; // - this.VectorMisalignment;
            this.VectorReadOffset = 0;
        }

        /// <summary>
        /// Sets a custom comparison function to use in scanning.
        /// </summary>
        /// <param name="customCompare"></param>
        public void SetCustomCompareAction(Func<Vector<Byte>> customCompare)
        {
            this.VectorCompare = customCompare;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Int32 CalculateVectorOverRead()
        {
            Int32 availableByteCount = this.Region.GetByteCount(this.DataTypeSize);
            Int32 vectorRemainder = availableByteCount % Vectors.VectorSize;
            Int32 vectorAlignedByteCount = vectorRemainder <= 0 ? availableByteCount : (availableByteCount - vectorRemainder + Vectors.VectorSize);
            UInt64 vectorEndAddress = unchecked(this.Region.BaseElementAddress + (UInt64)vectorAlignedByteCount);
            Int32 vectorOverRead = vectorEndAddress <= this.Region.ReadGroup.EndAddress ? 0 : unchecked((Int32)(vectorEndAddress - this.Region.ReadGroup.EndAddress));

            return vectorOverRead;
        }

        /// <summary>
        /// Gets the appropriate comparison function for a changed value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonChanged()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.OnesComplement(Vector.Equals(this.CurrentValues, this.PreviousValues));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an unchanged value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonUnchanged()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.Equals(this.CurrentValues, this.PreviousValues);
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an increased value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonIncreased()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.GreaterThan(this.CurrentValues, this.PreviousValues);
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a decreased value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonDecreased()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.LessThan(this.CurrentValues, this.PreviousValues);
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an increased by value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonIncreasedBy(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.Equals(this.CurrentValues, Vector.Add(this.PreviousValues, new Vector<Byte>(unchecked((Byte)value))));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.Add(Vector.AsVectorSByte(this.PreviousValues), new Vector<SByte>(unchecked((SByte)value)))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.Add(Vector.AsVectorInt16(this.PreviousValues), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorInt16(this.PreviousValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.Add(Vector.AsVectorInt32(this.PreviousValues), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorInt32(this.PreviousValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.Add(Vector.AsVectorInt64(this.PreviousValues), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorInt64(this.PreviousValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.Add(Vector.AsVectorUInt16(this.PreviousValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorUInt16(this.PreviousValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.Add(Vector.AsVectorUInt32(this.PreviousValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorUInt32(this.PreviousValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.Add(Vector.AsVectorUInt64(this.PreviousValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorUInt64(this.PreviousValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.Add(Vector.AsVectorSingle(this.PreviousValues), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorSingle(this.PreviousValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.Add(Vector.AsVectorDouble(this.PreviousValues), new Vector<Double>(unchecked((Double)value)))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorDouble(this.PreviousValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a decreased by value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonDecreasedBy(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.Equals(this.CurrentValues, Vector.Subtract(this.PreviousValues, new Vector<Byte>(unchecked((Byte)value))));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.Subtract(Vector.AsVectorSByte(this.PreviousValues), new Vector<SByte>(unchecked((SByte)value)))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt16(this.PreviousValues), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorInt16(this.PreviousValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt32(this.PreviousValues), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorInt32(this.PreviousValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt64(this.PreviousValues), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorInt64(this.PreviousValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt16(this.PreviousValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorUInt16(this.PreviousValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt32(this.PreviousValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorUInt32(this.PreviousValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt64(this.PreviousValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorUInt64(this.PreviousValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.Subtract(Vector.AsVectorSingle(this.PreviousValues), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorSingle(this.PreviousValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.Subtract(Vector.AsVectorDouble(this.PreviousValues), new Vector<Double>(unchecked((Double)value)))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorDouble(this.PreviousValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an equal to value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.Equals(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a not equal to value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonNotEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.OnesComplement(Vector.Equals(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value))));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value)))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value)))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonGreaterThan(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.GreaterThan(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than or equal to value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonGreaterThanOrEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.GreaterThanOrEqual(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonLessThan(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.LessThan(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a less than or equal to value scan.
        /// </summary>
        private unsafe Func<Vector<Byte>> GetComparisonLessThanOrEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => Vector.LessThanOrEqual(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                case ScannableType type when type == ScannableType.SByte:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                case ScannableType type when type == ScannableType.Int16:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                case ScannableType type when type == ScannableType.Int32:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                case ScannableType type when type == ScannableType.Int64:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                case ScannableType type when type == ScannableType.UInt16:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                case ScannableType type when type == ScannableType.UInt32:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                case ScannableType type when type == ScannableType.UInt64:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                case ScannableType type when type == ScannableType.Single:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                case ScannableType type when type == ScannableType.Double:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Sets the default compare action to use for this element.
        /// </summary>
        /// <param name="constraint">The constraint(s) to use for the scan.</param>
        /// <param name="compareActionValue">The value to use for the scan.</param>
        private Func<Vector<Byte>> BuildCompareActions(IScanConstraint constraint)
        {
            switch (constraint)
            {
                case ScanConstraints scanConstraints:
                    return this.BuildCompareActions(scanConstraints?.RootConstraint);
                case OperationConstraint operationConstraint:
                    if (operationConstraint.Left == null || operationConstraint.Right == null)
                    {
                        throw new ArgumentException("An operation constraint must have both a left and right child");
                    }

                    switch (operationConstraint.BinaryOperation)
                    {
                        case OperationConstraint.OperationType.AND:
                            return () =>
                            {
                                Vector<Byte> resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();

                                // Early exit mechanism to prevent extra comparisons
                                if (resultLeft.Equals(Vector<Byte>.Zero))
                                {
                                    return Vector<Byte>.Zero;
                                }

                                Vector<Byte> resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.BitwiseAnd(resultLeft, resultRight);
                            };
                        case OperationConstraint.OperationType.OR:
                            return () =>
                            {
                                Vector<Byte> resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();

                                // Early exit mechanism to prevent extra comparisons
                                if (resultLeft.Equals(Vector<Byte>.One))
                                {
                                    return Vector<Byte>.One;
                                }

                                Vector<Byte> resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.BitwiseOr(resultLeft, resultRight);
                            };
                        case OperationConstraint.OperationType.XOR:
                            return () =>
                            {
                                Vector<Byte> resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();
                                Vector<Byte> resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.Xor(resultLeft, resultRight);
                            };
                        default:
                            throw new ArgumentException("Unkown operation type");
                    }
                case ScanConstraint scanConstraint:
                    switch (scanConstraint.Constraint)
                    {
                        case ScanConstraint.ConstraintType.Unchanged:
                            return this.GetComparisonUnchanged();
                        case ScanConstraint.ConstraintType.Changed:
                            return this.GetComparisonChanged();
                        case ScanConstraint.ConstraintType.Increased:
                            return this.GetComparisonIncreased();
                        case ScanConstraint.ConstraintType.Decreased:
                            return this.GetComparisonDecreased();
                        case ScanConstraint.ConstraintType.IncreasedByX:
                            return this.GetComparisonIncreasedBy(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.DecreasedByX:
                            return this.GetComparisonDecreasedBy(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.Equal:
                            return this.GetComparisonEqual(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.NotEqual:
                            return this.GetComparisonNotEqual(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.GreaterThan:
                            return this.GetComparisonGreaterThan(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.GreaterThanOrEqual:
                            return this.GetComparisonGreaterThanOrEqual(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.LessThan:
                            return this.GetComparisonLessThan(scanConstraint.ConstraintValue);
                        case ScanConstraint.ConstraintType.LessThanOrEqual:
                            return this.GetComparisonLessThanOrEqual(scanConstraint.ConstraintValue);
                        default:
                            throw new Exception("Unsupported constraint type");
                    }
                default:
                    throw new ArgumentException("Invalid constraint");
            }
        }
    }
    //// End class
}
//// End namespace
