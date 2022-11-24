namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
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
            this.Region = region;
            this.VectorSize = Vectors.VectorSize;
            this.VectorReadBase = this.Region.ReadGroupOffset - this.Region.ReadGroupOffset % this.VectorSize;
            this.VectorReadOffset = 0;
            this.DataType = constraints.ElementType;
            this.DataTypeSize = constraints.ElementType.Size;
            this.Alignment = constraints.Alignment;
            this.ResultRegions = new List<SnapshotRegion>();

            this.Compare = this.ElementCompare;
            // this.Compare = this.DataType is ByteArrayType ? this.ArrayOfBytesCompare : this.ElementCompare;

            this.SetConstraintFunctions();
            this.VectorCompare = this.BuildCompareActions(constraints?.RootConstraint);
        }

        /// <summary>
        /// Gets or sets the index from which the next vector is read.
        /// </summary>
        public Int32 VectorReadOffset { get; private set; }

        /// <summary>
        /// Gets or sets the alignment offset, which is also used for reading the next vector.
        /// </summary>
        public Int32 AlignmentReadOffset { get; private set; }

        /// <summary>
        /// Gets or sets the index from which the run length encoding is started.
        /// </summary>
        public Int32 RunLengthEncodeOffset { get; private set; }

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

                for(Int32 index = 0; index < Vectors.VectorSize / sizeof(Int32); index++)
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
        private Int32 ArrayOfBytesChunkIndex { get; set; }

        /// <summary>
        /// Temporary storage used to reverse the endianness of scanned values.
        /// </summary>
        private Byte[] EndianStorage = new Byte[Vectors.VectorSize];

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        public Func<IList<SnapshotRegion>> Compare { get; private set; }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        private Func<Vector<Byte>> VectorCompare { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has changed.
        /// </summary>
        private Func<Vector<Byte>> Changed { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has not changed.
        /// </summary>
        private Func<Vector<Byte>> Unchanged { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has increased.
        /// </summary>
        private Func<Vector<Byte>> Increased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has decreased.
        /// </summary>
        private Func<Vector<Byte>> Decreased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value equal to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> EqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value not equal to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> NotEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> GreaterThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than or equal to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> GreaterThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> LessThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> LessThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has increased it's value by the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> IncreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has decreased it's value by the given value.
        /// </summary>
        private Func<Object, Vector<Byte>> DecreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this array of bytes has a value equal to the given array of bytes.
        /// </summary>
        private Func<Object, Vector<Byte>, Vector<Byte>> EqualToArrayOfBytes { get; set; }

        /// <summary>
        /// Gets a function which determines if this array of bytes has a value not equal to the given array of bytes.
        /// </summary>
        private Func<Object, Vector<Byte>, Vector<Byte>> NotEqualToArrayOfBytes { get; set; }

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        private SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are currently encoding a new result region.
        /// </summary>
        private Boolean Encoding { get; set; }

        /// <summary>
        /// Gets or sets the current run length for run length encoded current scan results.
        /// </summary>
        private Int32 RunLength { get; set; }

        /// <summary>
        /// Gets or sets the index of this element.
        /// </summary>
        private Int32 VectorReadBase { get; set; }

        /// <summary>
        /// Gets or sets the SSE vector size on the machine.
        /// </summary>
        private Int32 VectorSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        private Int32 DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        private Int32 Alignment { get; set; }

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
        private static readonly Vector<Byte>[] AlignmentMaskTable = new Vector<Byte>[8]
        {
                new Vector<Byte>(1 << 0),
                new Vector<Byte>(1 << 1),
                new Vector<Byte>(1 << 2),
                new Vector<Byte>(1 << 3),
                new Vector<Byte>(1 << 4),
                new Vector<Byte>(1 << 5),
                new Vector<Byte>(1 << 6),
                new Vector<Byte>(1 << 7),
        };

        /// <summary>
        /// Sets a custom comparison function to use in scanning.
        /// </summary>
        /// <param name="customCompare"></param>
        public void SetCustomCompareAction(Func<Vector<Byte>> customCompare)
        {
            this.VectorCompare = customCompare;
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
             * 5) Process the RLE vector to update our RunLength variable, and encode any regions as they complete.
            */

            Int32 scanCountPerVector = this.DataTypeSize / this.Alignment;
            Int32 elementsPerVector = this.VectorSize / this.DataTypeSize;
            Int32 incrementSize = this.VectorSize - scanCountPerVector;
            Vector<Byte> runLengthVector;
            Vector<Byte> allEqualsVector = new Vector<Byte>(unchecked((Byte)(1 << unchecked((Byte)scanCountPerVector) - 1)));
            this.RunLengthEncodeOffset = this.VectorReadOffset;

            // TODO: This might be overkill, also this leaves some dangling values at the end for initial scans. We would need to mop up the final values using a non-vector comparerer.
            this.Region.ResizeForSafeReading(this.VectorSize);

            for (; this.VectorReadOffset < this.Region.RegionSize; this.VectorReadOffset += this.VectorSize)
            {
                runLengthVector = Vector<Byte>.Zero;
                this.AlignmentReadOffset = 0;

                // For misalinged types, we will need to increment the vector read index and perform additional scans
                for (Int32 alignment = 0; this.VectorReadOffset < this.Region.RegionSize && alignment < scanCountPerVector; alignment++)
                {
                    // Call the desired comparison function to get the results
                    Vector<Byte> scanResults = this.VectorCompare();

                    // Store in-progress scan results for this batch
                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(scanResults, SnapshotElementVectorComparer.AlignmentMaskTable[alignment]));

                    this.AlignmentReadOffset++;
                }

                // Optimization: check all vector results true
                if (Vector.EqualsAll(runLengthVector, allEqualsVector))
                {
                    this.RunLength += elementsPerVector;
                    this.Encoding = true;
                    continue;
                }
                // Optimization: check all vector results false
                else if (Vector.EqualsAll(runLengthVector, Vector<Byte>.Zero))
                {
                    this.EncodeCurrentResults();
                    continue;
                }

                // Otherwise the vector contains a mixture of true and false
                for (Int32 resultIndex = 0; resultIndex < this.VectorSize; resultIndex += this.DataTypeSize)
                {
                    Byte runLengthFlags = runLengthVector[unchecked(resultIndex)];

                    for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                    {
                        Boolean runLengthResult = (runLengthFlags & unchecked((Byte)(1 << alignmentIndex))) != 0;

                        if (runLengthResult)
                        {
                            this.RunLength++;
                            this.Encoding = true;
                        }
                        else
                        {
                            this.EncodeCurrentResults(resultIndex + alignmentIndex);
                        }
                    }
                }

                /*
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
                    this.EncodeCurrentResults();
                    continue;
                }

                // Otherwise the vector contains a mixture of true and false
                for (Int32 index = 0; index < this.VectorSize; index += this.Alignment)
                {
                    // Vector result was false
                    if (scanResults[unchecked(index)] == 0)
                    {
                        this.EncodeCurrentResults(index);
                    }
                    // Vector result was true
                    else
                    {
                        this.RunLength += this.Alignment;
                        this.Encoding = true;
                    }
                }
                */
            }

            return this.GatherCollectedRegions();
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
            this.EncodeCurrentResults();
            return this.ResultRegions;
        }

        /// <summary>
        /// Encodes the current scan results if possible. This finalizes the current run-length encoded scan results to a snapshot region.
        /// </summary>
        /// <param name="vectorReadOffset">While performing run length encoding, the VectorReadOffset may have changed, and this can be used to make corrections.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncodeCurrentResults(Int32 vectorReadOffset = 0)
        {
            // Create the final region if we are still encoding
            if (this.Encoding)
            {
                Int32 readgroupOffset = this.VectorReadBase + this.VectorReadOffset + vectorReadOffset - this.RunLength;
                this.ResultRegions.Add(new SnapshotRegion(this.Region.ReadGroup, readgroupOffset, this.RunLength));
                this.RunLength = 0;
                this.Encoding = false;
            }
        }

        /// <summary>
        /// Initializes all constraint functions for value comparisons.
        /// </summary>
        private unsafe void SetConstraintFunctions()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    this.Changed = () => Vector.OnesComplement(Vector.Equals(this.CurrentValues, this.PreviousValues));
                    this.Unchanged = () => Vector.Equals(this.CurrentValues, this.PreviousValues);
                    this.Increased = () => Vector.GreaterThan(this.CurrentValues, this.PreviousValues);
                    this.Decreased = () => Vector.LessThan(this.CurrentValues, this.PreviousValues);
                    this.EqualToValue = (value) => Vector.Equals(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.Equals(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value))));
                    this.GreaterThanValue = (value) => Vector.GreaterThan(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                    this.GreaterThanOrEqualToValue = (value) => Vector.GreaterThanOrEqual(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                    this.LessThanValue = (value) => Vector.LessThan(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                    this.LessThanOrEqualToValue = (value) => Vector.LessThanOrEqual(this.CurrentValues, new Vector<Byte>(unchecked((Byte)value)));
                    this.IncreasedByValue = (value) => Vector.Equals(this.CurrentValues, Vector.Add(this.PreviousValues, new Vector<Byte>(unchecked((Byte)value))));
                    this.DecreasedByValue = (value) => Vector.Equals(this.CurrentValues, Vector.Subtract(this.PreviousValues, new Vector<Byte>(unchecked((Byte)value))));
                    break;
                case ScannableType type when type == ScannableType.SByte:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(this.CurrentValues), Vector.AsVectorSByte(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSByte(this.CurrentValues), new Vector<SByte>(unchecked((SByte)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.Add(Vector.AsVectorSByte(this.PreviousValues), new Vector<SByte>(unchecked((SByte)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSByte(this.CurrentValues), Vector.Subtract(Vector.AsVectorSByte(this.PreviousValues), new Vector<SByte>(unchecked((SByte)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int16:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValues), Vector.AsVectorInt16(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(this.CurrentValues), new Vector<Int16>(unchecked((Int16)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.Add(Vector.AsVectorInt16(this.PreviousValues), new Vector<Int16>(unchecked((Int16)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt16(this.PreviousValues), new Vector<Int16>(unchecked((Int16)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int16BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.AsVectorInt16(this.PreviousValuesBigEndian16)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorInt16(this.PreviousValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt16(this.CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorInt16(this.PreviousValuesBigEndian16), new Vector<Int16>(unchecked((Int16)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int32:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValues), Vector.AsVectorInt32(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(this.CurrentValues), new Vector<Int32>(unchecked((Int32)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.Add(Vector.AsVectorInt32(this.PreviousValues), new Vector<Int32>(unchecked((Int32)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt32(this.PreviousValues), new Vector<Int32>(unchecked((Int32)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int32BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.AsVectorInt32(this.PreviousValuesBigEndian32)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorInt32(this.PreviousValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt32(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorInt32(this.PreviousValuesBigEndian32), new Vector<Int32>(unchecked((Int32)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int64:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValues), Vector.AsVectorInt64(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.Add(Vector.AsVectorInt64(this.PreviousValues), new Vector<Int64>(unchecked((Int64)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValues), Vector.Subtract(Vector.AsVectorInt64(this.PreviousValues), new Vector<Int64>(unchecked((Int64)value)))));
                    break;
                case ScannableType type when type == ScannableType.Int64BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.AsVectorInt64(this.PreviousValuesBigEndian64)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorInt64(this.CurrentValues), new Vector<Int64>(unchecked((Int64)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorInt64(this.PreviousValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorInt64(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorInt64(this.PreviousValuesBigEndian64), new Vector<Int64>(unchecked((Int64)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt16:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValues), Vector.AsVectorUInt16(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(this.CurrentValues), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.Add(Vector.AsVectorUInt16(this.PreviousValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt16(this.PreviousValues), new Vector<UInt16>(unchecked((UInt16)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt16BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.AsVectorUInt16(this.PreviousValuesBigEndian16)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.Add(Vector.AsVectorUInt16(this.PreviousValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt16(this.CurrentValuesBigEndian16), Vector.Subtract(Vector.AsVectorUInt16(this.PreviousValuesBigEndian16), new Vector<UInt16>(unchecked((UInt16)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt32:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValues), Vector.AsVectorUInt32(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(this.CurrentValues), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.Add(Vector.AsVectorUInt32(this.PreviousValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt32(this.PreviousValues), new Vector<UInt32>(unchecked((UInt32)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt32BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.AsVectorUInt32(this.PreviousValuesBigEndian32)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorUInt32(this.PreviousValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt32(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorUInt32(this.PreviousValuesBigEndian32), new Vector<UInt32>(unchecked((UInt32)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt64:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValues), Vector.AsVectorUInt64(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(this.CurrentValues), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.Add(Vector.AsVectorUInt64(this.PreviousValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValues), Vector.Subtract(Vector.AsVectorUInt64(this.PreviousValues), new Vector<UInt64>(unchecked((UInt64)value)))));
                    break;
                case ScannableType type when type == ScannableType.UInt64BE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.AsVectorUInt64(this.PreviousValuesBigEndian64)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorUInt64(this.PreviousValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorUInt64(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorUInt64(this.PreviousValuesBigEndian64), new Vector<UInt64>(unchecked((UInt64)value)))));
                    break;
                case ScannableType type when type == ScannableType.Single:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValues), Vector.AsVectorSingle(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(this.CurrentValues), new Vector<Single>(unchecked((Single)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.Add(Vector.AsVectorSingle(this.PreviousValues), new Vector<Single>(unchecked((Single)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValues), Vector.Subtract(Vector.AsVectorSingle(this.PreviousValues), new Vector<Single>(unchecked((Single)value)))));
                    break;
                case ScannableType type when type == ScannableType.SingleBE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.AsVectorSingle(this.PreviousValuesBigEndian32)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), new Vector<Single>(unchecked((Single)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.Add(Vector.AsVectorSingle(this.PreviousValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorSingle(this.CurrentValuesBigEndian32), Vector.Subtract(Vector.AsVectorSingle(this.PreviousValuesBigEndian32), new Vector<Single>(unchecked((Single)value)))));
                    break;
                case ScannableType type when type == ScannableType.Double:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValues), Vector.AsVectorDouble(this.PreviousValues)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(this.CurrentValues), new Vector<Double>(unchecked((Double)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.Add(Vector.AsVectorDouble(this.PreviousValues), new Vector<Double>(unchecked((Double)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValues), Vector.Subtract(Vector.AsVectorDouble(this.PreviousValues), new Vector<Double>(unchecked((Double)value)))));
                    break;
                case ScannableType type when type == ScannableType.DoubleBE:
                    this.Changed = () => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64))));
                    this.Unchanged = () => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                    this.Increased = () => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                    this.Decreased = () => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.AsVectorDouble(this.PreviousValuesBigEndian64)));
                    this.EqualToValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                    this.NotEqualToValue = (value) => Vector.OnesComplement(Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                    this.GreaterThanValue = (value) => Vector.AsVectorByte(Vector.GreaterThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                    this.GreaterThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.GreaterThanOrEqual(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                    this.LessThanValue = (value) => Vector.AsVectorByte(Vector.LessThan(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                    this.LessThanOrEqualToValue = (value) => Vector.AsVectorByte(Vector.LessThanOrEqual(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), new Vector<Double>(unchecked((Double)value))));
                    this.IncreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.Add(Vector.AsVectorDouble(this.PreviousValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                    this.DecreasedByValue = (value) => Vector.AsVectorByte(Vector.Equals(Vector.AsVectorDouble(this.CurrentValuesBigEndian64), Vector.Subtract(Vector.AsVectorDouble(this.PreviousValuesBigEndian64), new Vector<Double>(unchecked((Double)value)))));
                    break;
                case ByteArrayType type:
                    this.Changed = () => new Vector<Byte>(Convert.ToByte(!Vector.EqualsAll(this.CurrentValuesArrayOfBytes, this.PreviousValuesArrayOfBytes)));
                    this.Unchanged = () => new Vector<Byte>(Convert.ToByte(Vector.EqualsAll(this.CurrentValuesArrayOfBytes, this.PreviousValuesArrayOfBytes)));
                    this.EqualToArrayOfBytes = (value, mask) => Vector.BitwiseOr(Vector.Equals(this.CurrentValuesArrayOfBytes, unchecked((Vector<Byte>)value)), mask);
                    this.NotEqualToArrayOfBytes = (value, mask) => Vector.BitwiseOr(Vector.OnesComplement(Vector.Equals(this.CurrentValuesArrayOfBytes, unchecked((Vector<Byte>)value))), mask);
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
        private Func<Vector<Byte>> BuildCompareActions(Constraint constraint)
        {
            switch(constraint)
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
                    /*
                     * Array of bytes scan works as such:
                     * Chunk the array of bytes and mask (these should be == size) into hardware vector sized byte arrays.
                     * 
                     * Iterate over all chunks, comparing these to the corresponding values being scanned.
                     *   - Vector AND all of the results together for detecting equal/not equal. Early exit if any chunk fails.
                     *   - Vector OR all of the results together for detecting changed/unchanged
                    */
                    if (this.DataType is ByteArrayType)
                    {
                        ByteArrayType byteArrayType = this.DataType as ByteArrayType;
                        Byte[] arrayOfBytes = scanConstraint?.ConstraintValue as Byte[];
                        Byte[] mask = scanConstraint?.ConstraintArgs as Byte[];

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
                                    return this.Unchanged;
                                }
                                else
                                {
                                    return this.Changed;
                                }
                            case ScanConstraint.ConstraintType.Equal:
                            case ScanConstraint.ConstraintType.NotEqual:
                                Int32 remainder = arrayOfBytes.Length % this.VectorSize;
                                Int32 chunkCount = arrayOfBytes.Length / this.VectorSize + (remainder > 0 ? 1 : 0);
                                Span<Byte> arrayOfByteSpan = new Span<Byte>(arrayOfBytes);
                                Span<Byte> maskSpan = new Span<Byte>(mask);
                                Vector<Byte>[] arrayOfByteChunks = new Vector<Byte>[chunkCount];
                                Vector<Byte>[] maskChunks = new Vector<Byte>[chunkCount];

                                for (Int32 chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                                {
                                    Int32 currentChunkSize = remainder > 0 && chunkIndex == chunkCount - 1 ? remainder : this.VectorSize;
                                    Span<Byte> arrayOfBytesChunk = arrayOfByteSpan.Slice(this.VectorSize * chunkIndex, currentChunkSize);
                                    Span<Byte> maskChunk = maskSpan.Slice(this.VectorSize * chunkIndex, currentChunkSize);

                                    if (currentChunkSize != this.VectorSize)
                                    {
                                        Byte[] arrayOfBytesChunkPadded = Enumerable.Repeat<Byte>(0x00, this.VectorSize).ToArray();
                                        Byte[] maskChunkPadded = Enumerable.Repeat<Byte>(0xFF, this.VectorSize).ToArray();

                                        arrayOfBytesChunk.CopyTo(arrayOfBytesChunkPadded);
                                        maskChunk.CopyTo(maskChunkPadded);

                                        arrayOfByteChunks[chunkIndex] = new Vector<Byte>(arrayOfBytesChunkPadded);
                                        maskChunks[chunkIndex] = new Vector<Byte>(maskChunkPadded);
                                    }
                                    else
                                    {
                                        arrayOfByteChunks[chunkIndex] = new Vector<Byte>(arrayOfBytesChunk);
                                        maskChunks[chunkIndex] = new Vector<Byte>(maskChunk);
                                    }
                                }

                                if (scanConstraint.Constraint == ScanConstraint.ConstraintType.Equal)
                                {
                                    return () =>
                                    {
                                        Vector<Byte> result = Vector<Byte>.One;

                                        for (this.ArrayOfBytesChunkIndex = 0; this.ArrayOfBytesChunkIndex < chunkCount; this.ArrayOfBytesChunkIndex++)
                                        {
                                            result = Vector.BitwiseAnd(result, this.EqualToArrayOfBytes(arrayOfByteChunks[this.ArrayOfBytesChunkIndex], maskChunks[this.ArrayOfBytesChunkIndex]));

                                            if (Vector.EqualsAll(result, Vector<Byte>.Zero))
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
                                        Vector<Byte> result = Vector<Byte>.One;

                                        for (this.ArrayOfBytesChunkIndex = 0; this.ArrayOfBytesChunkIndex < chunkCount; this.ArrayOfBytesChunkIndex++)
                                        {
                                            result = Vector.BitwiseAnd(result, this.NotEqualToArrayOfBytes(arrayOfByteChunks[this.ArrayOfBytesChunkIndex], maskChunks[this.ArrayOfBytesChunkIndex]));

                                            if (Vector.EqualsAll(result, Vector<Byte>.Zero))
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
                                return this.Unchanged;
                            case ScanConstraint.ConstraintType.Changed:
                                return this.Changed;
                            case ScanConstraint.ConstraintType.Increased:
                                return this.Increased;
                            case ScanConstraint.ConstraintType.Decreased:
                                return this.Decreased;
                            case ScanConstraint.ConstraintType.IncreasedByX:
                                return () => this.IncreasedByValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.DecreasedByX:
                                return () => this.DecreasedByValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.Equal:
                                return () => this.EqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.NotEqual:
                                return () => this.NotEqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.GreaterThan:
                                return () => this.GreaterThanValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.GreaterThanOrEqual:
                                return () => this.GreaterThanOrEqualToValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.LessThan:
                                return () => this.LessThanValue(scanConstraint.ConstraintValue);
                            case ScanConstraint.ConstraintType.LessThanOrEqual:
                                return () => this.LessThanOrEqualToValue(scanConstraint.ConstraintValue);
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