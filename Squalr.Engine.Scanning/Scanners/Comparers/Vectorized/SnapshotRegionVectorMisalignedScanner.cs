namespace Squalr.Engine.Scanning.Scanners.Comparers.Vectorized
{
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// A faster version of SnapshotElementComparer that takes advantage of vectorization/SSE instructions.
    /// </summary>
    internal unsafe class SnapshotRegionVectorMisalignedScanner : SnapshotRegionVectorScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionVectorMisalignedScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionVectorMisalignedScanner() : base()
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
            // This algorithm works as such:
            // 1) Load a hardware vector of the data type to scan (say 128 bytes => 16 doubles).
            // 2) Simultaneously scan all 16 doubles (scan result will be true/false).
            // 3) Store the results in a run length encoding (RLE) vector. Important: this RLE vector is (temporarily) a lie.
            //      In this case 1-byte alignment would mean there are 7 additional doubles between each of the doubles we just scanned!
            // 4) For this reason, we maintain an RLE vector and populate the "in-between" values for any alignments.
            //      ie we may have a RLE vector of < 1111000, 00001111 ... >, which would indicate 4 consecutive successes, 8 consecutive fails, and 4 consecutive successes.
            // 5) Process the RLE vector to update our RunLength variable, and encode any regions as they complete.

            // Since this algorithm loads elements into a hardware vector, there are three cases we need to consider:
            // Case 1) The snapshot readgroup does not fit into a single hardware vector (In which case using this scan class was a mistake, and it will crash)
            // Case 2) The snapshot region can be broken into chunks that fit perfectly in hardware vectors
            // Case 3) The snapshot region can fit into a hardware vector, but the base address would not allow the remaining data to fit into a vector
            //      In this case, we shift the base address to read in some extra data, and ignore the result of these extra scans.

            Int32 scanCountPerVector = this.DataTypeSize / unchecked((Int32)this.Alignment);
            Vector<Byte> allEqualsVector = new Vector<Byte>(unchecked((Byte)(1 << unchecked((Byte)scanCountPerVector) - 1)));
            Vector<Byte> runLengthVector;

            for (; this.VectorReadOffset < this.Region.Range; this.VectorReadOffset += Vectors.VectorSize)
            {
                runLengthVector = Vector<Byte>.Zero;

                for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                {
                    Vector<Byte> scanResults = this.VectorCompare();

                    runLengthVector = Vector.BitwiseOr(runLengthVector, Vector.BitwiseAnd(AlignmentMaskTable[alignmentIndex], scanResults));
                }

                // Optimization: check all vector results true
                if (Vector.EqualsAll(runLengthVector, allEqualsVector))
                {
                    this.RunLengthEncoder.EncodeBatch(Vectors.VectorSize);
                    continue;
                }
                // Optimization: check all vector results false
                else if (Vector.EqualsAll(runLengthVector, Vector<Byte>.Zero))
                {
                    this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(Vectors.VectorSize);
                    continue;
                }

                // Otherwise the vector contains a mixture of true and false
                for (Int32 resultIndex = 0; resultIndex < Vectors.VectorSize; resultIndex += this.DataTypeSize)
                {
                    Byte runLengthFlags = runLengthVector[resultIndex];

                    for (Int32 alignmentIndex = 0; alignmentIndex < scanCountPerVector; alignmentIndex++)
                    {
                        Boolean runLengthResult = (runLengthFlags & unchecked((Byte)(1 << alignmentIndex))) != 0;

                        if (runLengthResult)
                        {
                            this.RunLengthEncoder.EncodeBatch(this.DataTypeSize / scanCountPerVector);
                        }
                        else
                        {
                            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(this.DataTypeSize / scanCountPerVector);
                        }
                    }
                }
            }

            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked();

            return this.RunLengthEncoder.GetCollectedRegions();
        }
    }
    //// End class
}
//// End namespace
