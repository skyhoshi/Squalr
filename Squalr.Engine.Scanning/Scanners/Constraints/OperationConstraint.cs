namespace Squalr.Engine.Scanning.Scanners.Constraints
{
    using Squalr.Engine.Common;
    using System;

    /// <summary>
    /// Class for storing a collection of constraints to be used in a scan that applies more than one constraint per update.
    /// </summary>
    public class OperationConstraint : Constraint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScanConstraintTree" /> class.
        /// </summary>
        public OperationConstraint(OperationType operation, Constraint left = null, Constraint right = null)
        {
            this.BinaryOperation = operation;
            this.Left = left;
            this.Right = right;
        }

        /// <summary>
        /// Sets the element type to which all constraints apply.
        /// </summary>
        /// <param name="elementType">The new element type.</param>
        public override void SetElementType(ScannableType elementType)
        {
            this.Left?.SetElementType(elementType);
            this.Right?.SetElementType(elementType);
        }

        // TODO: Balance these trees to be "Right heavy". Scans currently early-exit after evaluating the left tree.

        public OperationType BinaryOperation { get; private set; }

        public Constraint Left { get; set; }

        public Constraint Right { get; set; }

        public override Boolean IsValid()
        {
            return (this.Left?.IsValid() ?? false) && (this.Right?.IsValid() ?? false);
        }

        public override Constraint Clone()
        {
            return new OperationConstraint(this.BinaryOperation, this.Left?.Clone(), this.Right?.Clone());
        }

        public enum OperationType
        {
            OR,
            AND,
            XOR,
        }
    }
    //// End class
}
//// End namespace