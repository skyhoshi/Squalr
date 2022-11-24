namespace Squalr.Engine.Scanning.Scanners
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Scanning;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Defines a reference to an element within a snapshot region.
    /// </summary>
    public class SnapshotElementComparer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotElementComparer" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="pointerIncrementMode">The method by which to increment element pointers.</param>
        /// <param name="constraints">The constraints to use for the element comparisons.</param>
        public unsafe SnapshotElementComparer(SnapshotRegion region, PointerIncrementMode pointerIncrementMode, ScannableType dataType)
        {
            Region = region;
            CurrentTypeCode = Type.GetTypeCode(dataType);

            // The garbage collector can relocate variables at runtime. Since we use unsafe pointers, we need to keep these pinned
            CurrentValuesHandle = GCHandle.Alloc(Region.ReadGroup.CurrentValues, GCHandleType.Pinned);
            PreviousValuesHandle = GCHandle.Alloc(Region.ReadGroup.PreviousValues, GCHandleType.Pinned);

            InitializePointers();
            SetConstraintFunctions();
            SetPointerFunction(pointerIncrementMode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotElementComparer" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="pointerIncrementMode">The method by which to increment element pointers.</param>
        /// <param name="constraints">The constraints to use for the element comparisons.</param>
        public unsafe SnapshotElementComparer(SnapshotRegion region, PointerIncrementMode pointerIncrementMode, Constraint constraints, ScannableType dataType) : this(region, pointerIncrementMode, dataType)
        {
            ElementCompare = BuildCompareActions(constraints);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SnapshotElementComparer" /> class.
        /// </summary>
        ~SnapshotElementComparer()
        {
            // Let the GC do what it wants now
            CurrentValuesHandle.Free();
            PreviousValuesHandle.Free();
        }

        /// <summary>
        /// Gets an action to increment only the needed pointers.
        /// </summary>
        public Action IncrementPointers { get; private set; }

        /// <summary>
        /// Gets an action based on the element iterator scan constraint.
        /// </summary>
        public Func<bool> ElementCompare { get; private set; }

        /// <summary>
        /// Gets a function which determines if this element has changed.
        /// </summary>
        private Func<bool> Changed { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has not changed.
        /// </summary>
        private Func<bool> Unchanged { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has increased.
        /// </summary>
        private Func<bool> Increased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has decreased.
        /// </summary>
        private Func<bool> Decreased { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value equal to the given value.
        /// </summary>
        private Func<object, bool> EqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value not equal to the given value.
        /// </summary>
        private Func<object, bool> NotEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than to the given value.
        /// </summary>
        private Func<object, bool> GreaterThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value greater than or equal to the given value.
        /// </summary>
        private Func<object, bool> GreaterThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<object, bool> LessThanValue { get; set; }

        /// <summary>
        /// Gets a function which determines if this element has a value less than to the given value.
        /// </summary>
        private Func<object, bool> LessThanOrEqualToValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has increased it's value by the given value.
        /// </summary>
        private Func<object, bool> IncreasedByValue { get; set; }

        /// <summary>
        /// Gets a function which determines if the element has decreased it's value by the given value.
        /// </summary>
        private Func<object, bool> DecreasedByValue { get; set; }

        /// <summary>
        /// Enums determining which pointers need to be updated every iteration.
        /// </summary>
        public enum PointerIncrementMode
        {
            /// <summary>
            /// Increment all pointers.
            /// </summary>
            AllPointers,

            /// <summary>
            /// Only increment current and previous value pointers.
            /// </summary>
            ValuesOnly,

            /// <summary>
            /// Only increment label pointers.
            /// </summary>
            LabelsOnly,

            /// <summary>
            /// Only increment current value pointer.
            /// </summary>
            CurrentOnly,

            /// <summary>
            /// Increment all pointers except the previous value pointer.
            /// </summary>
            NoPrevious,
        }

        /// <summary>
        /// Gets the base address of this element.
        /// </summary>
        public ulong BaseAddress
        {
            get
            {
                return Region.BaseAddress.Add(ElementIndex);
            }
        }

        /// <summary>
        /// Gets or sets the label associated with this element.
        /// </summary>
        public object ElementLabel
        {
            get
            {
                return Region.ReadGroup.ElementLabels[CurrentLabelIndex];
            }

            set
            {
                Region.ReadGroup.ElementLabels[CurrentLabelIndex] = value;
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
        /// Gets or sets the parent snapshot region.
        /// </summary>
        private SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets the pointer to the current value.
        /// </summary>
        private unsafe byte* CurrentValuePointer { get; set; }

        /// <summary>
        /// Gets or sets the pointer to the previous value.
        /// </summary>
        private unsafe byte* PreviousValuePointer { get; set; }

        /// <summary>
        /// Gets or sets the index of this element, used for setting and getting the label.
        /// Note that we cannot have a pointer to the label, as it is a non-blittable type.
        /// </summary>
        private int CurrentLabelIndex { get; set; }

        /// <summary>
        /// Gets the index of this element.
        /// </summary>
        private unsafe int ElementIndex
        {
            get
            {
                // Use the incremented current value pointer or label index to figure out the index of this element
                if (CurrentLabelIndex != 0)
                {
                    return CurrentLabelIndex;
                }
                else if (CurrentValuePointer != null)
                {
                    fixed (byte* pointerBase = &Region.ReadGroup.CurrentValues[Region.ReadGroupOffset])
                    {
                        return (int)(CurrentValuePointer - pointerBase);
                    }
                }
                else if (PreviousValuePointer != null)
                {
                    fixed (byte* pointerBase = &Region.ReadGroup.PreviousValues[Region.ReadGroupOffset])
                    {
                        return (int)(PreviousValuePointer - pointerBase);
                    }
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the type code associated with the data type of this element.
        /// </summary>
        private TypeCode CurrentTypeCode { get; set; }

        /// <summary>
        /// Sets a custom comparison function to use in scanning.
        /// </summary>
        /// <param name="customCompare"></param>
        public void SetCustomCompareAction(Func<bool> customCompare)
        {
            ElementCompare = customCompare;
        }

        /// <summary>
        /// Gets the current value of this element.
        /// </summary>
        /// <returns>The current value of this element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe object GetCurrentValue()
        {
            return LoadValue(CurrentValuePointer);
        }

        /// <summary>
        /// Gets the previous value of this element.
        /// </summary>
        /// <returns>The previous value of this element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe object GetPreviousValue()
        {
            return LoadValue(PreviousValuePointer);
        }

        /// <summary>
        /// Gets the label of this element.
        /// </summary>
        /// <returns>The label of this element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe object GetElementLabel()
        {
            return Region.ReadGroup.ElementLabels == null ? null : Region.ReadGroup.ElementLabels[CurrentLabelIndex];
        }

        /// <summary>
        /// Sets the label of this element.
        /// </summary>
        /// <param name="newLabel">The new element label.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetElementLabel(object newLabel)
        {
            Region.ReadGroup.ElementLabels[CurrentLabelIndex] = newLabel;
        }

        /// <summary>
        /// Determines if this element has a current value associated with it.
        /// </summary>
        /// <returns>True if a current value is present.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool HasCurrentValue()
        {
            if (CurrentValuePointer == (byte*)0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if this element has a previous value associated with it.
        /// </summary>
        /// <returns>True if a previous value is present.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool HasPreviousValue()
        {
            if (PreviousValuePointer == (byte*)0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Initializes snapshot value reference pointers
        /// </summary>
        private unsafe void InitializePointers()
        {
            CurrentLabelIndex = 0;

            if (Region.ReadGroup.CurrentValues != null && Region.ReadGroup.CurrentValues.Length > 0)
            {
                fixed (byte* pointerBase = &Region.ReadGroup.CurrentValues[Region.ReadGroupOffset])
                {
                    CurrentValuePointer = pointerBase;
                }
            }
            else
            {
                CurrentValuePointer = null;
            }

            if (Region.ReadGroup.PreviousValues != null && Region.ReadGroup.PreviousValues.Length > 0)
            {
                fixed (byte* pointerBase = &Region.ReadGroup.PreviousValues[Region.ReadGroupOffset])
                {
                    PreviousValuePointer = pointerBase;
                }
            }
            else
            {
                PreviousValuePointer = null;
            }
        }

        /// <summary>
        /// Initializes all constraint functions for value comparisons.
        /// </summary>
        private unsafe void SetConstraintFunctions()
        {
            switch (CurrentTypeCode)
            {
                case TypeCode.Byte:
                    Changed = () => { return *CurrentValuePointer != *PreviousValuePointer; };
                    Unchanged = () => { return *CurrentValuePointer == *PreviousValuePointer; };
                    Increased = () => { return *CurrentValuePointer > *PreviousValuePointer; };
                    Decreased = () => { return *CurrentValuePointer < *PreviousValuePointer; };
                    EqualToValue = (value) => { return *CurrentValuePointer == (byte)value; };
                    NotEqualToValue = (value) => { return *CurrentValuePointer != (byte)value; };
                    GreaterThanValue = (value) => { return *CurrentValuePointer > (byte)value; };
                    GreaterThanOrEqualToValue = (value) => { return *CurrentValuePointer >= (byte)value; };
                    LessThanValue = (value) => { return *CurrentValuePointer < (byte)value; };
                    LessThanOrEqualToValue = (value) => { return *CurrentValuePointer <= (byte)value; };
                    IncreasedByValue = (value) => { return *CurrentValuePointer == unchecked(*PreviousValuePointer + (byte)value); };
                    DecreasedByValue = (value) => { return *CurrentValuePointer == unchecked(*PreviousValuePointer - (byte)value); };
                    break;
                case TypeCode.SByte:
                    Changed = () => { return *(sbyte*)CurrentValuePointer != *(sbyte*)PreviousValuePointer; };
                    Unchanged = () => { return *(sbyte*)CurrentValuePointer == *(sbyte*)PreviousValuePointer; };
                    Increased = () => { return *(sbyte*)CurrentValuePointer > *(sbyte*)PreviousValuePointer; };
                    Decreased = () => { return *(sbyte*)CurrentValuePointer < *(sbyte*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(sbyte*)CurrentValuePointer == (sbyte)value; };
                    NotEqualToValue = (value) => { return *(sbyte*)CurrentValuePointer != (sbyte)value; };
                    GreaterThanValue = (value) => { return *(sbyte*)CurrentValuePointer > (sbyte)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(sbyte*)CurrentValuePointer >= (sbyte)value; };
                    LessThanValue = (value) => { return *(sbyte*)CurrentValuePointer < (sbyte)value; };
                    LessThanOrEqualToValue = (value) => { return *(sbyte*)CurrentValuePointer <= (sbyte)value; };
                    IncreasedByValue = (value) => { return *(sbyte*)CurrentValuePointer == unchecked(*(sbyte*)PreviousValuePointer + (sbyte)value); };
                    DecreasedByValue = (value) => { return *(sbyte*)CurrentValuePointer == unchecked(*(sbyte*)PreviousValuePointer - (sbyte)value); };
                    break;
                case TypeCode.Int16:
                    Changed = () => { return *(short*)CurrentValuePointer != *(short*)PreviousValuePointer; };
                    Unchanged = () => { return *(short*)CurrentValuePointer == *(short*)PreviousValuePointer; };
                    Increased = () => { return *(short*)CurrentValuePointer > *(short*)PreviousValuePointer; };
                    Decreased = () => { return *(short*)CurrentValuePointer < *(short*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(short*)CurrentValuePointer == (short)value; };
                    NotEqualToValue = (value) => { return *(short*)CurrentValuePointer != (short)value; };
                    GreaterThanValue = (value) => { return *(short*)CurrentValuePointer > (short)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(short*)CurrentValuePointer >= (short)value; };
                    LessThanValue = (value) => { return *(short*)CurrentValuePointer < (short)value; };
                    LessThanOrEqualToValue = (value) => { return *(short*)CurrentValuePointer <= (short)value; };
                    IncreasedByValue = (value) => { return *(short*)CurrentValuePointer == unchecked(*(short*)PreviousValuePointer + (short)value); };
                    DecreasedByValue = (value) => { return *(short*)CurrentValuePointer == unchecked(*(short*)PreviousValuePointer - (short)value); };
                    break;
                case TypeCode.Int32:
                    Changed = () => { return *(int*)CurrentValuePointer != *(int*)PreviousValuePointer; };
                    Unchanged = () => { return *(int*)CurrentValuePointer == *(int*)PreviousValuePointer; };
                    Increased = () => { return *(int*)CurrentValuePointer > *(int*)PreviousValuePointer; };
                    Decreased = () => { return *(int*)CurrentValuePointer < *(int*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(int*)CurrentValuePointer == (int)value; };
                    NotEqualToValue = (value) => { return *(int*)CurrentValuePointer != (int)value; };
                    GreaterThanValue = (value) => { return *(int*)CurrentValuePointer > (int)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(int*)CurrentValuePointer >= (int)value; };
                    LessThanValue = (value) => { return *(int*)CurrentValuePointer < (int)value; };
                    LessThanOrEqualToValue = (value) => { return *(int*)CurrentValuePointer <= (int)value; };
                    IncreasedByValue = (value) => { return *(int*)CurrentValuePointer == unchecked(*(int*)PreviousValuePointer + (int)value); };
                    DecreasedByValue = (value) => { return *(int*)CurrentValuePointer == unchecked(*(int*)PreviousValuePointer - (int)value); };
                    break;
                case TypeCode.Int64:
                    Changed = () => { return *(long*)CurrentValuePointer != *(long*)PreviousValuePointer; };
                    Unchanged = () => { return *(long*)CurrentValuePointer == *(long*)PreviousValuePointer; };
                    Increased = () => { return *(long*)CurrentValuePointer > *(long*)PreviousValuePointer; };
                    Decreased = () => { return *(long*)CurrentValuePointer < *(long*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(long*)CurrentValuePointer == (long)value; };
                    NotEqualToValue = (value) => { return *(long*)CurrentValuePointer != (long)value; };
                    GreaterThanValue = (value) => { return *(long*)CurrentValuePointer > (long)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(long*)CurrentValuePointer >= (long)value; };
                    LessThanValue = (value) => { return *(long*)CurrentValuePointer < (long)value; };
                    LessThanOrEqualToValue = (value) => { return *(long*)CurrentValuePointer <= (long)value; };
                    IncreasedByValue = (value) => { return *(long*)CurrentValuePointer == unchecked(*(long*)PreviousValuePointer + (long)value); };
                    DecreasedByValue = (value) => { return *(long*)CurrentValuePointer == unchecked(*(long*)PreviousValuePointer - (long)value); };
                    break;
                case TypeCode.UInt16:
                    Changed = () => { return *(ushort*)CurrentValuePointer != *(ushort*)PreviousValuePointer; };
                    Unchanged = () => { return *(ushort*)CurrentValuePointer == *(ushort*)PreviousValuePointer; };
                    Increased = () => { return *(ushort*)CurrentValuePointer > *(ushort*)PreviousValuePointer; };
                    Decreased = () => { return *(ushort*)CurrentValuePointer < *(ushort*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(ushort*)CurrentValuePointer == (ushort)value; };
                    NotEqualToValue = (value) => { return *(ushort*)CurrentValuePointer != (ushort)value; };
                    GreaterThanValue = (value) => { return *(ushort*)CurrentValuePointer > (ushort)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(ushort*)CurrentValuePointer >= (ushort)value; };
                    LessThanValue = (value) => { return *(ushort*)CurrentValuePointer < (ushort)value; };
                    LessThanOrEqualToValue = (value) => { return *(ushort*)CurrentValuePointer <= (ushort)value; };
                    IncreasedByValue = (value) => { return *(ushort*)CurrentValuePointer == unchecked(*(ushort*)PreviousValuePointer + (ushort)value); };
                    DecreasedByValue = (value) => { return *(ushort*)CurrentValuePointer == unchecked(*(ushort*)PreviousValuePointer - (ushort)value); };
                    break;
                case TypeCode.UInt32:
                    Changed = () => { return *(uint*)CurrentValuePointer != *(uint*)PreviousValuePointer; };
                    Unchanged = () => { return *(uint*)CurrentValuePointer == *(uint*)PreviousValuePointer; };
                    Increased = () => { return *(uint*)CurrentValuePointer > *(uint*)PreviousValuePointer; };
                    Decreased = () => { return *(uint*)CurrentValuePointer < *(uint*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(uint*)CurrentValuePointer == (uint)value; };
                    NotEqualToValue = (value) => { return *(uint*)CurrentValuePointer != (uint)value; };
                    GreaterThanValue = (value) => { return *(uint*)CurrentValuePointer > (uint)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(uint*)CurrentValuePointer >= (uint)value; };
                    LessThanValue = (value) => { return *(uint*)CurrentValuePointer < (uint)value; };
                    LessThanOrEqualToValue = (value) => { return *(uint*)CurrentValuePointer <= (uint)value; };
                    IncreasedByValue = (value) => { return *(uint*)CurrentValuePointer == unchecked(*(uint*)PreviousValuePointer + (uint)value); };
                    DecreasedByValue = (value) => { return *(uint*)CurrentValuePointer == unchecked(*(uint*)PreviousValuePointer - (uint)value); };
                    break;
                case TypeCode.UInt64:
                    Changed = () => { return *(ulong*)CurrentValuePointer != *(ulong*)PreviousValuePointer; };
                    Unchanged = () => { return *(ulong*)CurrentValuePointer == *(ulong*)PreviousValuePointer; };
                    Increased = () => { return *(ulong*)CurrentValuePointer > *(ulong*)PreviousValuePointer; };
                    Decreased = () => { return *(ulong*)CurrentValuePointer < *(ulong*)PreviousValuePointer; };
                    EqualToValue = (value) => { return *(ulong*)CurrentValuePointer == (ulong)value; };
                    NotEqualToValue = (value) => { return *(ulong*)CurrentValuePointer != (ulong)value; };
                    GreaterThanValue = (value) => { return *(ulong*)CurrentValuePointer > (ulong)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(ulong*)CurrentValuePointer >= (ulong)value; };
                    LessThanValue = (value) => { return *(ulong*)CurrentValuePointer < (ulong)value; };
                    LessThanOrEqualToValue = (value) => { return *(ulong*)CurrentValuePointer <= (ulong)value; };
                    IncreasedByValue = (value) => { return *(ulong*)CurrentValuePointer == unchecked(*(ulong*)PreviousValuePointer + (ulong)value); };
                    DecreasedByValue = (value) => { return *(ulong*)CurrentValuePointer == unchecked(*(ulong*)PreviousValuePointer - (ulong)value); };
                    break;
                case TypeCode.Single:
                    Changed = () => { return !(*(float*)CurrentValuePointer).AlmostEquals(*(float*)PreviousValuePointer); };
                    Unchanged = () => { return (*(float*)CurrentValuePointer).AlmostEquals(*(float*)PreviousValuePointer); };
                    Increased = () => { return *(float*)CurrentValuePointer > *(float*)PreviousValuePointer; };
                    Decreased = () => { return *(float*)CurrentValuePointer < *(float*)PreviousValuePointer; };
                    EqualToValue = (value) => { return (*(float*)CurrentValuePointer).AlmostEquals((float)value); };
                    NotEqualToValue = (value) => { return !(*(float*)CurrentValuePointer).AlmostEquals((float)value); };
                    GreaterThanValue = (value) => { return *(float*)CurrentValuePointer > (float)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(float*)CurrentValuePointer >= (float)value; };
                    LessThanValue = (value) => { return *(float*)CurrentValuePointer < (float)value; };
                    LessThanOrEqualToValue = (value) => { return *(float*)CurrentValuePointer <= (float)value; };
                    IncreasedByValue = (value) => { return (*(float*)CurrentValuePointer).AlmostEquals(unchecked(*(float*)PreviousValuePointer + (float)value)); };
                    DecreasedByValue = (value) => { return (*(float*)CurrentValuePointer).AlmostEquals(unchecked(*(float*)PreviousValuePointer - (float)value)); };
                    break;
                case TypeCode.Double:
                    Changed = () => { return !(*(double*)CurrentValuePointer).AlmostEquals(*(double*)PreviousValuePointer); };
                    Unchanged = () => { return (*(double*)CurrentValuePointer).AlmostEquals(*(double*)PreviousValuePointer); };
                    Increased = () => { return *(double*)CurrentValuePointer > *(double*)PreviousValuePointer; };
                    Decreased = () => { return *(double*)CurrentValuePointer < *(double*)PreviousValuePointer; };
                    EqualToValue = (value) => { return (*(double*)CurrentValuePointer).AlmostEquals((double)value); };
                    NotEqualToValue = (value) => { return !(*(double*)CurrentValuePointer).AlmostEquals((double)value); };
                    GreaterThanValue = (value) => { return *(double*)CurrentValuePointer > (double)value; };
                    GreaterThanOrEqualToValue = (value) => { return *(double*)CurrentValuePointer >= (double)value; };
                    LessThanValue = (value) => { return *(double*)CurrentValuePointer < (double)value; };
                    LessThanOrEqualToValue = (value) => { return *(double*)CurrentValuePointer <= (double)value; };
                    IncreasedByValue = (value) => { return (*(double*)CurrentValuePointer).AlmostEquals(unchecked(*(double*)PreviousValuePointer + (double)value)); };
                    DecreasedByValue = (value) => { return (*(double*)CurrentValuePointer).AlmostEquals(unchecked(*(double*)PreviousValuePointer - (double)value)); };
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        /// <summary>
        /// Initializes the pointer incrementing function based on the provided parameters.
        /// </summary>
        /// <param name="pointerIncrementMode">The method by which to increment pointers.</param>
        private unsafe void SetPointerFunction(PointerIncrementMode pointerIncrementMode)
        {
            int alignment = ScanSettings.Alignment;

            if (alignment == 1)
            {
                switch (pointerIncrementMode)
                {
                    case PointerIncrementMode.AllPointers:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex++;
                            CurrentValuePointer++;
                            PreviousValuePointer++;
                        };
                        break;
                    case PointerIncrementMode.CurrentOnly:
                        IncrementPointers = () =>
                        {
                            CurrentValuePointer++;
                        };
                        break;
                    case PointerIncrementMode.LabelsOnly:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex++;
                        };
                        break;
                    case PointerIncrementMode.NoPrevious:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex++;
                            CurrentValuePointer++;
                        };
                        break;
                    case PointerIncrementMode.ValuesOnly:
                        IncrementPointers = () =>
                        {
                            CurrentValuePointer++;
                            PreviousValuePointer++;
                        };
                        break;
                }
            }
            else
            {
                switch (pointerIncrementMode)
                {
                    case PointerIncrementMode.AllPointers:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex += alignment;
                            CurrentValuePointer += alignment;
                            PreviousValuePointer += alignment;
                        };
                        break;
                    case PointerIncrementMode.CurrentOnly:
                        IncrementPointers = () =>
                        {
                            CurrentValuePointer += alignment;
                        };
                        break;
                    case PointerIncrementMode.LabelsOnly:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex += alignment;
                        };
                        break;
                    case PointerIncrementMode.NoPrevious:
                        IncrementPointers = () =>
                        {
                            CurrentLabelIndex += alignment;
                            CurrentValuePointer += alignment;
                        };
                        break;
                    case PointerIncrementMode.ValuesOnly:
                        IncrementPointers = () =>
                        {
                            CurrentValuePointer += alignment;
                            PreviousValuePointer += alignment;
                        };
                        break;
                }
            }
        }

        /// <summary>
        /// Sets the default compare action to use for this element.
        /// </summary>
        /// <param name="constraint">The constraint(s) to use for the element quick action.</param>
        private Func<bool> BuildCompareActions(Constraint constraint)
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
                                bool resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();
                                bool resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft & resultRight;
                            };
                        case OperationConstraint.OperationType.OR:
                            return () =>
                            {
                                bool resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();
                                bool resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft | resultRight;
                            };
                        case OperationConstraint.OperationType.XOR:
                            return () =>
                            {
                                bool resultLeft = BuildCompareActions(operationConstraint.Left).Invoke();
                                bool resultRight = BuildCompareActions(operationConstraint.Right).Invoke();

                                return resultLeft ^ resultRight;
                            };
                        default:
                            throw new ArgumentException("Unkown operation type");
                    }
                case ScanConstraint scanConstraint:
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
                            throw new Exception("Unknown constraint type");
                    }
                default:
                    throw new ArgumentException("Invalid constraint");
            }
        }

        /// <summary>
        /// Loads the value of this snapshot element from the given array.
        /// </summary>
        /// <param name="array">The byte array from which to read a value.</param>
        /// <returns>The value at the start of this array casted as the proper data type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe object LoadValue(byte* array)
        {
            switch (CurrentTypeCode)
            {
                case TypeCode.Byte:
                    return *array;
                case TypeCode.SByte:
                    return *(sbyte*)array;
                case TypeCode.Int16:
                    return *(short*)array;
                case TypeCode.Int32:
                    return *(int*)array;
                case TypeCode.Int64:
                    return *(long*)array;
                case TypeCode.UInt16:
                    return *(ushort*)array;
                case TypeCode.UInt32:
                    return *(uint*)array;
                case TypeCode.UInt64:
                    return *(ulong*)array;
                case TypeCode.Single:
                    return *(float*)array;
                case TypeCode.Double:
                    return *(double*)array;
                default:
                    throw new ArgumentException();
            }
        }
    }
    //// End class
}
//// End namespace