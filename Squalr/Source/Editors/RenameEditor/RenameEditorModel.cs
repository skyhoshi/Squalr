namespace Squalr.Source.Editors.RenameEditor
{
    using Squalr.Engine.Projects.Items;
    using System;
    using System.ComponentModel;
    using System.Drawing.Design;
    using System.Windows;

    /// <summary>
    /// Editor for renaming project items.
    /// </summary>
    public class RenameEditorModel : UITypeEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenameEditorModel" /> class.
        /// </summary>
        public RenameEditorModel()
        {
        }

        /// <summary>
        /// Gets the editor style. This will be Modal, as it launches a custom editor.
        /// </summary>
        /// <param name="context">Type descriptor context.</param>
        /// <returns>Modal type editor.</returns>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        /// <summary>
        /// Launches the editor for this type.
        /// </summary>
        /// <param name="context">Type descriptor context.</param>
        /// <param name="provider">Service provider.</param>
        /// <param name="value">The current value.</param>
        /// <returns>The updated values.</returns>
        public override Object EditValue(ITypeDescriptorContext context, IServiceProvider provider, Object value)
        {
            View.Editors.RenameEditor renameEditor = new View.Editors.RenameEditor(value as ProjectItem) { Owner = Application.Current.MainWindow };

            if (renameEditor.ShowDialog() == true)
            {
                return renameEditor.RenameEditorViewModel.NewName;
            }

            return null;
        }
    }
    //// End class
}
//// End namespace