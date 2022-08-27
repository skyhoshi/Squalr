namespace Squalr.Engine.Scanning.Scanners.Constraints
{
    using Squalr.Engine.Common;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Class to define a constraint for certain types of scans.
    /// </summary>
    public class ScanConstraints : Constraint, INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScanConstraints" /> class.
        /// </summary>
        public ScanConstraints(Type elementType, Constraint rootConstraint)
        {
            this.RootConstraint = rootConstraint;
            this.SetElementType(elementType);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the element type of this constraint manager.
        /// </summary>
        public ScannableType ElementType { get; private set; }

        /// <summary>
        /// Gets the root constraint for this scan constraint set. Usually, this is just a single scan constraint like "> 5".
        /// </summary>
        public Constraint RootConstraint { get; private set; }

        /// <summary>
        /// Sets the element type to which all constraints apply.
        /// </summary>
        /// <param name="elementType">The new element type.</param>
        public override void SetElementType(ScannableType elementType)
        {
            this.ElementType = elementType;
            this.RootConstraint?.SetElementType(elementType);
        }

        public override Boolean IsValid()
        {
            return this.RootConstraint?.IsValid() ?? false;
        }

        public override Constraint Clone()
        {
            return new ScanConstraints(this.ElementType, this.RootConstraint?.Clone());
        }
    }
    //// End class
}
//// End namespace