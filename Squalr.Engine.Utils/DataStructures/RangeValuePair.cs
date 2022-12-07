namespace Squalr.Engine.Common.DataStructures
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a range of values. Both values must be of the same type and comparable.
    /// </summary>
    /// <typeparam name="TKey">Type of the keys.</typeparam>
    /// <typeparam name="TValue">Type of the values.</typeparam>
    public readonly struct RangeValuePair<TKey, TValue> : IEquatable<RangeValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Initializes a new <see cref="RangeValuePair{TKey, TValue}"/> instance.
        /// </summary>
        public RangeValuePair(TKey from, TKey to, TValue value) : this()
        {
            this.From = from;
            this.To = to;
            this.Value = value;
        }

        public TKey From { get; }

        public TKey To { get; }

        public TValue Value { get; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("[{0} - {1}] {2}", this.From, this.To, this.Value);
        }

        public override Int32 GetHashCode()
        {
            Int32 hash = 23;

            if (this.From != null)
            {
                hash = hash * 37 + From.GetHashCode();
            }

            if (this.To != null)
            {
                hash = hash * 37 + To.GetHashCode();
            }

            if (this.Value != null)
            {
                hash = hash * 37 + Value.GetHashCode();
            }

            return hash;
        }

        public Boolean Equals(RangeValuePair<TKey, TValue> other)
        {
            return EqualityComparer<TKey>.Default.Equals(this.From, other.From)
                   && EqualityComparer<TKey>.Default.Equals(this.To, other.To)
                   && EqualityComparer<TValue>.Default.Equals(this.Value, other.Value);
        }

        public override Boolean Equals(Object obj)
        {
            if (!(obj is RangeValuePair<TKey, TValue>))
            {
                return false;
            }

            return Equals((RangeValuePair<TKey, TValue>)obj);
        }

        public static Boolean operator ==(RangeValuePair<TKey, TValue> left, RangeValuePair<TKey, TValue> right)
        {
            return left.Equals(right);
        }

        public static Boolean operator !=(RangeValuePair<TKey, TValue> left, RangeValuePair<TKey, TValue> right)
        {
            return !(left == right);
        }
    }
    //// End class
}
//// End namespace