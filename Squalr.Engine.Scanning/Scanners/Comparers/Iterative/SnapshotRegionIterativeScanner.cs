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
        public unsafe SnapshotRegionIterativeScanner(SnapshotRegion region, ScanConstraints constraints) : base(region, constraints)
        {
            // The garbage collector can relocate variables at runtime. Since we use unsafe pointers, we need to keep these pinned
            this.CurrentValuesHandle = GCHandle.Alloc(this.Region.ReadGroup.CurrentValues, GCHandleType.Pinned);
            this.PreviousValuesHandle = GCHandle.Alloc(this.Region.ReadGroup.PreviousValues, GCHandleType.Pinned);

            this.InitializePointers();
            this.SetConstraintFunctions();
            this.ElementCompare = this.BuildCompareActions(constraints);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SnapshotRegionIterativeScanner" /> class.
        /// </summary>
        ~SnapshotRegionIterativeScanner()
        {
            // Let the GC do what it wants now
            this.CurrentValuesHandle.Free();
            this.PreviousValuesHandle.Free();
        }

        /// <summary>
        /// Gets an action to increment only the needed pointers.
        /// </summary>
        public Action IncrementPointers { get; private set; }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        public Func<Boolean> ElementCompare { get; private set; }

        /// <summary>
        /// Gets a function which determines if this element has changed.
        /// </summary>
        private Func<Boolean> Changed { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has not changed.
        /// </summary>
        private Func<Boolean> Unchanged { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has increased.
        /// </summary>
        private Func<Boolean> Increased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has decreased.
        /// </summary>
        private Func<Boolean> Decreased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value equal to the given value.
        /// </summary>
        private Func<Object, Boolean> EqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value not equal to the given value.
        /// </summary>
        private Func<Object, Boolean> NotEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than to the given value.
        /// </summary>
        private Func<Object, Boolean> GreaterThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than or equal to the given value.
        /// </summary>
        private Func<Object, Boolean> GreaterThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<Object, Boolean> LessThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<Object, Boolean> LessThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has increased it's value by the given value.
        /// </summary>
        private Func<Object, Boolean> IncreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has decreased it's value by the given value.
        /// </summary>
        private Func<Object, Boolean> DecreasedByValue { get; set; }

        /// <summary>
        /// Gets the base address of this element.
        /// </summary>
        public UInt64 BaseAddress
        {
            get
            {
                return this.Region.BaseElementAddress.Add(this.ElementIndex);
            }
        }

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

        /// <summary>
        /// Gets the index of this element.
        /// </summary>
        private unsafe Int32 ElementIndex
        {
            get
            {
                if (this.CurrentValuePointer != null)
                {
                    fixed (Byte* pointerBase = &this.Region.ReadGroup.CurrentValues[this.Region.ReadGroupOffset])
                    {
                        return (Int32)(this.CurrentValuePointer - pointerBase);
                    }
                }
                else if (this.PreviousValuePointer != null)
                {
                    fixed (Byte* pointerBase = &this.Region.ReadGroup.PreviousValues[this.Region.ReadGroupOffset])
                    {
                        return (Int32)(this.PreviousValuePointer - pointerBase);
                    }
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public override IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints)
        {
            if (this.ElementCompare())
            {
                this.RunLengthEncoder.EncodeOne();
            }
            else
            {
                this.RunLengthEncoder.FinalizeCurrentEncode(0);
            }

            throw new NotImplementedException();
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
        /// Initializes all constraint functions for value comparisons.
        /// </summary>
        private unsafe void SetConstraintFunctions()
        {
            switch (this.DataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    this.Changed = () => { return *this.CurrentValuePointer != *this.PreviousValuePointer; };
                    this.Unchanged = () => { return *this.CurrentValuePointer == *this.PreviousValuePointer; };
                    this.Increased = () => { return *this.CurrentValuePointer > *this.PreviousValuePointer; };
                    this.Decreased = () => { return *this.CurrentValuePointer < *this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *this.CurrentValuePointer == (Byte)value; };
                    this.NotEqualToValue = (value) => { return *this.CurrentValuePointer != (Byte)value; };
                    this.GreaterThanValue = (value) => { return *this.CurrentValuePointer > (Byte)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *this.CurrentValuePointer >= (Byte)value; };
                    this.LessThanValue = (value) => { return *this.CurrentValuePointer < (Byte)value; };
                    this.LessThanOrEqualToValue = (value) => { return *this.CurrentValuePointer <= (Byte)value; };
                    this.IncreasedByValue = (value) => { return *this.CurrentValuePointer == unchecked(*this.PreviousValuePointer + (Byte)value); };
                    this.DecreasedByValue = (value) => { return *this.CurrentValuePointer == unchecked(*this.PreviousValuePointer - (Byte)value); };
                    break;
                case ScannableType type when type == ScannableType.SByte:
                    this.Changed = () => { return *(SByte*)this.CurrentValuePointer != *(SByte*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(SByte*)this.CurrentValuePointer == *(SByte*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(SByte*)this.CurrentValuePointer > *(SByte*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(SByte*)this.CurrentValuePointer < *(SByte*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(SByte*)this.CurrentValuePointer == (SByte)value; };
                    this.NotEqualToValue = (value) => { return *(SByte*)this.CurrentValuePointer != (SByte)value; };
                    this.GreaterThanValue = (value) => { return *(SByte*)this.CurrentValuePointer > (SByte)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(SByte*)this.CurrentValuePointer >= (SByte)value; };
                    this.LessThanValue = (value) => { return *(SByte*)this.CurrentValuePointer < (SByte)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(SByte*)this.CurrentValuePointer <= (SByte)value; };
                    this.IncreasedByValue = (value) => { return *(SByte*)this.CurrentValuePointer == unchecked(*(SByte*)this.PreviousValuePointer + (SByte)value); };
                    this.DecreasedByValue = (value) => { return *(SByte*)this.CurrentValuePointer == unchecked(*(SByte*)this.PreviousValuePointer - (SByte)value); };
                    break;
                case ScannableType type when type == ScannableType.Int16:
                    this.Changed = () => { return *(Int16*)this.CurrentValuePointer != *(Int16*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(Int16*)this.CurrentValuePointer == *(Int16*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(Int16*)this.CurrentValuePointer > *(Int16*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(Int16*)this.CurrentValuePointer < *(Int16*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(Int16*)this.CurrentValuePointer == (Int16)value; };
                    this.NotEqualToValue = (value) => { return *(Int16*)this.CurrentValuePointer != (Int16)value; };
                    this.GreaterThanValue = (value) => { return *(Int16*)this.CurrentValuePointer > (Int16)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(Int16*)this.CurrentValuePointer >= (Int16)value; };
                    this.LessThanValue = (value) => { return *(Int16*)this.CurrentValuePointer < (Int16)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(Int16*)this.CurrentValuePointer <= (Int16)value; };
                    this.IncreasedByValue = (value) => { return *(Int16*)this.CurrentValuePointer == unchecked(*(Int16*)this.PreviousValuePointer + (Int16)value); };
                    this.DecreasedByValue = (value) => { return *(Int16*)this.CurrentValuePointer == unchecked(*(Int16*)this.PreviousValuePointer - (Int16)value); };
                    break;
                case ScannableType type when type == ScannableType.Int16BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == (Int16)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) != (Int16)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) > (Int16)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) >= (Int16)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) < (Int16)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) <= (Int16)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer) + (Int16)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int16*)this.PreviousValuePointer) - (Int16)value); };
                    break;
                case ScannableType type when type == ScannableType.Int32:
                    this.Changed = () => { return *(Int32*)this.CurrentValuePointer != *(Int32*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(Int32*)this.CurrentValuePointer == *(Int32*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(Int32*)this.CurrentValuePointer > *(Int32*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(Int32*)this.CurrentValuePointer < *(Int32*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(Int32*)this.CurrentValuePointer == (Int32)value; };
                    this.NotEqualToValue = (value) => { return *(Int32*)this.CurrentValuePointer != (Int32)value; };
                    this.GreaterThanValue = (value) => { return *(Int32*)this.CurrentValuePointer > (Int32)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(Int32*)this.CurrentValuePointer >= (Int32)value; };
                    this.LessThanValue = (value) => { return *(Int32*)this.CurrentValuePointer < (Int32)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(Int32*)this.CurrentValuePointer <= (Int32)value; };
                    this.IncreasedByValue = (value) => { return *(Int32*)this.CurrentValuePointer == unchecked(*(Int32*)this.PreviousValuePointer + (Int32)value); };
                    this.DecreasedByValue = (value) => { return *(Int32*)this.CurrentValuePointer == unchecked(*(Int32*)this.PreviousValuePointer - (Int32)value); };
                    break;
                case ScannableType type when type == ScannableType.Int32BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == (Int32)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) != (Int32)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) > (Int32)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) >= (Int32)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) < (Int32)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) <= (Int32)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer) + (Int32)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer) - (Int32)value); };
                    break;
                case ScannableType type when type == ScannableType.Int64:
                    this.Changed = () => { return *(Int64*)this.CurrentValuePointer != *(Int64*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(Int64*)this.CurrentValuePointer == *(Int64*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(Int64*)this.CurrentValuePointer > *(Int64*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(Int64*)this.CurrentValuePointer < *(Int64*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(Int64*)this.CurrentValuePointer == (Int64)value; };
                    this.NotEqualToValue = (value) => { return *(Int64*)this.CurrentValuePointer != (Int64)value; };
                    this.GreaterThanValue = (value) => { return *(Int64*)this.CurrentValuePointer > (Int64)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(Int64*)this.CurrentValuePointer >= (Int64)value; };
                    this.LessThanValue = (value) => { return *(Int64*)this.CurrentValuePointer < (Int64)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(Int64*)this.CurrentValuePointer <= (Int64)value; };
                    this.IncreasedByValue = (value) => { return *(Int64*)this.CurrentValuePointer == unchecked(*(Int64*)this.PreviousValuePointer + (Int64)value); };
                    this.DecreasedByValue = (value) => { return *(Int64*)this.CurrentValuePointer == unchecked(*(Int64*)this.PreviousValuePointer - (Int64)value); };
                    break;
                case ScannableType type when type == ScannableType.Int64BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == (Int64)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) != (Int64)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) > (Int64)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) >= (Int64)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) < (Int64)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) <= (Int64)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer) + (Int64)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer) - (Int64)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt16:
                    this.Changed = () => { return *(UInt16*)this.CurrentValuePointer != *(UInt16*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(UInt16*)this.CurrentValuePointer == *(UInt16*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(UInt16*)this.CurrentValuePointer > *(UInt16*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(UInt16*)this.CurrentValuePointer < *(UInt16*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(UInt16*)this.CurrentValuePointer == (UInt16)value; };
                    this.NotEqualToValue = (value) => { return *(UInt16*)this.CurrentValuePointer != (UInt16)value; };
                    this.GreaterThanValue = (value) => { return *(UInt16*)this.CurrentValuePointer > (UInt16)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(UInt16*)this.CurrentValuePointer >= (UInt16)value; };
                    this.LessThanValue = (value) => { return *(UInt16*)this.CurrentValuePointer < (UInt16)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(UInt16*)this.CurrentValuePointer <= (UInt16)value; };
                    this.IncreasedByValue = (value) => { return *(UInt16*)this.CurrentValuePointer == unchecked(*(UInt16*)this.PreviousValuePointer + (UInt16)value); };
                    this.DecreasedByValue = (value) => { return *(UInt16*)this.CurrentValuePointer == unchecked(*(UInt16*)this.PreviousValuePointer - (UInt16)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt16BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == (UInt16)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) != (UInt16)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) > (UInt16)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) >= (UInt16)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) < (UInt16)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) <= (UInt16)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer) + (UInt16)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt16*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt16*)this.PreviousValuePointer) - (UInt16)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt32:
                    this.Changed = () => { return *(UInt32*)this.CurrentValuePointer != *(UInt32*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(UInt32*)this.CurrentValuePointer == *(UInt32*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(UInt32*)this.CurrentValuePointer > *(UInt32*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(UInt32*)this.CurrentValuePointer < *(UInt32*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(UInt32*)this.CurrentValuePointer == (UInt32)value; };
                    this.NotEqualToValue = (value) => { return *(UInt32*)this.CurrentValuePointer != (UInt32)value; };
                    this.GreaterThanValue = (value) => { return *(UInt32*)this.CurrentValuePointer > (UInt32)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(UInt32*)this.CurrentValuePointer >= (UInt32)value; };
                    this.LessThanValue = (value) => { return *(UInt32*)this.CurrentValuePointer < (UInt32)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(UInt32*)this.CurrentValuePointer <= (UInt32)value; };
                    this.IncreasedByValue = (value) => { return *(UInt32*)this.CurrentValuePointer == unchecked(*(UInt32*)this.PreviousValuePointer + (UInt32)value); };
                    this.DecreasedByValue = (value) => { return *(UInt32*)this.CurrentValuePointer == unchecked(*(UInt32*)this.PreviousValuePointer - (UInt32)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt32BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == (UInt32)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) != (UInt32)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) > (UInt32)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) >= (UInt32)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) < (UInt32)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) <= (UInt32)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer) + (UInt32)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt32*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt32*)this.PreviousValuePointer) - (UInt32)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt64:
                    this.Changed = () => { return *(UInt64*)this.CurrentValuePointer != *(UInt64*)this.PreviousValuePointer; };
                    this.Unchanged = () => { return *(UInt64*)this.CurrentValuePointer == *(UInt64*)this.PreviousValuePointer; };
                    this.Increased = () => { return *(UInt64*)this.CurrentValuePointer > *(UInt64*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(UInt64*)this.CurrentValuePointer < *(UInt64*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return *(UInt64*)this.CurrentValuePointer == (UInt64)value; };
                    this.NotEqualToValue = (value) => { return *(UInt64*)this.CurrentValuePointer != (UInt64)value; };
                    this.GreaterThanValue = (value) => { return *(UInt64*)this.CurrentValuePointer > (UInt64)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(UInt64*)this.CurrentValuePointer >= (UInt64)value; };
                    this.LessThanValue = (value) => { return *(UInt64*)this.CurrentValuePointer < (UInt64)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(UInt64*)this.CurrentValuePointer <= (UInt64)value; };
                    this.IncreasedByValue = (value) => { return *(UInt64*)this.CurrentValuePointer == unchecked(*(UInt64*)this.PreviousValuePointer + (UInt64)value); };
                    this.DecreasedByValue = (value) => { return *(UInt64*)this.CurrentValuePointer == unchecked(*(UInt64*)this.PreviousValuePointer - (UInt64)value); };
                    break;
                case ScannableType type when type == ScannableType.UInt64BE:
                    this.Changed = () => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) != BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer); };
                    this.Increased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) > BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer); };
                    this.Decreased = () => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) < BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer); };
                    this.EqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == (UInt64)value; };
                    this.NotEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) != (UInt64)value; };
                    this.GreaterThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) > (UInt64)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) >= (UInt64)value; };
                    this.LessThanValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) < (UInt64)value; };
                    this.LessThanOrEqualToValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) <= (UInt64)value; };
                    this.IncreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer) + (UInt64)value); };
                    this.DecreasedByValue = (value) => { return BinaryPrimitives.ReverseEndianness(*(UInt64*)this.CurrentValuePointer) == unchecked(BinaryPrimitives.ReverseEndianness(*(UInt64*)this.PreviousValuePointer) - (UInt64)value); };
                    break;
                case ScannableType type when type == ScannableType.Single:
                    this.Changed = () => { return !(*(Single*)this.CurrentValuePointer).AlmostEquals(*(Single*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return (*(Single*)this.CurrentValuePointer).AlmostEquals(*(Single*)this.PreviousValuePointer); };
                    this.Increased = () => { return *(Single*)this.CurrentValuePointer > *(Single*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(Single*)this.CurrentValuePointer < *(Single*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return (*(Single*)this.CurrentValuePointer).AlmostEquals((Single)value); };
                    this.NotEqualToValue = (value) => { return !(*(Single*)this.CurrentValuePointer).AlmostEquals((Single)value); };
                    this.GreaterThanValue = (value) => { return *(Single*)this.CurrentValuePointer > (Single)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(Single*)this.CurrentValuePointer >= (Single)value; };
                    this.LessThanValue = (value) => { return *(Single*)this.CurrentValuePointer < (Single)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(Single*)this.CurrentValuePointer <= (Single)value; };
                    this.IncreasedByValue = (value) => { return (*(Single*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Single*)this.PreviousValuePointer + (Single)value)); };
                    this.DecreasedByValue = (value) => { return (*(Single*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Single*)this.PreviousValuePointer - (Single)value)); };
                    break;
                case ScannableType type when type == ScannableType.SingleBE:
                    this.Changed = () => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) != BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)); };
                    this.Unchanged = () => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)); };
                    this.Increased = () => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) > BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)); };
                    this.Decreased = () => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) < BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)); };
                    this.EqualToValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == (Single)value; };
                    this.NotEqualToValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) != (Single)value; };
                    this.GreaterThanValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) > (Single)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) >= (Single)value; };
                    this.LessThanValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) < (Single)value; };
                    this.LessThanOrEqualToValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) <= (Single)value; };
                    this.IncreasedByValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == unchecked(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)) + (Single)value); };
                    this.DecreasedByValue = (value) => { return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.CurrentValuePointer)) == unchecked(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(*(Int32*)this.PreviousValuePointer)) - (Single)value); };
                    break;
                case ScannableType type when type == ScannableType.Double:
                    this.Changed = () => { return !(*(Double*)this.CurrentValuePointer).AlmostEquals(*(Double*)this.PreviousValuePointer); };
                    this.Unchanged = () => { return (*(Double*)this.CurrentValuePointer).AlmostEquals(*(Double*)this.PreviousValuePointer); };
                    this.Increased = () => { return *(Double*)this.CurrentValuePointer > *(Double*)this.PreviousValuePointer; };
                    this.Decreased = () => { return *(Double*)this.CurrentValuePointer < *(Double*)this.PreviousValuePointer; };
                    this.EqualToValue = (value) => { return (*(Double*)this.CurrentValuePointer).AlmostEquals((Double)value); };
                    this.NotEqualToValue = (value) => { return !(*(Double*)this.CurrentValuePointer).AlmostEquals((Double)value); };
                    this.GreaterThanValue = (value) => { return *(Double*)this.CurrentValuePointer > (Double)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return *(Double*)this.CurrentValuePointer >= (Double)value; };
                    this.LessThanValue = (value) => { return *(Double*)this.CurrentValuePointer < (Double)value; };
                    this.LessThanOrEqualToValue = (value) => { return *(Double*)this.CurrentValuePointer <= (Double)value; };
                    this.IncreasedByValue = (value) => { return (*(Double*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Double*)this.PreviousValuePointer + (Double)value)); };
                    this.DecreasedByValue = (value) => { return (*(Double*)this.CurrentValuePointer).AlmostEquals(unchecked(*(Double*)this.PreviousValuePointer - (Double)value)); };
                    break;
                case ScannableType type when type == ScannableType.DoubleBE:
                    this.Changed = () => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) != BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)); };
                    this.Unchanged = () => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)); };
                    this.Increased = () => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) > BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)); };
                    this.Decreased = () => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) < BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)); };
                    this.EqualToValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == (Double)value; };
                    this.NotEqualToValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) != (Double)value; };
                    this.GreaterThanValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) > (Double)value; };
                    this.GreaterThanOrEqualToValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) >= (Double)value; };
                    this.LessThanValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) < (Double)value; };
                    this.LessThanOrEqualToValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) <= (Double)value; };
                    this.IncreasedByValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == unchecked(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)) + (Double)value); };
                    this.DecreasedByValue = (value) => { return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.CurrentValuePointer)) == unchecked(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(*(Int64*)this.PreviousValuePointer)) - (Double)value); };
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        /// <summary>
        /// Sets the default compare action to use for this element.
        /// </summary>
        /// <param name="constraint">The constraint(s) to use for the element quick action.</param>
        private Func<Boolean> BuildCompareActions(Constraint constraint)
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
