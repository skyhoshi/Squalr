﻿namespace Squalr.View
{
    using Aga.Controls.Tree;
    using Aga.Controls.Tree.NodeControls;
    using Content;
    using SharpDX.DirectInput;
    using Source.Controls;
    using Source.Controls.TreeView;
    using Source.Editors.ScriptEditor;
    using Source.Editors.ValueEditor;
    using Source.Engine;
    using Source.Engine.Input.Keyboard;
    using Source.ProjectExplorer;
    using Source.ProjectExplorer.ProjectItems;
    using Source.Utils;
    using Source.Utils.DataStructures;
    using Source.Utils.Extensions;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;

    /// <summary>
    /// Interaction logic for ProjectExplorer.xaml.
    /// </summary>
    internal partial class ProjectExplorer : System.Windows.Controls.UserControl, IProjectExplorerObserver, IObserver<KeyState>
    {
        /// <summary>
        /// The project explorer tree view.
        /// </summary>
        private TreeViewAdv projectExplorerTreeView;

        /// <summary>
        /// The data model for the project explorer tree view.
        /// </summary>
        private TreeModel projectTree;

        /// <summary>
        /// Two way mapping of nodes to the project items that they represent.
        /// </summary>
        private BiDictionary<ProjectItem, ProjectNode> nodeCache;

        /// <summary>
        /// The current node being dragged.
        /// </summary>
        private TreeNodeAdv draggedItem;

        /// <summary>
        /// The current project items copied to the clip board.
        /// </summary>
        private IEnumerable<ProjectItem> clipBoard;

        /// <summary>
        /// Add New Script menu item.
        /// </summary>
        private ToolStripMenuItem addNewItemMenuItem;

        /// <summary>
        /// Delete Selection menu item.
        /// </summary>
        private ToolStripMenuItem deleteSelectionMenuItem;

        /// <summary>
        /// Toggle Selection menu item.
        /// </summary>
        private ToolStripMenuItem toggleSelectionMenuItem;

        /// <summary>
        /// Compile Selection menu item.
        /// </summary>
        private ToolStripMenuItem compileSelectionMenuItem;

        /// <summary>
        /// Copy Selection menu item.
        /// </summary>
        private ToolStripMenuItem copySelectionMenuItem;

        /// <summary>
        /// Cut Selection menu item.
        /// </summary>
        private ToolStripMenuItem cutSelectionMenuItem;

        /// <summary>
        /// Paste Selection menu item.
        /// </summary>
        private ToolStripMenuItem pasteSelectionMenuItem;

        /// <summary>
        /// Add New Folder menu item.
        /// </summary>
        private ToolStripMenuItem addNewFolderMenuItem;

        /// <summary>
        /// Add New Address menu item.
        /// </summary>
        private ToolStripMenuItem addNewAddressMenuItem;

        /// <summary>
        /// Add New Script menu item.
        /// </summary>
        private ToolStripMenuItem addNewScriptMenuItem;

        /// <summary>
        /// The right click menu.
        /// </summary>
        private ContextMenuStrip contextMenuStrip;

        /// <summary>
        /// The tool tip for project items.
        /// </summary>
        private ToolTip projectItemToolTip;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectExplorer" /> class.
        /// </summary>
        public ProjectExplorer()
        {
            this.InitializeComponent();

            this.nodeCache = new BiDictionary<ProjectItem, ProjectNode>();
            this.projectTree = new TreeModel();
            this.AccessLock = new Object();

            this.InitializeDesigner();
            this.projectExplorerTreeViewContainer.Children.Add(WinformsHostingHelper.CreateHostedControl(this.projectExplorerTreeView));

            EngineCore.GetInstance().Input?.GetKeyboardCapture().WeakSubscribe(this);
            ProjectExplorerViewModel.GetInstance().Subscribe(this);
        }

        /// <summary>
        /// Gets the view model associated with this view.
        /// </summary>
        public ProjectExplorerViewModel ProjectExplorerViewModel
        {
            get
            {
                return this.DataContext as ProjectExplorerViewModel;
            }
        }

        private Object AccessLock { get; set; }

        public void OnNext(KeyState value)
        {
            if (value.PressedKeys.IsNullOrEmpty())
            {
                return;
            }

            foreach (Key key in value.PressedKeys)
            {
                ControlThreadingHelper.InvokeControlAction(
                    this.projectExplorerTreeView,
                    () =>
                {
                    if (!this.projectExplorerTreeView.Focused)
                    {
                        return;
                    }

                    switch (key)
                    {
                        case Key.Space:
                            this.ActivateSelectedItems();
                            break;
                        case Key.Delete:
                            this.DeleteSelectedItems();
                            break;
                        case Key.C:
                            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                this.CopySelection();
                            }

                            break;
                        case Key.X:
                            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                this.CutSelection();
                            }

                            break;
                        case Key.V:
                            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                this.PasteSelection();
                            }

                            break;
                    }
                });
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        /// <summary>
        /// Recieves an update of the project items in the project explorer upon structure changes.
        /// </summary>
        /// <param name="projectRoot">The project root.</param>
        public void UpdateStructure(ProjectRoot projectRoot)
        {
            projectRoot?.BuildParents();

            this.projectExplorerTreeView.BeginUpdate();

            lock (this.AccessLock)
            {
                this.projectTree.Nodes.Clear();
            }

            this.nodeCache.Clear();

            if (projectRoot != null)
            {
                foreach (ProjectItem child in projectRoot.Children.ToArray())
                {
                    this.BuildNodes(child);
                }
            }

            this.projectExplorerTreeView.EndUpdate();
            this.projectExplorerTreeView.ExpandAll();
        }

        /// <summary>
        /// Recieves an update of the project items in the project explorer upon value changes.
        /// </summary>
        /// <param name="projectRoot">The project root.</param>
        public void Update(ProjectRoot projectRoot)
        {
            if (projectRoot != null)
            {
                this.projectExplorerTreeView.BeginUpdate();
                foreach (ProjectItem child in projectRoot.Children.ToArray())
                {
                    this.UpdateNodes(child);
                }

                this.projectExplorerTreeView.EndUpdate();
            }
        }

        /// <summary>
        /// Copies the current selected project items to the clipboard.
        /// </summary>
        private void CopySelection()
        {
            this.clipBoard = this.CloneSelectedProjectItems();
        }

        /// <summary>
        /// Removes the current selected project items, and adds them to the clipboard.
        /// </summary>
        private void CutSelection()
        {
            this.clipBoard = this.CloneSelectedProjectItems();
            this.DeleteSelectedItems();
        }

        /// <summary>
        /// Pastes the project items from the clipboard.
        /// </summary>
        private void PasteSelection()
        {
            if (this.clipBoard == null || this.clipBoard.Count() <= 0)
            {
                return;
            }

            foreach (ProjectItem projectItem in this.clipBoard)
            {
                // We must clone the item, such as to prevent duplicate references of the same exact object
                ProjectExplorerViewModel.GetInstance().AddNewProjectItems(true, projectItem.Clone());
            }
        }

        /// <summary>
        /// Recursively contructs the nodes to add to the tree model.
        /// </summary>
        /// <param name="projectItem">The current project item for which to build a node.</param>
        /// <param name="parent">The parent project item of the added project item.</param>
        private void BuildNodes(ProjectItem projectItem, ProjectItem parent = null)
        {
            if (projectItem == null)
            {
                return;
            }

            // Create new node to insert
            ProjectNode projectNode = new ProjectNode();
            this.UpdateProjectNode(projectNode, projectItem);

            if (parent != null && this.nodeCache.ContainsKey(parent))
            {
                ProjectNode result;
                if (this.nodeCache.TryGetValue(parent, out result))
                {
                    result.Nodes.Add(projectNode);
                }
            }
            else
            {
                lock (this.AccessLock)
                {
                    this.projectTree.Nodes.Add(projectNode);
                }
            }

            this.nodeCache.Add(projectItem, projectNode);

            if (projectItem is FolderItem)
            {
                FolderItem folderItem = projectItem as FolderItem;

                foreach (ProjectItem child in folderItem.Children.ToArray())
                {
                    this.BuildNodes(child, projectItem);
                }
            }
        }

        /// <summary>
        /// Recursively updates all nodes in the treeview, refreshing updated properties.
        /// </summary>
        /// <param name="projectItem">The project item for which to update the corresponding node.</param>
        private void UpdateNodes(ProjectItem projectItem)
        {
            ProjectNode projectNode;

            if (!this.nodeCache.TryGetValue(projectItem, out projectNode))
            {
                return;
            }

            this.UpdateProjectNode(projectNode, projectItem);

            if (projectItem is FolderItem)
            {
                FolderItem folderItem = projectItem as FolderItem;

                foreach (ProjectItem child in folderItem.Children.ToArray())
                {
                    this.UpdateNodes(child);
                }
            }
        }

        private void UpdateProjectNode(ProjectNode projectNode, ProjectItem projectItem)
        {
            if (projectNode == null || projectItem == null)
            {
                return;
            }

            projectNode.ProjectItem = projectItem;
            projectNode.IsChecked = projectItem.IsActivated;
            String hotkeyString = projectItem.HotKey?.ToString();
            projectNode.EntryDescription = projectItem.Description;
            projectNode.EntryStreamCommand = String.IsNullOrEmpty(projectItem.StreamCommand) ? String.Empty : "[" + projectItem.StreamCommand + "]";
            projectNode.EntryHotkey = String.IsNullOrEmpty(hotkeyString) ? String.Empty : "[" + hotkeyString + "]";
            projectNode.EntryValuePreview = (projectItem is AddressItem) ? (projectItem as AddressItem).Value?.ToString() : String.Empty;
            projectNode.IsChecked = projectItem.IsActivated;

            Bitmap image = null;

            if (projectItem is AddressItem || projectItem is ScriptItem)
            {
                switch (projectItem.Category)
                {
                    case ProjectItem.ProjectItemCategory.Glitch:
                        image = ImageUtils.BitmapImageToBitmap(Images.Glitch);
                        break;
                    case ProjectItem.ProjectItemCategory.Curse:
                        image = ImageUtils.BitmapImageToBitmap(Images.Curse);
                        break;
                    case ProjectItem.ProjectItemCategory.Buff:
                        image = ImageUtils.BitmapImageToBitmap(Images.Heart);
                        break;
                    case ProjectItem.ProjectItemCategory.Utility:
                        image = ImageUtils.BitmapImageToBitmap(Images.Cog);
                        break;
                    case ProjectItem.ProjectItemCategory.None:
                    default:
                        image = ImageUtils.BitmapImageToBitmap(Images.CollectValues);
                        break;
                }
            }

            if (projectItem is FolderItem)
            {
                image = ImageUtils.BitmapImageToBitmap(Images.Open);
            }

            projectNode.EntryIcon = image;

            if (projectItem is AddressItem)
            {
                projectNode.EntryValuePreview = (projectItem as AddressItem).Value?.ToString() ?? String.Empty;
            }

            if (projectItem is FolderItem)
            {
                projectNode.EntryValuePreview = "[" + ((projectItem as FolderItem).Children?.Count.ToString() ?? String.Empty) + "]";
            }

            if (projectItem is ScriptItem && (projectItem as ScriptItem).IsCompiled)
            {
                projectNode.EntryValuePreview = "[Compiled]";
            }
        }

        /// <summary>
        /// Gets the project node that corresponds to the given tree node.
        /// </summary>
        /// <param name="treeNodeAdv">The tree node.</param>
        /// <returns>The project node if it exists, otherwise null.</returns>
        private ProjectNode GetProjectNodeFromTreeNodeAdv(TreeNodeAdv treeNodeAdv)
        {
            lock (this.AccessLock)
            {
                Node node = this.projectTree.FindNode(this.projectExplorerTreeView.GetPath(treeNodeAdv));

                if (node == null || !typeof(ProjectNode).IsAssignableFrom(node.GetType()))
                {
                    return null;
                }

                return node as ProjectNode;
            }
        }

        /// <summary>
        /// Gets all selected project items.
        /// </summary>
        /// <returns>A collection of all selected project items.</returns>
        private IEnumerable<ProjectItem> GetSelectedProjectItems()
        {
            List<ProjectItem> nodes = new List<ProjectItem>();

            foreach (TreeNodeAdv node in this.projectExplorerTreeView.SelectedNodes.ToArray())
            {
                nodes.Add(this.GetProjectItemFromNode(node));
            }

            return nodes;
        }

        /// <summary>
        /// Creates a clone of all selected project items.
        /// </summary>
        /// <returns>A collection of the cloned project items.</returns>
        private IEnumerable<ProjectItem> CloneSelectedProjectItems()
        {
            IList<ProjectItem> nodes = new List<ProjectItem>();
            foreach (ProjectItem node in this.GetSelectedProjectItems())
            {
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Gets the project item that corresponds to the given tree node.
        /// </summary>
        /// <param name="treeNodeAdv">The tree node.</param>
        /// <returns>The project item if it exists, otherwise null.</returns>
        private ProjectItem GetProjectItemFromNode(TreeNodeAdv treeNodeAdv)
        {
            return this.GetProjectNodeFromTreeNodeAdv(treeNodeAdv)?.ProjectItem;
        }

        /// <summary>
        /// Activates all selected project items.
        /// </summary>
        private void ActivateSelectedItems()
        {
            IEnumerable<ProjectItem> selectedProjectItems = this.GetSelectedProjectItems();

            if (selectedProjectItems.IsNullOrEmpty())
            {
                return;
            }

            // Behavior here is undefined, we could check only the selected items, or enforce the recursive rules of folders
            selectedProjectItems.ForEach(x => this.CheckItem(x, !x.IsActivated));

            foreach (ProjectItem projectItem in this.GetSelectedProjectItems())
            {
                ProjectNode result;

                if (this.nodeCache.TryGetValue(projectItem, out result))
                {
                    result.IsChecked = projectItem.IsActivated;
                }
            }

            this.UpdateStructure(this.ProjectExplorerViewModel.ProjectRoot);
        }

        /// <summary>
        /// Compiles all selected script project items.
        /// </summary>
        private void CompileSelectedItems()
        {
            IEnumerable<ProjectItem> selectedProjectItems = this.GetSelectedProjectItems();

            if (selectedProjectItems.IsNullOrEmpty())
            {
                return;
            }

            foreach (ProjectItem projectItem in selectedProjectItems)
            {
                if (projectItem is ScriptItem)
                {
                    ScriptItem compiledScript = (projectItem as ScriptItem)?.Compile();

                    if (compiledScript != null)
                    {
                        ProjectExplorerViewModel.GetInstance().AddNewProjectItems(true, compiledScript);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes all selected project items.
        /// </summary>
        private void DeleteSelectedItems()
        {
            if (this.GetSelectedProjectItems().IsNullOrEmpty())
            {
                return;
            }

            System.Windows.MessageBoxResult result = System.Windows.MessageBoxResult.No;
            ControlThreadingHelper.InvokeControlAction(
                this.projectExplorerTreeView,
                () =>
            {
                result = CenteredDialogBox.Show(
                    System.Windows.Application.Current.MainWindow,
                   "Delete selected items?",
                   "Confirm",
                   System.Windows.MessageBoxButton.OKCancel,
                   System.Windows.MessageBoxImage.Warning);
            });

            if (result == System.Windows.MessageBoxResult.OK)
            {
                ProjectExplorerViewModel.GetInstance().DeleteSelectionCommand.Execute(null);
            }
        }

        /// <summary>
        /// Activates or deactivates a project item, updating the corresponding node.
        /// </summary>
        /// <param name="projectItem">The project item.</param>
        /// <param name="activated">The new activation state of the item.</param>
        private void CheckItem(ProjectItem projectItem, Boolean activated)
        {
            if (projectItem == null)
            {
                return;
            }

            if (!projectItem.CanActivate)
            {
                return;
            }

            projectItem.IsActivated = activated;

            ProjectNode node;
            if (this.nodeCache.TryGetValue(projectItem, out node))
            {
                node.IsChecked = projectItem.IsActivated;
            }

            if (projectItem is FolderItem)
            {
                FolderItem folderitem = projectItem as FolderItem;

                switch (folderitem.FolderType)
                {
                    case FolderItem.FolderTypeEnum.Group:
                        foreach (ProjectItem child in folderitem.Children.ToArray())
                        {
                            this.CheckItem(child, activated);
                        }
                        break;
                    case FolderItem.FolderTypeEnum.Normal:
                    case FolderItem.FolderTypeEnum.UniqueGroup:
                        break;
                }

            }
        }

        /// <summary>
        /// Event for checking a project item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Node event args.</param>
        private void CheckIndex(Object sender, NodeControlValueEventArgs e)
        {
            if (e == null || e.Node == null)
            {
                return;
            }

            ProjectItem projectItem = this.GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            this.CheckItem(projectItem, !projectItem.IsActivated);
            this.UpdateStructure(this.ProjectExplorerViewModel.ProjectRoot);
        }

        /// <summary>
        /// Event for double clicking a project node.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Tree node mouse event args.</param>
        private void ProjectExplorerTreeViewNodeMouseDoubleClick(Object sender, TreeNodeAdvMouseEventArgs e)
        {
            if (e == null || e.Node == null)
            {
                return;
            }

            ProjectItem projectItem = this.GetProjectItemFromNode(e.Node);

            if (projectItem is AddressItem)
            {
                ValueEditorModel valueEditor = new ValueEditorModel();
                AddressItem addressItem = projectItem as AddressItem;
                dynamic result = valueEditor.EditValue(null, null, addressItem);

                if (CheckSyntax.CanParseValue(addressItem.ElementType, result?.ToString()))
                {
                    addressItem.Value = result;
                }
            }
            else if (projectItem is ScriptItem)
            {
                ScriptEditorModel scriptEditor = new ScriptEditorModel();
                ScriptItem scriptItem = projectItem as ScriptItem;
                scriptItem.Script = scriptEditor.EditValue(null, null, scriptItem.Script) as String;
            }
        }

        /// <summary>
        /// Event for a changed selection of project nodes.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Drag event args.</param>
        private void ProjectExplorerTreeViewSelectionChanged(Object sender, EventArgs e)
        {
            // Update the view model with the selected nodes
            List<ProjectItem> projectItems = new List<ProjectItem>();

            foreach (ProjectItem node in this.GetSelectedProjectItems())
            {
                projectItems.Add(node);
            }

            ProjectExplorerViewModel.GetInstance().SelectedProjectItems = projectItems;
        }

        /// <summary>
        /// Event for Drag Start of a project node.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Drag event args.</param>
        private void ProjectExplorerTreeViewItemDrag(Object sender, ItemDragEventArgs e)
        {
            this.draggedItem = e.Item as TreeNodeAdv;
            this.projectExplorerTreeView.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        /// <summary>
        /// Event for Drag Move of a project node.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Drag event args.</param>
        private void ProjectExplorerTreeViewDragOver(Object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        /// <summary>
        /// Event for Drag Enter of a project node.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Drag event args.</param>
        private void ProjectExplorerTreeViewDragEnter(Object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        /// <summary>
        /// Event for Drag Drop of a project node.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Drag event args.</param>
        private void ProjectExplorerTreeViewDragDrop(Object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the drop location.
            Point targetPoint = this.projectExplorerTreeView.PointToClient(new Point(e.X, e.Y));

            // Retrieve the node at the drop location.
            TreeNodeAdv targetNodeAdv = this.projectExplorerTreeView.GetNodeAt(targetPoint);

            // Retrieve the node that was dragged.
            TreeNodeAdv[] draggedNodesAdv = e.Data.GetData(typeof(TreeNodeAdv[])) as TreeNodeAdv[];

            if (draggedNodesAdv.Count() <= 0)
            {
                return;
            }

            TreeNodeAdv draggedNodeAdv = draggedNodesAdv[0];

            if (draggedNodeAdv != null && targetNodeAdv != null && draggedNodeAdv != targetNodeAdv)
            {
                ProjectRoot projectRoot = this.ProjectExplorerViewModel.ProjectRoot;
                ProjectItem draggedItem = this.GetProjectItemFromNode(draggedNodeAdv);
                ProjectItem targetItem = this.GetProjectItemFromNode(targetNodeAdv);

                projectRoot?.BuildParents();

                if (!(draggedItem is FolderItem) || !(draggedItem as FolderItem).HasNode(targetItem))
                {
                    switch (this.projectExplorerTreeView.DropPosition.Position)
                    {
                        case NodePosition.Before:
                            projectRoot.RemoveNode(draggedItem, dispose: false);
                            targetItem.Parent?.AddSibling(targetItem, draggedItem, after: false);
                            break;
                        case NodePosition.Inside:
                            if (targetItem is FolderItem)
                            {
                                projectRoot.RemoveNode(draggedItem, dispose: false);
                                (targetItem as FolderItem).AddChild(draggedItem);
                            }

                            break;
                        case NodePosition.After:
                            projectRoot.RemoveNode(draggedItem, dispose: false);
                            targetItem.Parent?.AddSibling(targetItem, draggedItem, after: true);
                            break;
                    }
                }

                this.UpdateStructure(this.ProjectExplorerViewModel.ProjectRoot);
            }
        }

        /// <summary>
        /// Initialize all windows forms components.
        /// </summary>
        private void InitializeDesigner()
        {
            NodeCheckBox entryCheckBox = new NodeCheckBox();
            entryCheckBox.DataPropertyName = "IsChecked";
            entryCheckBox.EditEnabled = true;
            entryCheckBox.LeftMargin = 0;
            entryCheckBox.ParentColumn = null;
            entryCheckBox.IsEditEnabledValueNeeded += this.CheckIndex;
            entryCheckBox.IsVisibleValueNeeded += this.EntryCheckBoxIsVisibleValueNeeded;

            NodeIcon entryIcon = new NodeIcon();
            entryIcon.DataPropertyName = "EntryIcon";
            entryIcon.LeftMargin = 0;
            entryIcon.ParentColumn = null;
            entryIcon.ScaleMode = ImageScaleMode.Clip;

            NodeTextBox entryDescription = new NodeTextBox();
            entryDescription.DataPropertyName = "EntryDescription";
            entryDescription.IncrementalSearchEnabled = true;
            entryDescription.LeftMargin = 0;
            entryDescription.ParentColumn = null;
            entryDescription.DrawText += this.EntryDescriptionDrawText;
            entryDescription.IsVisibleValueNeeded += EntryDescriptionIsVisibleValueNeeded;

            NodeTextBox entryStreamCommand = new NodeTextBox();
            entryStreamCommand.DataPropertyName = "EntryStreamCommand";
            entryStreamCommand.IncrementalSearchEnabled = true;
            entryStreamCommand.LeftMargin = 0;
            entryStreamCommand.ParentColumn = null;
            entryStreamCommand.DrawText += this.EntryStreamCommandDrawText;
            entryStreamCommand.IsVisibleValueNeeded += this.EntryStreamCommandIsVisibleValueNeeded;

            NodeTextBox entryHotkey = new NodeTextBox();
            entryHotkey.DataPropertyName = "EntryHotkey";
            entryHotkey.IncrementalSearchEnabled = true;
            entryHotkey.LeftMargin = 0;
            entryHotkey.ParentColumn = null;
            entryHotkey.DrawText += this.EntryHotkeyDrawText;
            entryHotkey.IsVisibleValueNeeded += this.EntryHotkeyIsVisibleValueNeeded;

            NodeTextBox entryValuePreview = new NodeTextBox();
            entryValuePreview.DataPropertyName = "EntryValuePreview";
            entryValuePreview.IncrementalSearchEnabled = true;
            entryValuePreview.LeftMargin = 0;
            entryValuePreview.ParentColumn = null;
            entryValuePreview.DrawText += this.EntryValuePreviewDrawText;
            entryValuePreview.IsVisibleValueNeeded += this.EntryValuePreviewIsVisibleValueNeeded;

            this.projectItemToolTip = new ToolTip();
            this.toggleSelectionMenuItem = new ToolStripMenuItem("Toggle");
            this.compileSelectionMenuItem = new ToolStripMenuItem("Compile");
            this.addNewItemMenuItem = new ToolStripMenuItem("Add New...");
            this.deleteSelectionMenuItem = new ToolStripMenuItem("Delete");
            this.copySelectionMenuItem = new ToolStripMenuItem("Copy");
            this.cutSelectionMenuItem = new ToolStripMenuItem("Cut");
            this.pasteSelectionMenuItem = new ToolStripMenuItem("Paste");
            this.addNewFolderMenuItem = new ToolStripMenuItem("Add Folder", ImageUtils.BitmapImageToBitmap(Images.Open));
            this.addNewAddressMenuItem = new ToolStripMenuItem("Add Address", ImageUtils.BitmapImageToBitmap(Images.CollectValues));
            this.addNewScriptMenuItem = new ToolStripMenuItem("Add Script", ImageUtils.BitmapImageToBitmap(Images.CollectValues));
            this.contextMenuStrip = new ContextMenuStrip();

            KeysConverter keysConverter = new KeysConverter();
            this.toggleSelectionMenuItem.ShortcutKeyDisplayString = keysConverter.ConvertToString(Keys.Space);
            this.copySelectionMenuItem.ShortcutKeyDisplayString = keysConverter.ConvertToString(Keys.Control | Keys.C);
            this.cutSelectionMenuItem.ShortcutKeyDisplayString = keysConverter.ConvertToString(Keys.Control | Keys.X);
            this.pasteSelectionMenuItem.ShortcutKeyDisplayString = keysConverter.ConvertToString(Keys.Control | Keys.V);
            this.deleteSelectionMenuItem.ShortcutKeyDisplayString = keysConverter.ConvertToString(Keys.Delete);

            this.toggleSelectionMenuItem.Click += this.ToggleSelectionMenuItemClick;
            this.compileSelectionMenuItem.Click += this.CompileSelectionMenuItemClick;
            this.deleteSelectionMenuItem.Click += this.DeleteSelectionMenuItemClick;
            this.copySelectionMenuItem.Click += this.CopySelectionMenuItemClick;
            this.cutSelectionMenuItem.Click += this.CutSelectionMenuItemClick;
            this.pasteSelectionMenuItem.Click += this.PasteSelectionMenuItemClick;
            this.addNewFolderMenuItem.Click += this.AddNewFolderMenuItemClick;
            this.addNewAddressMenuItem.Click += this.AddNewAddressMenuItemClick;
            this.addNewScriptMenuItem.Click += this.AddNewScriptMenuItemClick;
            this.contextMenuStrip.Opening += this.ContextMenuStripOpening;

            this.addNewItemMenuItem.DropDownItems.Add(this.addNewFolderMenuItem);
            this.addNewItemMenuItem.DropDownItems.Add(this.addNewAddressMenuItem);
            this.addNewItemMenuItem.DropDownItems.Add(this.addNewScriptMenuItem);

            this.contextMenuStrip.Items.Add(this.toggleSelectionMenuItem);
            this.contextMenuStrip.Items.Add(this.compileSelectionMenuItem);
            this.contextMenuStrip.Items.Add(this.addNewItemMenuItem);
            this.contextMenuStrip.Items.Add(this.deleteSelectionMenuItem);
            this.contextMenuStrip.Items.Add(new ToolStripSeparator());
            this.contextMenuStrip.Items.Add(this.copySelectionMenuItem);
            this.contextMenuStrip.Items.Add(this.cutSelectionMenuItem);
            this.contextMenuStrip.Items.Add(this.pasteSelectionMenuItem);

            this.projectExplorerTreeView = new TreeViewAdv();
            this.projectExplorerTreeView.NodeControls.Add(entryCheckBox);
            this.projectExplorerTreeView.NodeControls.Add(entryIcon);
            this.projectExplorerTreeView.NodeControls.Add(entryDescription);
            this.projectExplorerTreeView.NodeControls.Add(entryStreamCommand);
            this.projectExplorerTreeView.NodeControls.Add(entryHotkey);
            this.projectExplorerTreeView.NodeControls.Add(entryValuePreview);
            this.projectExplorerTreeView.SelectionMode = TreeSelectionMode.Multi;
            this.projectExplorerTreeView.BorderStyle = BorderStyle.None;
            this.projectExplorerTreeView.Model = this.projectTree;
            this.projectExplorerTreeView.AllowDrop = true;
            this.projectExplorerTreeView.FullRowSelect = true;
            this.projectExplorerTreeView.ContextMenuStrip = this.contextMenuStrip;

            this.projectExplorerTreeView.RowDraw += ProjectExplorerTreeViewRowDraw;
            this.projectExplorerTreeView.ItemDrag += this.ProjectExplorerTreeViewItemDrag;
            this.projectExplorerTreeView.NodeMouseDoubleClick += this.ProjectExplorerTreeViewNodeMouseDoubleClick;
            this.projectExplorerTreeView.SelectionChanged += this.ProjectExplorerTreeViewSelectionChanged;
            this.projectExplorerTreeView.DragDrop += this.ProjectExplorerTreeViewDragDrop;
            this.projectExplorerTreeView.DragEnter += this.ProjectExplorerTreeViewDragEnter;
            this.projectExplorerTreeView.DragOver += this.ProjectExplorerTreeViewDragOver;
            this.projectExplorerTreeView.MouseMove += this.ProjectExplorerTreeViewMouseMove;

            this.projectExplorerTreeView.BackColor = DarkBrushes.BaseColor3;
            this.projectExplorerTreeView.ForeColor = DarkBrushes.BaseColor2;
            this.projectExplorerTreeView.DragDropMarkColor = DarkBrushes.BaseColor11;
            this.projectExplorerTreeView.LineColor = DarkBrushes.BaseColor11;
        }

        private void EntryValuePreviewIsVisibleValueNeeded(Object sender, NodeControlValueEventArgs e)
        {
            ProjectItem projectItem = GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            String previewText = String.Empty;

            if (projectItem is AddressItem)
            {
                if (String.IsNullOrWhiteSpace((projectItem as AddressItem).Value?.ToString()))
                {
                    e.Value = false;
                    return;
                }
            }

            if (projectItem is FolderItem)
            {
                if (String.IsNullOrWhiteSpace((projectItem as FolderItem).Children?.Count.ToString()))
                {
                    e.Value = false;
                    return;
                }
            }

            if (projectItem is ScriptItem && !(projectItem as ScriptItem).IsCompiled)
            {
                e.Value = false;
                return;
            }

            e.Value = true;
        }

        private void EntryHotkeyIsVisibleValueNeeded(Object sender, NodeControlValueEventArgs e)
        {
            ProjectItem projectItem = GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(projectItem.HotKey?.ToString()))
            {
                e.Value = false;
            }
            else
            {
                e.Value = true;
            }
        }

        private void EntryStreamCommandIsVisibleValueNeeded(Object sender, NodeControlValueEventArgs e)
        {
            ProjectItem projectItem = GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(projectItem.StreamCommand))
            {
                e.Value = false;
            }
            else
            {
                e.Value = true;
            }
        }

        private void EntryDescriptionIsVisibleValueNeeded(Object sender, NodeControlValueEventArgs e)
        {
            ProjectItem projectItem = GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(projectItem.Description))
            {
                e.Value = false;
            }
            else
            {
                e.Value = true;
            }
        }

        private void EntryCheckBoxIsVisibleValueNeeded(Object sender, NodeControlValueEventArgs e)
        {
            ProjectItem projectItem = GetProjectItemFromNode(e.Node);

            if (projectItem == null)
            {
                return;
            }

            e.Value = projectItem.CanActivate;
        }

        private void ProjectExplorerTreeViewRowDraw(Object sender, TreeViewRowDrawEventArgs e)
        {
            ProjectItem projectItem = this.GetProjectNodeFromTreeNodeAdv(e.Node)?.ProjectItem;

            if (projectItem != null && projectItem.IsActivated)
            {
                e.Graphics.FillRectangle(new SolidBrush(DarkBrushes.BaseColor15), 0, e.RowRect.Top, ((Control)sender).Width, e.RowRect.Height);
            }
        }

        /// <summary>
        /// Event when the mouse moves on the tree view. Will show the appropriate tooltip for the target item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void ProjectExplorerTreeViewMouseMove(Object sender, MouseEventArgs e)
        {
            // Get the node at the current mouse pointer location
            ProjectItem projectItem = this.GetProjectItemFromNode(this.projectExplorerTreeView.GetNodeAt(e.Location));

            if (projectItem != null)
            {
                // Change the ToolTip only if the pointer moved to a new node
                if (projectItem.ExtendedDescription != this.projectItemToolTip.GetToolTip(this.projectExplorerTreeView))
                {
                    this.projectItemToolTip.SetToolTip(this.projectExplorerTreeView, projectItem.ExtendedDescription);
                }
            }
            else
            {
                // Pointer is not over a node so clear the ToolTip
                this.projectItemToolTip.SetToolTip(this.projectExplorerTreeView, String.Empty);
            }
        }

        /// <summary>
        /// Event when the context menu begins to open. Will enable or disable menu items depending on whether they should be accessible.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void ContextMenuStripOpening(Object sender, CancelEventArgs e)
        {
            if (this.GetSelectedProjectItems().IsNullOrEmpty())
            {
                this.deleteSelectionMenuItem.Enabled = false;
                this.toggleSelectionMenuItem.Enabled = false;
                this.copySelectionMenuItem.Enabled = false;
                this.cutSelectionMenuItem.Enabled = false;
                this.compileSelectionMenuItem.Visible = false;
            }
            else
            {
                this.deleteSelectionMenuItem.Enabled = true;
                this.toggleSelectionMenuItem.Enabled = true;
                this.copySelectionMenuItem.Enabled = true;
                this.cutSelectionMenuItem.Enabled = true;

                if (this.GetSelectedProjectItems().All(x => x?.GetType() == typeof(ScriptItem)))
                {
                    this.compileSelectionMenuItem.Visible = true;
                }
                else
                {
                    this.compileSelectionMenuItem.Visible = false;
                }
            }

            if (this.clipBoard == null || this.clipBoard.Count() <= 0)
            {
                this.pasteSelectionMenuItem.Enabled = false;
            }
            else
            {
                this.pasteSelectionMenuItem.Enabled = true;
            }
        }

        /// <summary>
        /// Event when drawing the description text. Sets the draw color.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void EntryDescriptionDrawText(Object sender, DrawEventArgs e)
        {
            e.TextColor = DarkBrushes.BaseColor2;
        }

        /// <summary>
        /// Event when drawing the stream command preview text. Sets the draw color.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void EntryStreamCommandDrawText(Object sender, DrawEventArgs e)
        {
            e.TextColor = DarkBrushes.BaseColor16;
        }

        /// <summary>
        /// Event when drawing the hotkey preview text. Sets the draw color.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void EntryHotkeyDrawText(Object sender, DrawEventArgs e)
        {
            e.TextColor = DarkBrushes.BaseColor15;
        }

        /// <summary>
        /// Event when drawing the value preview text. Sets the draw color.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void EntryValuePreviewDrawText(Object sender, DrawEventArgs e)
        {
            e.TextColor = DarkBrushes.BaseColor11;
        }

        /// <summary>
        /// Event for the Copy Selection menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void CopySelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.CopySelection();
        }

        /// <summary>
        /// Event for the Cut Selection menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void CutSelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.CutSelection();
        }

        /// <summary>
        /// Event for the Paste Selection menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void PasteSelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.PasteSelection();
        }

        /// <summary>
        /// Event for the Add New Script menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void AddNewScriptMenuItemClick(Object sender, EventArgs e)
        {
            ProjectExplorerViewModel.GetInstance().AddNewScriptItemCommand.Execute(null);
        }

        /// <summary>
        /// Event for the Add New Address menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void AddNewAddressMenuItemClick(Object sender, EventArgs e)
        {
            ProjectExplorerViewModel.GetInstance().AddNewAddressItemCommand.Execute(null);
        }

        /// <summary>
        /// Event for the Add New Folder menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void AddNewFolderMenuItemClick(Object sender, EventArgs e)
        {
            ProjectExplorerViewModel.GetInstance().AddNewFolderItemCommand.Execute(null);
        }

        /// <summary>
        /// Event for the Delete Selection menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void DeleteSelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.DeleteSelectedItems();
        }

        /// <summary>
        /// Event for the Toggle Select menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void ToggleSelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.ActivateSelectedItems();
        }

        /// <summary>
        /// Event for the Compile Script menu item.
        /// </summary>
        /// <param name="sender">Sending object.</param>
        /// <param name="e">Event args.</param>
        private void CompileSelectionMenuItemClick(Object sender, EventArgs e)
        {
            this.CompileSelectedItems();
        }
    }
    //// End class
}
//// End namespace