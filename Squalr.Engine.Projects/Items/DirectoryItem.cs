namespace Squalr.Engine.Projects.Items
{
    using Squalr.Engine.Common.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Defines a directory in the project.
    /// </summary>
    public class DirectoryItem : ProjectItem
    {
        public delegate void ProjectItemDeleted(ProjectItem projectItem);
        public delegate void ProjectItemAdded(ProjectItem projectItem);

        public ProjectItemDeleted ProjectItemDeletedEvent { get; set; }
        public ProjectItemDeleted ProjectItemAddedEvent { get; set; }

        /// <summary>
        /// The child project items under this directory.
        /// </summary>
        private Dictionary<String, ProjectItem> childItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryItem" /> class.
        /// </summary>
        public DirectoryItem(String directoryPath, DirectoryItem parent) : base(directoryPath)
        {
            // Bypass setters to avoid re-saving
            this.Parent = parent;
            this.childItems = new Dictionary<String, ProjectItem>();
            this.name = new DirectoryInfo(directoryPath).Name;

            try
            {
                this.LoadAllChildProjectItems();
                this.WatchForUpdates();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error initializing project directory", ex);
            }
        }

        /// <summary>
        /// Creates a directory item from the specified project directory path, instantiating all children.
        /// </summary>
        /// <param name="directoryPath">The path to the project directory or subdirectory.</param>
        /// <returns>The instantiated directory item.</returns>
        public static DirectoryItem FromDirectory(String directoryPath, DirectoryItem parent)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    throw new Exception("Directory does not exist: " + directoryPath);
                }

                return new DirectoryItem(directoryPath, parent);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error loading file", ex);
                throw ex;
            }
        }


        /// <summary>
        /// Gets the child project items under this directory.
        /// </summary>
        public Dictionary<String, ProjectItem> ChildItems
        {
            get
            {
                return this.childItems;
            }

            private set
            {
                this.childItems = value;
                this.RaisePropertyChanged(nameof(this.ChildItems));
            }
        }

        /// <summary>
        /// Gets or sets an object to watch for file system changes under this directory.
        /// </summary>
        private FileSystemWatcher FileSystemWatcher { get; set; }

        /// <summary>
        /// Updates all project items under this directory.
        /// </summary>
        public override void Update()
        {
            foreach (KeyValuePair<String, ProjectItem> child in this.ChildItems)
            {
                child.Value?.Update();
            }
        }

        public bool Rename(String newProjectPathOrName)
        {
            try
            {
                this.StopWatchingForUpdates();

                if (!Path.IsPathRooted(newProjectPathOrName))
                {
                    newProjectPathOrName = Path.Combine(ProjectSettings.ProjectRoot, newProjectPathOrName);
                }

                Directory.Move(this.FullPath, newProjectPathOrName);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Unable to rename project", ex);

                return false;
            }
            finally
            {
                this.WatchForUpdates();
            }
        }

        /// <summary>
        /// Adds the specified project item to this directory.
        /// </summary>
        /// <param name="projectItem">The project item to add.</param>
        public void AddChild(ProjectItem projectItem)
        {
            try
            {
                projectItem.Parent = this;
                this.ChildItems.Add(projectItem.FullPath, projectItem);
                projectItem.Save();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Unable to add project item", ex);
            }
        }

        /// <summary>
        /// Removes the specified project item from this directory.
        /// </summary>
        /// <param name="projectItem">The project item to remove.</param>
        public void RemoveChild(ProjectItem projectItem)
        {
            try
            {
                if (this.ChildItems.ContainsKey(projectItem.FullPath))
                {
                    projectItem.Parent = null;
                    
                    if (projectItem is DirectoryItem)
                    {
                        Directory.Delete(projectItem.FullPath, recursive: true);
                    }
                    else
                    {
                        File.Delete(projectItem.FullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Unable to delete project item", ex);
            }
        }

        /// <summary>
        /// Gets the list of files in the directory Name passed.
        /// </summary>
        private void LoadAllChildProjectItems()
        {
            this.childItems?.Clear();

            try
            {
                IEnumerable<DirectoryInfo> subdirectories = Directory.GetDirectories(this.FullPath).Select(subdirectory => new DirectoryInfo(subdirectory));

                foreach (DirectoryInfo subdirectory in subdirectories)
                {
                    this.LoadProjectItem(subdirectory.FullName);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error fetching directories", ex);
            }

            try
            {
                foreach (FileInfo file in Directory.GetFiles(this.FullPath).Select(directoryFile => new FileInfo(directoryFile)))
                {
                    this.LoadProjectItem(file.FullName);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error fetching files", ex);
            }

            // Notify changes after everything finished loading
            this.RaisePropertyChanged(nameof(this.ChildItems));
        }

        /// <summary>
        /// Saves this directory to disk.
        /// </summary>
        public override void Save()
        {
            try
            {
                if (!Directory.Exists(this.FullPath))
                {
                    Directory.CreateDirectory(this.FullPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error creating directory within project.", ex);
            }
        }

        private ProjectItem LoadProjectItem(String projectItemPath)
        {
            if (Directory.Exists(projectItemPath))
            {
                try
                {
                    DirectoryItem childDirectory = DirectoryItem.FromDirectory(projectItemPath, this);

                    if (childDirectory != null)
                    {
                        this.childItems?.Add(childDirectory.FullPath, childDirectory);
                        this.ProjectItemAddedEvent?.Invoke(childDirectory);
                    }

                    return childDirectory;
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "Error loading directory", ex);
                }
            }
            else if (File.Exists(projectItemPath))
            {
                try
                {
                    ProjectItem projectItem = ProjectItem.FromFile(projectItemPath, this);

                    if (projectItem != null)
                    {
                        this.childItems?.Add(projectItem.FullPath, projectItem);
                        this.ProjectItemAddedEvent?.Invoke(projectItem);

                        return projectItem;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "Error reading project item", ex);
                }
            }

            Logger.Log(LogLevel.Error, "Unable to read project item from path: " + (projectItemPath ?? String.Empty));
            return null;
        }

        private void RemoveProjectItem(String projectItemPath)
        {
            if (this.ChildItems.ContainsKey(projectItemPath))
            {
                ProjectItem deletedProjectItem = this.ChildItems[projectItemPath];
                if (deletedProjectItem != null)
                {
                    deletedProjectItem.Parent = null;
                    this.ChildItems?.Remove(projectItemPath);
                    this.ProjectItemDeletedEvent?.Invoke(deletedProjectItem);
                }
            }
        }

        /// <summary>
        /// Initializes the filesystem watcher to listen for filesystem changes.
        /// </summary>
        private void WatchForUpdates()
        {
            this.StopWatchingForUpdates();

            this.FileSystemWatcher = new FileSystemWatcher(this.FullPath, "*.*")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };

            this.FileSystemWatcher.Deleted += OnFilesOrDirectoriesChanged;
            this.FileSystemWatcher.Changed += OnFilesOrDirectoriesChanged;
            this.FileSystemWatcher.Renamed += OnFilesOrDirectoriesChanged;
            this.FileSystemWatcher.Created += OnFilesOrDirectoriesChanged;
        }

        /// <summary>
        /// Cancels and removes the current filesystem watcher.
        /// </summary>
        private void StopWatchingForUpdates()
        {
            if (this.FileSystemWatcher != null)
            {
                this.FileSystemWatcher.Deleted -= OnFilesOrDirectoriesChanged;
                this.FileSystemWatcher.Changed -= OnFilesOrDirectoriesChanged;
                this.FileSystemWatcher.Renamed -= OnFilesOrDirectoriesChanged;
                this.FileSystemWatcher.Created -= OnFilesOrDirectoriesChanged;
                this.FileSystemWatcher = null;
            }
        }

        /// <summary>
        /// Method invoked when files or directories change under the project root.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="args">The filesystem change event args.</param>
        private void OnFilesOrDirectoriesChanged(Object source, FileSystemEventArgs args)
        {
            bool isDirectory = Directory.Exists(args.FullPath);

            switch (args.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    this.LoadProjectItem(args.FullPath);
                    break;
                case WatcherChangeTypes.Deleted:
                    this.RemoveProjectItem(args.FullPath);
                    break;
                case WatcherChangeTypes.Changed:
                    // TODO: Reread data from disc?
                    break;
                case WatcherChangeTypes.Renamed:
                    RenamedEventArgs renameArgs = args as RenamedEventArgs;

                    if (renameArgs != null)
                    {
                        this.RemoveProjectItem(renameArgs.OldFullPath);
                        this.LoadProjectItem(args.FullPath);
                    }
                    break;
            }

            this.RaisePropertyChanged(nameof(this.ChildItems));
        }
    }
    //// End class
}
//// End namespace