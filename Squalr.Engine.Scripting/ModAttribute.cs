namespace Squalr.Engine.Scripting
{
    using System;

    public class ModAttribute : Attribute
    {
        public String Name { get; set; }

        public InputType InputType { get; set; }
    }
    //// End class
}
//// End namespace