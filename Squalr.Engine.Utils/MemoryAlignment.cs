namespace Squalr.Engine.Common
{
    using System.Runtime.Serialization;

    /// <summary>
    /// An enum for configuring memory alignment.
    /// </summary>
    [DataContract]
    public enum MemoryAlignment
    {
        Auto = 0,
        Alignment1 = 1,
        Alignment2 = 2,
        Alignment4 = 4,
        Alignment8 = 8,
    }
    //// End class
}
//// End namespace