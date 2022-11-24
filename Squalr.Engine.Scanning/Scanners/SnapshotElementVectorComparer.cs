namespace Squalr.Engine.Scanning.Scanners
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A faster version of SnapshotElementComparer that takes advantage of vectorization/SSE instructions.
    /// </summary>
    internal unsafe class SnapshotElementVectorComparer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotElementVectorComparer" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotElementVectorComparer(SnapshotRegion region, ScanConstraints constraints)
        {
            Region = region;
            VectorSize = Vectors.VectorSize;
            VectorReadBase = Region.ReadGroupOffset - Region.ReadGroupOffset % VectorSize;
            VectorReadOffset = 0;
            DataType = constraints.ElementType;
            DataTypeSize = constraints.ElementType.Size;
            Alignment = constraints.Alignment;
            ResultRegions = new List<SnapshotRegion>();

            Compare = ElementCompare;
            // this.Compare = this.DataType is ByteArrayType ? this.ArrayOfBytesCompare : this.ElementCompare;

            SetConstraintFunctions();
            VectorCompare = BuildCompareActions(constraints?.RootConstraint);
        }

        /// <summary>
        /// Gets or sets the index from which the next vector is read.
        /// </summary>
        public int VectorReadOffset { get; private set; }

        /// <summary>
        /// Gets or sets the alignment offset, which is also used for reading the next vector.
        /// </summary>
        public int AlignmentReadOffset { get; private set; }

        /// <summary>
        /// Gets or sets the index from which the run length encoding is started.
        /// </summary>
        public int RunLengthEncodeOffset { get; private set; }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public ulong CurrentAddress
        {
            get
            {
                return Region.ReadGroup.BaseAddress + unchecked((uint)(VectorReadBase + VectorReadOffset + AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public Vector<byte> CurrentValues
        {
            get
            {
                return new Vector<byte>(Region.ReadGroup.CurrentValues, unchecked(VectorReadBase + VectorReadOffset + AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index.
        /// </summary>
        public Vector<byte> PreviousValues
        {
            get
            {
                return new Vector<byte>(Region.ReadGroup.PreviousValues, unchecked(VectorReadBase + VectorReadOffset + AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public Vector<byte> CurrentValuesArrayOfBytes
        {
            get
            {
                return new Vector<byte>(Region.ReadGroup.CurrentValues, unchecked(VectorReadBase + VectorReadOffset + ArrayOfBytesChunkIndex * VectorSize));
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index.
        /// </summary>
        public Vector<byte> PreviousValuesArrayOfBytes
        {
            get
            {
                return new Vector<byte>(Region.ReadGroup.PreviousValues, unchecked(VectorReadBase + VectorReadOffset + ArrayOfBytesChunkIndex * VectorSize));
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> CurrentValuesBigEndian16
        {
            get
            {
                Vector<short> result = Vector.AsVectorInt16(CurrentValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(short); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(short));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> PreviousValuesBigEndian16
        {
            get
            {
                Vector<short> result = Vector.AsVectorInt16(PreviousValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(short); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(short));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> CurrentValuesBigEndian32
        {
            get
            {
                Vector<int> result = Vector.AsVectorInt32(CurrentValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(int); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(int));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> PreviousValuesBigEndian32
        {
            get
            {
                Vector<int> result = Vector.AsVectorInt32(PreviousValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(int); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(int));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Gets the current values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> CurrentValuesBigEndian64
        {
            get
            {
                Vector<long> result = Vector.AsVectorInt64(CurrentValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(long); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(long));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Gets the previous values at the current vector read index in big endian format.
        /// </summary>
        public Vector<byte> PreviousValuesBigEndian64
        {
            get
            {
                Vector<long> result = Vector.AsVectorInt64(PreviousValues);

                for (int index = 0; index < Vectors.VectorSize / sizeof(long); index++)
                {
                    BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(result[index])).CopyTo(EndianStorage, index * sizeof(long));
                }

                return new Vector<byte>(EndianStorage);
            }
        }

        /// <summary>
        /// Iterator for array of bytes vectorized chunks.
        /// </summary>
        private int ArrayOfBytesChunkIndex { get; set; }

        /// <summary>
        /// Temporary storage used to reverse the endianness of scanned values.
        /// </summary>
        private byte[] EndianStorage = new byte[Vectors.VectorSize];

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        public Func<IList<SnapshotRegion>> Compare { get; private set; }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        private Func<Vector<byte>> VectorCompare { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has changed.
        /// </summary>
        private Func<Vector<byte>> Changed { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has not changed.
        /// </summary>
        private Func<Vector<byte>> Unchanged { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has increased.
        /// </summary>
        private Func<Vector<byte>> Increased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has decreased.
        /// </summary>
        private Func<Vector<byte>> Decreased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value equal to the given value.
        /// </summary>
        private Func<object, Vector<byte>> EqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value not equal to the given value.
        /// </summary>
        private Func<object, Vector<byte>> NotEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than to the given value.
        /// </summary>
        private Func<object, Vector<byte>> GreaterThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than or equal to the given value.
        /// </summary>
        private Func<object, Vector<byte>> GreaterThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<object, Vector<byte>> LessThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<object, Vector<byte>> LessThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has increased it's value by the given value.
        /// </summary>
        private Func<object, Vector<byte>> IncreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has decreased it's value by the given value.
        /// </summary>
        private Func<object, Vector<byte>> DecreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this array of bytes has a value equal to the given array of bytes.
        /// </summary>
        private Func<object, Vector<byte>, Vector<byte>> EqualToArrayOfBytes { get; set; }

        /// <summary>
        /// Gets a function which determines if this array of bytes has a value not equal to the given array of bytes.
        /// </summary>
        private Func<object, Vector<byte>, Vector<byte>> NotEqualToArrayOfBytes { get; set; }

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        private SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are currently encoding a new result region.
        /// </summary>
        private bool Encoding { get; set; }

        /// <summary>
        /// Gets or sets the current run length for run length encoded current scan results.
        /// </summary>
        private int RunLength { get; set; }

        /// <summary>
        /// Gets or sets the index of this element.
        /// </summary>
        private int VectorReadBase { get; set; }

        /// <summary>
        /// Gets or sets the SSE vector size on the machine.
        /// </summary>
        private int VectorSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        private int DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        private int Alignment { get; set; }

        /// <summary>
        /// Gets or sets the data type being compared.
        /// </summary>
        private ScannableType DataType { get; set; }

        /// <summary>
        /// Gets or sets the list of discovered result regions.
        /// </summary>
        private IList<SnapshotRegion> ResultRegions { get; set; }

        /// <summary>
        /// An alignment mask table for computing temporary run length encoding data during scans.
        /// </summary>
        private static readonly Vector<byte>[] AlignmentMaskTable = new Vector<byte>[8]
        {
                new Vector<byte>(1 << 0),
                new Vector<byte>(1 << 1),
                new Vector<byte>(1 << 2),
                new Vector<byte>(1 << 3),
                new Vector<byte>(1 << 4),
                new Vector<byte>(1 << 5),
                new Vector<byte>(1 << 6),
                new Vector<byte>(1 << 7),
        };

        /// <summary>
        /// Sets a custom comparison function to use in scanning.
        /// </summary>
        /// <param name="customCompare"></param>
        public void SetCustomCompareAction(Func<Vector<byte>> customCompare)
        {
            VectorCompare = customCompare;
        }

        /// <summary>
        /// Performs all vector comparisons, returning the discovered regions.
        /// </summary>
        public IList<SnapshotRegion> ElementCompare()
        {
            /*
             * This algorithm works as such:
             * 1) Load a vector of the data type to scan (say 128 bytes => 16 doubles).
             * 2) Simultaneously scan all 16 doubles (scan result will be true/false).
             * 3) Store the results in a run length encoding (RLE) vector.
             *      Important: this RLE vector is a lie, because if we are scanning mis-aligned ints, we will have missed them.
             *      For example, if there is no alignment, there are 7 additional doubles between each of the doubles we just scanned!
             * 4) For this reason, we maintain an RLE vector and populate the "in-between" values for any alignments.
             *      ie we may have a RLE vector of < 1111000, 00001111 ... >, which would indicate 4 consecutive successes, 8 consecutive fails, and 4 consecutive successes.
             *      The actual vector values will have consecutive duplicates, as we are mapping 16 double scan results to 128 bytes, meaning each double has 16 bytes of redundancy
             * 5) Process the RLE vector to update our RunLength variable, and encode any regions as they complete.
            */

            int scanCountPerVector = DataTypeSize / Alignment;
            int elementsPerVector = VectorSize / DataTypeSize;
            int incrementSize = VectorSize - scanCountPerVector;
            Vector<byte> runLengthVector;
            Vector<byte> allEqualsVector = new Vector<byte>(unchecked((byte)(1 << unchecked((byte)scanCountPerVector) - 1)));
            RunLengthEncodeOffset = VectorReadOffset;

            // TODO: This might be overkill, also this leaves some dangling values at the end for initial scans. We would need to mop up the final values using a non-vector comparerer.
            Region.ResizeForSafeReading(VectorSize);

            for (; VectorReadOffset < Region.RegionSize; VectorReadOffset += VectorSize)
            {
                runLengthVector = Vector<byte>.Zero;
                AlignmentReadOffset = 0;

                // For misalinged types, we will need to increment the vector read index and perform additional scans
                for (int alignment = 0; VectorReadOffset < Region.RegionSize && alignment < scanCountPerVector; alignment++)
                {
                    // Call the desired comparison function to get the results
                    Vector<byte> scanResults = VectorCompare();

                    // Store in-progress scan results for this batch
                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(scanResults, AlignmentMaskTable[alignment]));

                    AlignmentReadOffset++;
                }

                // Optimization: check all vector results true
                if (Vector.EqualsAll(runLengthVector, allEqualsVector))
                {
                    RunLength += elementsPerVector;
                    Encoding = true;
                    continue;
                }
                // Optimization: check all vector results false
                else if (Vector.EqualsAll(runLengthVector, Vector<byte>.Zero))
                {
                    EncodeCurrentResults();
                    continue;
                }

                // Otherwise the vector contains a mixture of true and false
                for (int resultIndex = 0; resultIndex < VectorSize; resultIndex += DataTypeSize)
                {
                    byte runLengthFlags = runLengthVector[resultIndex];

                    for (int alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                    {
                        bool runLengthResult = (runLengthFlags & unchecked((byte)(1 << alignmentIndex))) != 0;

                        if (runLengthResult)
                        {
                            RunLength++;
                            Encoding = true;
                        }
                        else
                        {
                            EncodeCurrentResults(resultIndex + alignmentIndex);
                        }
                    }
                }
            }

            return GatherCollectedRegions();
        }

        /*
        /// <summary>
        /// Performs all vector comparisons, returning the discovered regions.
        /// </summary>
        public IList<SnapshotRegion> ArrayOfBytesCompare()
        {
            Int32 ByteArraySize = (this.DataType as ByteArrayType)?.Length ?? 0;
            Byte[] Mask = (this.DataType as ByteArrayType)?.Mask;

            if (ByteArraySize <= 0)
            {
                return new List<SnapshotRegion>();
            }

            // Note that array of bytes must increment by 1 per iteration, unlike data type scans which can increment by vector size
            for (; this.VectorReadOffset <= this.Region.RegionSize - ByteArraySize; this.VectorReadOffset++)
            {
                Vector<Byte> scanResults = this.VectorCompare();

                // Optimization: check all vector results true (vector of 0xFF's, which is how SSE/AVX instructions store true)
                if (Vector.GreaterThanAll(scanResults, Vector<Byte>.Zero))
                {
                    this.RunLength += this.VectorSize;
                    this.Encoding = true;
                    continue;
                }

                // Optimization: check all vector results false
                else if (Vector.EqualsAll(scanResults, Vector<Byte>.Zero))
                {
                    this.EncodeCurrentResults(0, ByteArraySize);
                    continue;
                }

                // Otherwise the vector contains a mixture of true and false
                for (Int32 index = 0; index < this.VectorSize; index += this.DataTypeSize)
                {
                    // Vector result was false
                    if (scanResults[unchecked(index)] == 0)
                    {
                        this.EncodeCurrentResults(index, ByteArraySize);
                    }
                    // Vector result was true
                    else
                    {
                        this.RunLength += this.DataTypeSize;
                        this.Encoding = true;
                    }
                }
            }

            return this.GatherCollectedRegions();
        }*/

        /// <summary>
        /// Finalizes any leftover snapshot regions and returns them.
        /// </summary>
        public IList<SnapshotRegion> GatherCollectedRegions()
        {
            EncodeCurrentResults();
            return ResultRegions;
        }

        /// <summary>
        /// Encodes the current scan results if possible. This finalizes the current run-length encoded scan results to a snapshot region.
        /// </summary>
        /// <param name="vectorReadOffset">While performing run length encoding, the VectorReadOffset may have changed, and this can be used to make corrections.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncodeCurrentResults(int vectorReadOffset = 0)
        {
            // Create the final region if we are still encoding
            if (Encoding)
            {
                int readgroupOffset = VectorReadBase + VectorReadOffset + vectorReadOffset - RunLength * Alignment;
                ulong absoluteAddressStart = Region.ReadGroup.BaseAddress + unchecked((ulong)readgroupOffset);
                ulong absoluteAddressEnd = absoluteAddressStart + unchecked((ulong)RunLength);

                // Vector comparisons can produce some false positives since vectors can load values outside of the snapshot range.
                // This check catches any potential errors introduced this way.
                if (absoluteAddressStart >= Region.BaseAddress && absoluteAddressEnd <= Region.EndAddress)
                {
                    ResultRegions.Add(new SnapshotRegion(Region.ReadGroup, readgroupOffset, RunLength));
                }

                RunLength = 0;
                Encoding = false;
            }
        }

        /// <summary>
        /// Initializes all constraint functions for value comparisons.
        /// </summary>
        private unsafe void SetConstraintFunctions()
        {
            switch (DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    Changed = () => Vector.OnesComplement(Vector.Equals(CurrentValues, PreviousValues));
                    Unchanged = () => Vector.Equals(CurrentValues, PreviousValues);
                    Increased = () => Vector.GreaterThan(CurrentValues, PreviousValues);
                    Decreased = () => Vector.LessThan(CurrentValues, PreviousValues);
                    EqualToValue = (value) => Vector.Equals(CurrentValues, new Vector<byte>(unchecked((byte)value)));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.Equals(CurrentValues, new Vector<byte>(unchecked((byte)value))));
                    GreaterThanValue = (value) => Vector.GreaterThan(CurrentValues, new Vector<byte>(unchecked((byte)value)));
                    GreaterThanOrEqualToValue = (value) => Vector.GreaterThanOrEqual(CurrentValues, new Vector<byte>(unchecked((byte)value)));
                    LessThanValue = (value) => Vector.LessThan(CurrentValues, new Vector<byte>(unchecked((byte)value)));
                    LessThanOrEqualToValue = (value) => Vector.LessThanOrEqual(CurrentValues, new Vector<byte>(unchecked((byte)value)));
                    IncreasedByValue = (value) => Vector.Equals(CurrentValues, Vector.Add(PreviousValues, new Vector<byte>(unchecked((byte)value))));
                    DecreasedByValue = (value) => Vector.Equals(CurrentValues, Vector.Subtract(PreviousValues, new Vector<byte>(unchecked((byte)value))));
                    break;
                case ScannableType type when type == ScannableType.SByte:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), Vector.AsVectorSByte(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), Vector.AsVectorSByte(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(CurrentValues), Vector.AsVectorSByte(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(CurrentValues), Vector.AsVectorSByte(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSByte(CurrentValues), new Vector<sbyte>(unchecked((sbyte)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), Vector.Add(Vector.AsVectorSByte(PreviousValues), new Vector<sbyte>(unchecked((sbyte)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(CurrentValues), Vector.Subtract(Vector.AsVectorSByte(PreviousValues), new Vector<sbyte>(unchecked((sbyte)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int16:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), Vector.AsVectorInt16(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), Vector.AsVectorInt16(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(CurrentValues), Vector.AsVectorInt16(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(CurrentValues), Vector.AsVectorInt16(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(CurrentValues), new Vector<short>(unchecked((short)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), Vector.Add(Vector.AsVectorInt16(PreviousValues), new Vector<short>(unchecked((short)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValues), Vector.Subtract(Vector.AsVectorInt16(PreviousValues), new Vector<short>(unchecked((short)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int16BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.AsVectorInt16(PreviousValuesBigEndian16))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.AsVectorInt16(PreviousValuesBigEndian16)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.AsVectorInt16(PreviousValuesBigEndian16)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.AsVectorInt16(PreviousValuesBigEndian16)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(CurrentValuesBigEndian16), new Vector<short>(unchecked((short)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorInt16(PreviousValuesBigEndian16), new Vector<short>(unchecked((short)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorInt16(PreviousValuesBigEndian16), new Vector<short>(unchecked((short)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int32:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), Vector.AsVectorInt32(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), Vector.AsVectorInt32(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(CurrentValues), Vector.AsVectorInt32(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(CurrentValues), Vector.AsVectorInt32(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(CurrentValues), new Vector<int>(unchecked((int)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), Vector.Add(Vector.AsVectorInt32(PreviousValues), new Vector<int>(unchecked((int)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValues), Vector.Subtract(Vector.AsVectorInt32(PreviousValues), new Vector<int>(unchecked((int)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int32BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.AsVectorInt32(PreviousValuesBigEndian32))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.AsVectorInt32(PreviousValuesBigEndian32)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.AsVectorInt32(PreviousValuesBigEndian32)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.AsVectorInt32(PreviousValuesBigEndian32)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(CurrentValuesBigEndian32), new Vector<int>(unchecked((int)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorInt32(PreviousValuesBigEndian32), new Vector<int>(unchecked((int)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorInt32(PreviousValuesBigEndian32), new Vector<int>(unchecked((int)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int64:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), Vector.AsVectorInt64(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), Vector.AsVectorInt64(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(CurrentValues), Vector.AsVectorInt64(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(CurrentValues), Vector.AsVectorInt64(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), Vector.Add(Vector.AsVectorInt64(PreviousValues), new Vector<long>(unchecked((long)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValues), Vector.Subtract(Vector.AsVectorInt64(PreviousValues), new Vector<long>(unchecked((long)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int64BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.AsVectorInt64(PreviousValuesBigEndian64))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.AsVectorInt64(PreviousValuesBigEndian64)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.AsVectorInt64(PreviousValuesBigEndian64)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.AsVectorInt64(PreviousValuesBigEndian64)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), new Vector<long>(unchecked((long)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), new Vector<long>(unchecked((long)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(CurrentValues), new Vector<long>(unchecked((long)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(CurrentValuesBigEndian64), new Vector<long>(unchecked((long)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(CurrentValuesBigEndian64), new Vector<long>(unchecked((long)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(CurrentValuesBigEndian64), new Vector<long>(unchecked((long)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorInt64(PreviousValuesBigEndian64), new Vector<long>(unchecked((long)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorInt64(PreviousValuesBigEndian64), new Vector<long>(unchecked((long)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt16:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), Vector.AsVectorUInt16(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), Vector.AsVectorUInt16(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(CurrentValues), Vector.AsVectorUInt16(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(CurrentValues), Vector.AsVectorUInt16(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(CurrentValues), new Vector<ushort>(unchecked((ushort)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), Vector.Add(Vector.AsVectorUInt16(PreviousValues), new Vector<ushort>(unchecked((ushort)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValues), Vector.Subtract(Vector.AsVectorUInt16(PreviousValues), new Vector<ushort>(unchecked((ushort)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt16BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.AsVectorUInt16(PreviousValuesBigEndian16))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.AsVectorUInt16(PreviousValuesBigEndian16)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.AsVectorUInt16(PreviousValuesBigEndian16)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.AsVectorUInt16(PreviousValuesBigEndian16)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(CurrentValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorUInt16(PreviousValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorUInt16(PreviousValuesBigEndian16), new Vector<ushort>(unchecked((ushort)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt32:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), Vector.AsVectorUInt32(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), Vector.AsVectorUInt32(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(CurrentValues), Vector.AsVectorUInt32(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(CurrentValues), Vector.AsVectorUInt32(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(CurrentValues), new Vector<uint>(unchecked((uint)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), Vector.Add(Vector.AsVectorUInt32(PreviousValues), new Vector<uint>(unchecked((uint)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValues), Vector.Subtract(Vector.AsVectorUInt32(PreviousValues), new Vector<uint>(unchecked((uint)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt32BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.AsVectorUInt32(PreviousValuesBigEndian32))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.AsVectorUInt32(PreviousValuesBigEndian32)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.AsVectorUInt32(PreviousValuesBigEndian32)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.AsVectorUInt32(PreviousValuesBigEndian32)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(CurrentValuesBigEndian32), new Vector<uint>(unchecked((uint)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorUInt32(PreviousValuesBigEndian32), new Vector<uint>(unchecked((uint)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorUInt32(PreviousValuesBigEndian32), new Vector<uint>(unchecked((uint)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt64:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), Vector.AsVectorUInt64(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), Vector.AsVectorUInt64(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(CurrentValues), Vector.AsVectorUInt64(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(CurrentValues), Vector.AsVectorUInt64(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(CurrentValues), new Vector<ulong>(unchecked((ulong)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), Vector.Add(Vector.AsVectorUInt64(PreviousValues), new Vector<ulong>(unchecked((ulong)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValues), Vector.Subtract(Vector.AsVectorUInt64(PreviousValues), new Vector<ulong>(unchecked((ulong)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt64BE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.AsVectorUInt64(PreviousValuesBigEndian64))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.AsVectorUInt64(PreviousValuesBigEndian64)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.AsVectorUInt64(PreviousValuesBigEndian64)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.AsVectorUInt64(PreviousValuesBigEndian64)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(CurrentValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorUInt64(PreviousValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorUInt64(PreviousValuesBigEndian64), new Vector<ulong>(unchecked((ulong)value)))));
                    break;
                case ScannableType type when type == ScannableType.Single:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), Vector.AsVectorSingle(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), Vector.AsVectorSingle(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(CurrentValues), Vector.AsVectorSingle(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(CurrentValues), Vector.AsVectorSingle(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(CurrentValues), new Vector<float>(unchecked((float)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), Vector.Add(Vector.AsVectorSingle(PreviousValues), new Vector<float>(unchecked((float)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValues), Vector.Subtract(Vector.AsVectorSingle(PreviousValues), new Vector<float>(unchecked((float)value)))));
                    break;
                case ScannableType type when type == ScannableType.SingleBE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.AsVectorSingle(PreviousValuesBigEndian32))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.AsVectorSingle(PreviousValuesBigEndian32)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.AsVectorSingle(PreviousValuesBigEndian32)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.AsVectorSingle(PreviousValuesBigEndian32)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(CurrentValuesBigEndian32), new Vector<float>(unchecked((float)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorSingle(PreviousValuesBigEndian32), new Vector<float>(unchecked((float)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorSingle(PreviousValuesBigEndian32), new Vector<float>(unchecked((float)value)))));
                    break;
                case ScannableType type when type == ScannableType.Double:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), Vector.AsVectorDouble(PreviousValues))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), Vector.AsVectorDouble(PreviousValues)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(CurrentValues), Vector.AsVectorDouble(PreviousValues)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(CurrentValues), Vector.AsVectorDouble(PreviousValues)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(CurrentValues), new Vector<double>(unchecked((double)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), Vector.Add(Vector.AsVectorDouble(PreviousValues), new Vector<double>(unchecked((double)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValues), Vector.Subtract(Vector.AsVectorDouble(PreviousValues), new Vector<double>(unchecked((double)value)))));
                    break;
                case ScannableType type when type == ScannableType.DoubleBE:
                    Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.AsVectorDouble(PreviousValuesBigEndian64))));
                    Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.AsVectorDouble(PreviousValuesBigEndian64)));
                    Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.AsVectorDouble(PreviousValuesBigEndian64)));
                    Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.AsVectorDouble(PreviousValuesBigEndian64)));
                    EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value))));
                    NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value)))));
                    GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value))));
                    GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value))));
                    LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value))));
                    LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(CurrentValuesBigEndian64), new Vector<double>(unchecked((double)value))));
                    IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorDouble(PreviousValuesBigEndian64), new Vector<double>(unchecked((double)value)))));
                    DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorDouble(PreviousValuesBigEndian64), new Vector<double>(unchecked((double)value)))));
                    break;
                case ByteArrayType type:
                    Changed = () => new Vector<byte>(Convert.ToByte(!Vector.EqualsAll(CurrentValuesArrayOfBytes, PreviousValuesArrayOfBytes)));
                    Unchanged = () => new Vector<byte>(Convert.ToByte(Vector.EqualsAll(CurrentValuesArrayOfBytes, PreviousValuesArrayOfBytes)));
                    EqualToArrayOfBytes = (value, mask) => Vector.BitwiseOr(Vector.Equals(CurrentValuesArrayOfBytes, unchecked((Vector<byte>)value)), mask);
                    NotEqualToArrayOfBytes = (value, mask) => Vector.BitwiseOr(Vector.OnesComplement(Vector.Equals(CurrentValuesArrayOfBytes, unchecked((Vector<byte>)value))), mask);
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        /// <summary>
        /// Sets the default compare action to use for this element.
        /// </summary>
        /// <param name="constraint">The constraint(s) to use for the scan.</param>
        /// <param name="compareActionValue">The value to use for the scan.</param>
        private Func<Vector<byte>> BuildCompareActions(Constraint constraint)
        {
            switch (constraint)
            {
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
                                Vector<byte> resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();

                                // Early exit mechanism to prevent extra comparisons
                                if (resultLeft.Equals(Vector<byte>.Zero))
                                {
                                    return Vector<byte>.Zero;
                                }

                                Vector<byte> resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.BitwiseAnd(resultLeft, resultRight);
                            };
                        case OperationConstraint.OperationType.OR:
                            return () =>
                            {
                                Vector<byte> resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();

                                // Early exit mechanism to prevent extra comparisons
                                if (resultLeft.Equals(Vector<byte>.One))
                                {
                                    return Vector<byte>.One;
                                }

                                Vector<byte> resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.BitwiseOr(resultLeft, resultRight);
                            };
                        case OperationConstraint.OperationType.XOR:
                            return () =>
                            {
                                Vector<byte> resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();
                                Vector<byte> resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return Vector.Xor(resultLeft, resultRight);
                            };
                        default:
                            throw new ArgumentException("Unkown operation type");
                    }
                case ScanConstraint scanConstraint:
                    /*
                     * Array of bytes scan works as such:
                     * Chunk the array of bytes and mask (these should be == size) into hardware vector sized byte arrays.
                     * 
                     * Iterate over all chunks, comparing these to the corresponding values being scanned.
                     *   - Vector AND all of the results together for detecting equal/not equal. Early exit if any chunk fails.
                     *   - Vector OR all of the results together for detecting changed/unchanged
                    */
                    if (DataType is ByteArrayType)
                    {
                        ByteArrayType byteArrayType = DataType as ByteArrayType;
                        byte[] arrayOfBytes = scanConstraint?.ConstraintValue as byte[];
                        byte[] mask = scanConstraint?.ConstraintArgs as byte[];

                        if (arrayOfBytes == null || mask == null || arrayOfBytes.Length != mask.Length)
                        {
                            throw new ArgumentException("Array of bytes and mask must be provided with all array of byte scans. These should be equal in length.");
                        }

                        switch (scanConstraint.Constraint)
                        {
                            case ScanConstraint.ConstraintType.Unchanged:
                            case ScanConstraint.ConstraintType.Changed:

                                if (scanConstraint.Constraint == ScanConstraint.ConstraintType.Unchanged)
                                {
                                    return Unchanged;
                                }
                                else
                                {
                                    return Changed;
                                }
                            case ScanConstraint.ConstraintType.Equal:
                            case ScanConstraint.ConstraintType.NotEqual:
                                int remainder = arrayOfBytes.Length % VectorSize;
                                int chunkCount = arrayOfBytes.Length / VectorSize + (remainder > 0 ? 1 : 0);
                                Span<byte> arrayOfByteSpan = new Span<byte>(arrayOfBytes);
                                Span<byte> maskSpan = new Span<byte>(mask);
                                Vector<byte>[] arrayOfByteChunks = new Vector<byte>[chunkCount];
                                Vector<byte>[] maskChunks = new Vector<byte>[chunkCount];

                                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                                {
                                    int currentChunkSize = remainder > 0 && chunkIndex == chunkCount - 1 ? remainder : VectorSize;
                                    Span<byte> arrayOfBytesChunk = arrayOfByteSpan.Slice(VectorSize * chunkIndex, currentChunkSize);
                                    Span<byte> maskChunk = maskSpan.Slice(VectorSize * chunkIndex, currentChunkSize);

                                    if (currentChunkSize != VectorSize)
                                    {
                                        byte[] arrayOfBytesChunkPadded = Enumerable.Repeat<byte>(0x00, VectorSize).ToArray();
                                        byte[] maskChunkPadded = Enumerable.Repeat<byte>(0xFF, VectorSize).ToArray();

                                        arrayOfBytesChunk.CopyTo(arrayOfBytesChunkPadded);
                                        maskChunk.CopyTo(maskChunkPadded);

                                        arrayOfByteChunks[chunkIndex] = new Vector<byte>(arrayOfBytesChunkPadded);
                                        maskChunks[chunkIndex] = new Vector<byte>(maskChunkPadded);
                                    }
                                    else
                                    {
                                        arrayOfByteChunks[chunkIndex] = new Vector<byte>(arrayOfBytesChunk);
                                        maskChunks[chunkIndex] = new Vector<byte>(maskChunk);
                                    }
                                }

                                if (scanConstraint.Constraint == ScanConstraint.ConstraintType.Equal)
                                {
                                    return () =>
                                    {
                                        Vector<byte> result = Vector<byte>.One;

                                        for (ArrayOfBytesChunkIndex = 0; ArrayOfBytesChunkIndex < chunkCount; ArrayOfBytesChunkIndex++)
                                        {
                                            result = Vector.BitwiseAnd(result, EqualToArrayOfBytes(arrayOfByteChunks[ArrayOfBytesChunkIndex], maskChunks[ArrayOfBytesChunkIndex]));

                                            if (Vector.EqualsAll(result, Vector<byte>.Zero))
                                            {
                                                break;
                                            }
                                        }

                                        return result;
                                    };
                                }
                                else
                                {
                                    return () =>
                                    {
                                        Vector<byte> result = Vector<byte>.One;

                                        for (ArrayOfBytesChunkIndex = 0; ArrayOfBytesChunkIndex < chunkCount; ArrayOfBytesChunkIndex++)
                                        {
                                            result = Vector.BitwiseAnd(result, NotEqualToArrayOfBytes(arrayOfByteChunks[ArrayOfBytesChunkIndex], maskChunks[ArrayOfBytesChunkIndex]));

                                            if (Vector.EqualsAll(result, Vector<byte>.Zero))
                                            {
                                                break;
                                            }
                                        }

                                        return result;
                                    };
                                }
                            default:
                                throw new Exception("Unsupported constraint type");
                        }
                    }
                    else
                    {
                        switch (scanConstraint.Constraint)
                        {
                            case ScanConstraint.ConstraintType.Unchanged:
                                return Unchanged;
                            case ScanConstraint.ConstraintType.Changed:
                                return Changed;
                            case ScanConstraint.ConstraintType.Increased:
                                return Increased;
                            case ScanConstraint.ConstraintType.Decreased:
                                return Decreased;
                            case ScanConstraint.ConstraintType.IncreasedByX:
                                return () => IncreasedByValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.DecreasedByX:
                                return () => DecreasedByValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.Equal:
                                return () => EqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.NotEqual:
                                return () => NotEqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.GreaterThan:
                                return () => GreaterThanValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.GreaterThanOrEqual:
                                return () => GreaterThanOrEqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.LessThan:
                                return () => LessThanValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.LessThanOrEqual:
                                return () => LessThanOrEqualToValue(scanConstraint.ConstraintValue);
                            default:
                                throw new Exception("Unsupported constraint type");
                        }
                    }
                default:
                    throw new ArgumentException("Invalid constraint");
            }
        }
    }
    //// End class
}
//// End namespace