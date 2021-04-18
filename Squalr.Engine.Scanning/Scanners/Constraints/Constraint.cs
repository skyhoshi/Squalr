namespace Squalr.Engine.Scanning.Scanners.Constraints
{
    using System;

    public abstract class Constraint
    {
        public Constraint()
        {
        }

        /// <summary>
        /// Gets the element type of this constraint manager.
        /// </summary>
        public Type ElementType { get; private set; }

        /// <summary>
        /// Sets the element type to which all constraints apply.
        /// </summary>
        /// <param name="elementType">The new element type.</param>
        public virtual void SetElementType(Type elementType)
        {
            this.ElementType = elementType;
        }

        public virtual Boolean IsValid()
        {
            return false;
        }
    }
    //// End class
}
//// End namespace