namespace Squalr.View.Editors
{
    using Squalr.Engine.Projects.Items;
    using Squalr.Source.Editors.RenameEditor;
    using System;
    using System.Windows;

    /// <summary>
    /// Interaction logic for RenameEditor.xaml.
    /// </summary>
    public partial class RenameEditor : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenameEditor" /> class.
        /// </summary>
        /// <param name="projectItem">The project being renamed.</param>
        public RenameEditor(ProjectItem projectItem)
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets the view model associated with this view.
        /// </summary>
        public RenameEditorViewModel RenameEditorViewModel
        {
            get
            {
                return this.DataContext as RenameEditorViewModel;
            }
        }

        /// <summary>
        /// Invoked when the added offsets are canceled. Closes the view.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="args">Event args.</param>
        private void CancelButtonClick(Object sender, RoutedEventArgs args)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Invoked when the added offsets are accepted. Closes the view.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="args">Event args.</param>
        private void AcceptButtonClick(Object sender, RoutedEventArgs args)
        {
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Invoked when the exit file menu event executes. Closes the view.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="args">Event args.</param>
        private void ExitFileMenuItemClick(Object sender, RoutedEventArgs args)
        {
            this.Close();
        }

        /// <summary>
        /// Event when this window has been loaded.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="args">Event args.</param>
        private void SqualrRenameEditorLoaded(Object sender, RoutedEventArgs args)
        {
            this.NameTextBox.Focus();
        }
    }
    //// End class
}
//// End namespace