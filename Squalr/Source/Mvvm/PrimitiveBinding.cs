﻿namespace Squalr.Source.Mvvm
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Display class to allow binding of a collection of primitive types, which is normally not allowed.
    /// </summary>
    /// <typeparam name="T">The primitive type.</typeparam>
    public class PrimitiveBinding<T> : INotifyPropertyChanged where T : struct
    {
        private T value;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrimitiveBinding{T}" /> class.
        /// </summary>
        /// <param name="value">The primitive value.</param>
        public PrimitiveBinding(T value)
        {
            this.Value = value;
        }

        /// <summary>
        /// Gets or sets the primitive value.
        /// </summary>
        public T Value
        {
            get
            {
                return this.value;
            }

            set
            {
                this.value = value;
                this.OnPropertyChanged(nameof(this.Value));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Indicates that a given property in this project item has changed.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void OnPropertyChanged(String propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    //// End class
}
//// End namespace