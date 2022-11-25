namespace Squalr.Engine.Common
{
    using System.Runtime.Serialization;

    /// <summary>
    /// An enum representing an emulator target.
    /// </summary>
    [DataContract]
    public enum EmulatorType
    {
        Auto,
        None,
        Dolphin,
    }
    //// End enum
}
//// End namespace