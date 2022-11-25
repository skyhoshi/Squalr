namespace Squalr.Engine.Common.OS
{
    using System;
    using System.Numerics;

    public static class Vectors
    {
        public static Vector<Byte> QuarterZeros { get; private set; }
        public static Vector<Byte> HalfZeros { get; private set; }
        public static Vector<Byte> ThreeFourthsZeros { get; private set; }

        static Vectors()
        {
            Byte[] quarterZeros = new Byte[Vectors.VectorSize];
            Byte[] halfZeros = new Byte[Vectors.VectorSize];
            Byte[] threeFourthsZeros = new Byte[Vectors.VectorSize];

            for (Int32 index = Vectors.VectorSize / 4; index < Vectors.VectorSize; index++)
            {
                quarterZeros[index] = 0xFF;
            }

            for (Int32 index = Vectors.VectorSize / 2; index < Vectors.VectorSize; index++)
            {
                halfZeros[index] = 0xFF;
            }

            for (Int32 index = Vectors.VectorSize * 3 / 4; index < Vectors.VectorSize; index++)
            {
                threeFourthsZeros[index] = 0xFF;
            }

            Vectors.HalfZeros = new Vector<Byte>(halfZeros);
            Vectors.QuarterZeros = new Vector<Byte>(quarterZeros);
            Vectors.ThreeFourthsZeros = new Vector<Byte>(threeFourthsZeros);
        }

        /// <summary>
        /// Gets a value indicating if the archiecture has vector instruction support.
        /// </summary>
        public static Boolean HasVectorSupport
        {
            get
            {
                return Vector.IsHardwareAccelerated;
            }
        }

        /// <summary>
        /// Gets the vector size supported by the current architecture.
        /// If vectors are not supported, returns the lowest common denominator vector size for the architecture.
        /// </summary>
        public static Int32 VectorSize
        {
            get
            {
                return Vector<Byte>.Count;
            }
        }
    }
    //// End class
}
//// End namespace