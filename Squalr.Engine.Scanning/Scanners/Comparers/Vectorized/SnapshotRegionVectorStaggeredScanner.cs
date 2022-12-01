namespace Squalr.Engine.Scanning.Scanners.Comparers.Vectorized
{
    using Squalr.Engine.Common.Hardware;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A vector scanner implementation that can handle staggered (alignment less than data type size) vector scans.
    /// </summary>
    internal unsafe class SnapshotRegionVectorStaggeredScanner : SnapshotRegionVectorScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionVectorStaggeredScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionVectorStaggeredScanner() : base()
        {
        }

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
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public override IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints)
        {
            this.Initialize(region: region, constraints: constraints);

            // This algorithm works as such:
            // 1) Load a hardware vector of the data type to scan (say 128 bytes => 16 doubles).
            // 2) Simultaneously scan all 16 doubles (scan result will be true/false).
            // 3) Store the results in a run length encoding (RLE) vector. Important: this RLE vector is (temporarily) a lie.
            //      In this case 1-byte alignment would mean there are 7 additional doubles between each of the doubles we just scanned!
            // 4) For this reason, we maintain an RLE vector and populate the "in-between" values for any alignments.
            //      ie we may have a RLE vector of < 1111000, 00001111 ... >, which would indicate 4 consecutive successes, 8 consecutive fails, and 4 consecutive successes.
            // 5) Process the RLE vector to update our RunLength variable, and encode any regions as they complete.

            // This algorithm has three stages:
            // 1) Scan the first vector of memory, which may contain elements we do not care about. ie <x, x, x, x ... y, y, y, y>,
            //      where x is data outside the snapshot region (but within the readgroup), and y is within the region we are scanning.
            //      to solve this, we mask out the x values such that these will always be considered false by our scan
            // 2) Scan the middle parts of. These will all fit perfectly into vectors
            // 3) Scan the final vector, if it exists. This may spill outside of the snapshot region (but within the readgroup).
            //      This works exactly like the first scan, but reversed. ie <y, y, y, y, ... x, x, x, x>, where x values are masked to be false.
            //      Note: This mask may also be applied to the first scan, if it is also the last scan (ie only 1 scan total for this region).

            Int32 elementsPerVector = Vectors.VectorSize / this.DataTypeSize;
            Int32 scanCountPerVector = unchecked(Vectors.VectorSize / (Int32)this.Alignment);
            Int32 scanCount = this.Region.Range / Vectors.VectorSize + (this.VectorOverread > 0 ? 1 : 0);

            Vector<Byte> misalignmentMask = this.BuildVectorMisalignmentMask();
            Vector<Byte> overreadMask = this.BuildVectorOverreadMask();

            Span<Vector<Byte>> staggeredScanResults = stackalloc Vector<Byte>[scanCountPerVector];

            for (this.AlignmentReadOffset = 0; this.AlignmentReadOffset < scanCountPerVector; this.AlignmentReadOffset++)
            {
                staggeredScanResults[this.AlignmentReadOffset] = VectorCompare();
            }

            // Perform the first scan (there should always be at least one). Apply the misalignment mask, and optionally the overread mask if this is also the finals scan.
            Vector<Byte> scanResults = Vector.BitwiseAnd(Vector.BitwiseAnd(misalignmentMask, this.VectorCompare()), scanCount == 1 ? overreadMask : Vectors.AllBits);
            // this.EncodeScanResults(ref scanResults);
            this.VectorReadOffset += Vectors.VectorSize;







            /*
            Int32 scanCountPerVector = this.DataTypeSize / unchecked((Int32)this.Alignment);
            Vector<Byte> allEqualsVector = new Vector<Byte>(unchecked((Byte)(1 << unchecked((Byte)scanCountPerVector) - 1)));
            Vector<Byte> runLengthVector = Vector<Byte>.Zero;

            Int32 scanCount = this.Region.Range / Vectors.VectorSize + (this.VectorOverread > 0 ? 1 : 0);
            Vector<Byte> misalignmentMask = this.BuildVectorMisalignmentMask();
            Vector<Byte> overreadMask = this.BuildVectorOverreadMask();

            // Perform the first scan (there should always be at least one). Apply the misalignment mask, and optionally the overread mask if this is also the finals scan.
            {
                for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                {
                    Vector<Byte> scanResults = this.VectorCompare();

                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(AlignmentMaskTable[alignmentIndex], scanResults));
                }

                runLengthVector = Vector.BitwiseAnd(Vector.BitwiseAnd(misalignmentMask, runLengthVector), scanCount == 1 ? overreadMask : Vector<Byte>.One);
                this.EncodeScanResults(ref runLengthVector, ref allEqualsVector, scanCountPerVector);
                this.VectorReadOffset += Vectors.VectorSize;
            }

            // Perform middle scans
            for (; this.VectorReadOffset < this.Region.Range - Vectors.VectorSize; this.VectorReadOffset += Vectors.VectorSize)
            {
                runLengthVector = Vector<Byte>.Zero;

                for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                {
                    Vector<Byte> scanResults = this.VectorCompare();

                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(AlignmentMaskTable[alignmentIndex], scanResults));
                }

                this.EncodeScanResults(ref runLengthVector, ref allEqualsVector, scanCountPerVector);
            }

            // Perform final scan, applying the overread mask if applicable.
            if (scanCount > 1)
            {
                for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                {
                    Vector<Byte> scanResults = this.VectorCompare();

                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(AlignmentMaskTable[alignmentIndex], scanResults));
                }

                runLengthVector = Vector.BitwiseAnd(overreadMask, runLengthVector);
                this.EncodeScanResults(ref runLengthVector, ref allEqualsVector, scanCountPerVector);
                this.VectorReadOffset += Vectors.VectorSize;
            }*/

            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked();

            return this.RunLengthEncoder.GetCollectedRegions();
        }

        /// <summary>
        /// Run-length encodes the given scan results into snapshot regions.
        /// </summary>
        /// <param name="scanResults">The scan results to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncodeScanResults(ref Vector<Byte> runLengthVector, ref Vector<Byte> allEqualsVector, Int32 scanCountPerVector)
        {
            // Optimization: check all vector results true
            if (Vector.EqualsAll(runLengthVector, allEqualsVector))
            {
                this.RunLengthEncoder.EncodeRange(Vectors.VectorSize);
            }
            // Optimization: check all vector results false
            else if (Vector.EqualsAll(runLengthVector, Vector<Byte>.Zero))
            {
                this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(Vectors.VectorSize);
            }
            // Otherwise the vector contains a mixture of true and false
            else
            {
                for (Int32 resultIndex = 0; resultIndex < Vectors.VectorSize; resultIndex += this.DataTypeSize)
                {
                    Byte runLengthFlags = runLengthVector[resultIndex];

                    for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                    {
                        Boolean runLengthResult = (runLengthFlags & unchecked((Byte)(1 << alignmentIndex))) != 0;

                        if (runLengthResult)
                        {
                            this.RunLengthEncoder.EncodeRange(this.DataTypeSize / scanCountPerVector);
                        }
                        else
                        {
                            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(this.DataTypeSize / scanCountPerVector);
                        }
                    }
                }
            }
        }
    }
    //// End class
}
//// End namespace
