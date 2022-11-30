namespace Squalr.Engine.Scanning.Scanners.Comparers.Iterative
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A scanner that works by looping over each element of the snapshot individually. Much slower than the vectorized version.
    /// </summary>
    internal class SnapshotRegionIterativeScanner : SnapshotRegionScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionIterativeScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The constraints to use for the element comparisons.</param>
        public unsafe SnapshotRegionIterativeScanner() : base()
        {
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SnapshotRegionIterativeScanner" /> class.
        /// </summary>
        ~SnapshotRegionIterativeScanner()
        {
        }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        public Func<Boolean> ElementCompare { get; private set; }

        /// <summary>
        /// Gets or sets a garbage collector handle to the current value array.
        /// </summary>
        private GCHandle CurrentValuesHandle { get; set; }

        /// <summary>
        /// Gets or sets a garbage collector handle to the previous value array.
        /// </summary>
        private GCHandle PreviousValuesHandle { get; set; }

        /// <summary>
        /// Gets or sets the pointer to the current value.
        /// </summary>
        private unsafe Byte* CurrentValuePointer { get; set; }

        /// <summary>
        /// Gets or sets the pointer to the previous value.
        /// </summary>
        private unsafe Byte* PreviousValuePointer { get; set; }

        public override void Initialize(SnapshotRegion region, ScanConstraints constraints)
        {
            base.Initialize(region, constraints);

            // The garbage collector can relocate variables at runtime. Since we use unsafe pointers, we need to keep these pinned
            this.CurrentValuesHandle = GCHandle.Alloc(this.Region.ReadGroup.CurrentValues, GCHandleType.Pinned);
            this.PreviousValuesHandle = GCHandle.Alloc(this.Region.ReadGroup.PreviousValues, GCHandleType.Pinned);

            this.InitializePointers();
            this.ElementCompare = this.BuildCompareActions(constraints);
        }

        public override void Dispose()
        {
            // Let the GC do what it wants now
            this.CurrentValuesHandle.Free();
            this.PreviousValuesHandle.Free();
        }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public unsafe override IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints)
        {
            this.Initialize(region: region, constraints: constraints);

            Int32 alignedElementCount = region.GetAlignedElementCount(constraints.Alignment);

            for (Int32 index = 0; index < alignedElementCount; index++)
            {
                if (this.ElementCompare())
                {
                    this.RunLengthEncoder.EncodeBatch((Int32)constraints.Alignment);
                }
                else
                {
                    this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked((Int32)constraints.Alignment);
                }

                this.CurrentValuePointer += (Int32)constraints.Alignment;
                this.PreviousValuePointer += (Int32)constraints.Alignment;
            }

            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked();

            return this.RunLengthEncoder.GetCollectedRegions();
        }

        /// <summary>
        /// Sets a custom comparison function to use in scanning.
        /// </summary>
        /// <param name="customCompare"></param>
        public void SetCustomCompareAction(Func<Boolean> customCompare)
        {
            this.ElementCompare = customCompare;
        }

        /// <summary>
        /// Initializes snapshot value reference pointers
        /// </summary>
        private unsafe void InitializePointers()
        {
            if (this.Region.ReadGroup.CurrentValues != null && this.Region.ReadGroup.CurrentValues.Length > 0)
            {
                fixed (Byte* pointerBase = &this.Region.ReadGroup.CurrentValues[this.Region.ReadGroupOffset])
                {
                    this.CurrentValuePointer = pointerBase;
                }
            }
            else
            {
                this.CurrentValuePointer = null;
            }

            if (this.Region.ReadGroup.PreviousValues != null && this.Region.ReadGroup.PreviousValues.Length > 0)
            {
                fixed (Byte* pointerBase = &this.Region.ReadGroup.PreviousValues[this.Region.ReadGroupOffset])
                {
                    this.PreviousValuePointer = pointerBase;
                }
            }
            else
            {
                this.PreviousValuePointer = null;
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a changed value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonChanged()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer != *this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer != *(SByte*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer != *(Int16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer != *(Int32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer != *(Int64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer != *(UInt16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer != *(UInt32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer != *(UInt64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Single:
                    return () => !(*(Single*)this.CurrentValuePointer).AlmostEquals(*(Single*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) != BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer));
                case ScannableType type when type == ScannableType.Double:
                    return () => !(*(Double*)this.CurrentValuePointer).AlmostEquals(*(Double*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) != BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an unchanged value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonUnchanged()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer == *this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer == *(SByte*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer == *(Int16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer == *(Int32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer == *(Int64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer == *(UInt16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer == *(UInt32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer == *(UInt64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Single:
                    return () => (*(Single*)this.CurrentValuePointer).AlmostEquals(*(Single*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer));
                case ScannableType type when type == ScannableType.Double:
                    return () => (*(Double*)this.CurrentValuePointer).AlmostEquals(*(Double*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an increased value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonIncreased()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer > *this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer > *(SByte*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer > *(Int16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer > *(Int32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer > *(Int64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer > *(UInt16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer > *(UInt32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer > *(UInt64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer > *(Single*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) > BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer));
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer > *(Double*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) > BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a decreased value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonDecreased()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer < *this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer < *(SByte*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer < *(Int16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer < *(Int32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer < *(Int64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer < *(UInt16*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer < *(UInt32*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer < *(UInt64*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer);
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer < *(Single*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) < BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer));
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer < *(Double*)this.PreviousValuePointer;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) < BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer));
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an increased by value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonIncreasedBy(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer == unchecked(*this.PreviousValuePointer + (Byte)value);
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer == unchecked(*(SByte*)this.PreviousValuePointer + (SByte)value);
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer == unchecked(*(Int16*)this.PreviousValuePointer + (Int16)value);
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer) + (Int16)value);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer == unchecked(*(Int32*)this.PreviousValuePointer + (Int32)value);
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer) + (Int32)value);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer == unchecked(*(Int64*)this.PreviousValuePointer + (Int64)value);
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer) + (Int64)value);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer == unchecked(*(UInt16*)this.PreviousValuePointer + (UInt16)value);
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer) + (UInt16)value);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer == unchecked(*(UInt32*)this.PreviousValuePointer + (UInt32)value);
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer) + (UInt32)value);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer == unchecked(*(UInt64*)this.PreviousValuePointer + (UInt64)value);
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer) + (UInt64)value);
                case ScannableType type when type == ScannableType.Single:
                    return () => (*(Single*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Single*)this.PreviousValuePointer + (Single)value));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == unchecked(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)) + (Single)value);
                case ScannableType type when type == ScannableType.Double:
                    return () => (*(Double*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Double*)this.PreviousValuePointer + (Double)value));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == unchecked(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)) + (Double)value);
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a decreased by value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonDecreasedBy(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer == unchecked(*this.PreviousValuePointer - (Byte)value);
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer == unchecked(*(SByte*)this.PreviousValuePointer - (SByte)value);
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer == unchecked(*(Int16*)this.PreviousValuePointer - (Int16)value);
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer) - (Int16)value);
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer == unchecked(*(Int32*)this.PreviousValuePointer - (Int32)value);
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer) - (Int32)value);
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer == unchecked(*(Int64*)this.PreviousValuePointer - (Int64)value);
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer) - (Int64)value);
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer == unchecked(*(UInt16*)this.PreviousValuePointer - (UInt16)value);
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer) - (UInt16)value);
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer == unchecked(*(UInt32*)this.PreviousValuePointer - (UInt32)value);
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer) - (UInt32)value);
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer == unchecked(*(UInt64*)this.PreviousValuePointer - (UInt64)value);
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer) - (UInt64)value);
                case ScannableType type when type == ScannableType.Single:
                    return () => (*(Single*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Single*)this.PreviousValuePointer - (Single)value));
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == unchecked(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)) - (Single)value);
                case ScannableType type when type == ScannableType.Double:
                    return () => (*(Double*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Double*)this.PreviousValuePointer - (Double)value));
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == unchecked(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)) - (Double)value);
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for an equal to value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer == (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer == (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer == (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer == (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer == (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer == (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer == (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer == (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => (*(Single*)this.CurrentValuePointer).AlmostEquals((Single)value);
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => (*(Double*)this.CurrentValuePointer).AlmostEquals((Double)value);
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a not equal to value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonNotEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer != (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer != (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer != (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) != (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer != (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) != (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer != (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) != (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer != (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) != (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer != (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) != (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer != (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) != (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => !(*(Single*)this.CurrentValuePointer).AlmostEquals((Single)value);
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) != (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => !(*(Double*)this.CurrentValuePointer).AlmostEquals((Double)value);
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) != (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonGreaterThan(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer > (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer > (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer > (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) > (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer > (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) > (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer > (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) > (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer > (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) > (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer > (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) > (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer > (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) > (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer > (Single)value;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) > (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer > (Double)value;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) > (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than or equal to value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonGreaterThanOrEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer >= (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer >= (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer >= (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) >= (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer >= (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) >= (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer >= (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) >= (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer >= (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) >= (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer >= (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) >= (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer >= (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) >= (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer >= (Single)value;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) >= (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer >= (Double)value;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) >= (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a greater than value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonLessThan(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer < (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer < (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer < (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) < (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer < (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) < (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer < (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) < (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer < (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) < (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer < (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) < (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer < (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) < (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer < (Single)value;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) < (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer < (Double)value;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) < (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Gets the appropriate comparison function for a less than or equal to value scan.
        /// </summary>
        private unsafe Func<Boolean> GetComparisonLessThanOrEqual(Object value)
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return () => *this.CurrentValuePointer <= (Byte)value;
                case ScannableType type when type == ScannableType.SByte:
                    return () => *(SByte*)this.CurrentValuePointer <= (SByte)value;
                case ScannableType type when type == ScannableType.Int16:
                    return () => *(Int16*)this.CurrentValuePointer <= (Int16)value;
                case ScannableType type when type == ScannableType.Int16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) <= (Int16)value;
                case ScannableType type when type == ScannableType.Int32:
                    return () => *(Int32*)this.CurrentValuePointer <= (Int32)value;
                case ScannableType type when type == ScannableType.Int32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) <= (Int32)value;
                case ScannableType type when type == ScannableType.Int64:
                    return () => *(Int64*)this.CurrentValuePointer <= (Int64)value;
                case ScannableType type when type == ScannableType.Int64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) <= (Int64)value;
                case ScannableType type when type == ScannableType.UInt16:
                    return () => *(UInt16*)this.CurrentValuePointer <= (UInt16)value;
                case ScannableType type when type == ScannableType.UInt16BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) <= (UInt16)value;
                case ScannableType type when type == ScannableType.UInt32:
                    return () => *(UInt32*)this.CurrentValuePointer <= (UInt32)value;
                case ScannableType type when type == ScannableType.UInt32BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) <= (UInt32)value;
                case ScannableType type when type == ScannableType.UInt64:
                    return () => *(UInt64*)this.CurrentValuePointer <= (UInt64)value;
                case ScannableType type when type == ScannableType.UInt64BE:
                    return () => BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) <= (UInt64)value;
                case ScannableType type when type == ScannableType.Single:
                    return () => *(Single*)this.CurrentValuePointer <= (Single)value;
                case ScannableType type when type == ScannableType.SingleBE:
                    return () => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) <= (Single)value;
                case ScannableType type when type == ScannableType.Double:
                    return () => *(Double*)this.CurrentValuePointer <= (Double)value;
                case ScannableType type when type == ScannableType.DoubleBE:
                    return () => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) <= (Double)value;
                default:
                    throw new ArgumentException("Unsupported data type provided.");
            }
        }

        /// <summary>
        /// Sets the default compare action to use for this element.
        /// </summary>
        /// <param name="constraint">The constraint(s) to use for the element quick action.</param>
        private Func<Boolean> BuildCompareActions(IScanConstraint constraint)
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
                                Boolean resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();
                                Boolean resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft & resultRight;
                            };
                        case OperationConstraint.OperationType.OR:
                            return () =>
                            {
                                Boolean resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();
                                Boolean resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft | resultRight;
                            };
                        case OperationConstraint.OperationType.XOR:
                            return () =>
                            {
                                Boolean resultLeft = this.BuildCompareActions(operationConstraint.Left).Invoke();
                                Boolean resultRight = this.BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft ^ resultRight;
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
                            throw new Exception("Unknown constraint type");
                    }
                default:
                    throw new ArgumentException("Invalid constraint");
            }
        }
    }
    //// End class
}
//// End namespace
