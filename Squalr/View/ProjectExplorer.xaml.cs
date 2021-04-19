namespace Squalr.View
{
    using Squalr.Source.ProjectExplorer.ProjectItems;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for Settings.xaml.
    /// </summary>
    public partial class ProjectExplorer : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Settings" /> class.
        /// </summary>
        public ProjectExplorer()
        {
            this.InitializeComponent();

            // This works, but can be offloaded to a helper class, or perhaps rolled into the viewmodel itself.
            // Should be modified to support keyboard ctrl/shift+arrow stuff.
            // It's shit, but it's a great place to start.
            AllowMultiSelection(ProjectExplorerTreeView);
        }

        private static readonly PropertyInfo IsSelectionChangeActiveProperty = typeof(TreeView).GetProperty("IsSelectionChangeActive", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void AllowMultiSelection(TreeView treeView)
        {
            if (IsSelectionChangeActiveProperty == null)
            {
                return;
            }

            List<ProjectItemView> selectedItems = new List<ProjectItemView>();

            treeView.SelectedItemChanged += (a, b) =>
            {
                ProjectItemView treeViewItem = treeView.SelectedItem as ProjectItemView;

                if (treeViewItem == null)
                {
                    return;
                }

                // allow multiple selection
                // when control key is pressed
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    // suppress selection change notification
                    // select all selected items
                    // then restore selection change notifications
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);

                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    selectedItems.ForEach(item => item.IsSelected = true);

                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                }
                else
                {
                    // deselect all selected items except the current one
                    selectedItems.ForEach(item => item.IsSelected = (item == treeViewItem));
                    selectedItems.Clear();
                }

                if (!selectedItems.Contains(treeViewItem))
                {
                    selectedItems.Add(treeViewItem);
                }
                else
                {
                    // deselect if already selected
                    treeViewItem.IsSelected = false;
                    selectedItems.Remove(treeViewItem);
                }
            };
        }
    }
    //// End class
}
//// End namespace