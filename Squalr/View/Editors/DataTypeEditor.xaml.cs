namespace Squalr.View.Editors
{
    using Squalr.Engine.Common;
    using Squalr.Source.Editors.DataTypeEditor;
    using System;
    using System.ComponentModel;
    using System.Windows;

    /// <summary>
    /// Interaction logic for DataTypeEditor.xaml.
    /// </summary>
    public partial class DataTypeEditor : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataTypeEditor" /> class.
        /// </summary>
        /// <param name="offsets">The initial offsets to edit.</param>
        public DataTypeEditor()
        {
            this.InitializeComponent();

            this.DataTypeEditorViewModel.DataType = ScannableType.Int32;
            this.DataTypeEditorViewModel.PropertyChanged += DataTypeEditorViewModel_PropertyChanged;
        }

        /// <summary>
        /// Gets the view model associated with this view.
        /// </summary>
        public DataTypeEditorViewModel DataTypeEditorViewModel
        {
            get
            {
                return this.DataContext as DataTypeEditorViewModel;
            }
        }

        /// <summary>
        /// Gets or sets a boolean indicating if the modal has been attempted to be closed.
        /// </summary>
        private bool HasClosed { get; set; }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            if (!this.HasClosed && this.DialogResult != true)
            {
                this.DialogResult = false;
                this.HasClosed = true;
                this.Close();
            }
        }

        private void DataTypeEditorViewModel_PropertyChanged(Object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(DataTypeEditorViewModel.DataType))
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (!this.HasClosed)
                    {
                        this.DialogResult = true;
                        this.HasClosed = true;
                        this.Close();
                    }
                }));
            }
        }
    }
    //// End class
}
//// End namespace