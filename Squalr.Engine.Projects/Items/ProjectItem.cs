namespace Squalr.Engine.Projects.Items
{
    using SharpDX.DirectInput;
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataTypes;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Common.Logging;
    using Squalr.Engine.Input.HotKeys;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    /// <summary>
    /// A base class for all project items that can be added to the project explorer.
    /// </summary>
    [KnownType(typeof(ProjectItem))]
    [KnownType(typeof(ScriptItem))]
    [KnownType(typeof(AddressItem))]
    [KnownType(typeof(InstructionItem))]
    [KnownType(typeof(PointerItem))]
    [KnownType(typeof(DotNetItem))]
    [KnownType(typeof(JavaItem))]
    [DataContract]
    public class ProjectItem : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// The name of this project item.
        /// </summary>
        [Browsable(false)]
        [DataMember]
        protected String name;

        /// <summary>
        /// The description of this project item.
        /// </summary>
        [Browsable(false)]
        [DataMember]
        protected String description;

        /// <summary>
        /// The unique identifier of this project item.
        /// </summary>
        [Browsable(false)]
        [DataMember]
        protected Guid guid;

        /// <summary>
        /// The hotkey associated with this project item.
        /// </summary>
        [Browsable(false)]
        protected Hotkey hotkey;

        /// <summary>
        /// A value indicating whether this project item has been activated.
        /// </summary>
        [Browsable(false)]
        protected Boolean isActivated;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItem" /> class.
        /// </summary>
        internal ProjectItem() : this( String.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItem" /> class.
        /// </summary>
        /// <param name="name">The name of the project item.</param>
        internal ProjectItem(String name)
        {
            // Bypass setters/getters to avoid triggering any view updates in constructor
            this.name = name ?? String.Empty;
            this.isActivated = false;
            this.guid = Guid.NewGuid();
            this.ActivationLock = new Object();
            this.HasAssociatedFileOrFolder = false;
        }

        public static ProjectItem FromFile(String filePath, DirectoryItem parent)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new Exception("File does not exist: " + filePath);
                }

                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    Type type = null;

                    switch (new FileInfo(filePath).Extension.ToLower())
                    {
                        case ScriptItem.Extension:
                            type = typeof(ScriptItem);
                            break;
                        case PointerItem.Extension:
                            type = typeof(PointerItem);
                            break;
                        case InstructionItem.Extension:
                            type = typeof(InstructionItem);
                            break;
                        case DotNetItem.Extension:
                            type = typeof(DotNetItem);
                            break;
                        case JavaItem.Extension:
                            type = typeof(JavaItem);
                            break;
                        default:
                            return null;
                    }

                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(type);

                    ProjectItem projectItem = serializer.ReadObject(fileStream) as ProjectItem;
                    projectItem.name = Path.GetFileNameWithoutExtension(filePath);
                    projectItem.Parent = parent;
                    projectItem.HasAssociatedFileOrFolder = true;

                    return projectItem;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error loading file", ex);
                throw ex;
            }
        }

        /// <summary>
        /// Saves this project item by serializing it to disk.
        /// </summary>
        public virtual void Save()
        {
            try
            {
                if (!this.HasAssociatedFileOrFolder)
                {
                    throw new Exception("Unable to save project item. Project item is not part of a project.");
                }

                using (FileStream fileStream = new FileStream(this.FullPath, FileMode.Create, FileAccess.Write))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(this.GetType());
                    serializer.WriteObject(fileStream, this);

                    this.HasAssociatedFileOrFolder = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error saving file", ex);
                throw ex;
            }
        }

        private void Rename(String newName)
        {
            if (!this.HasAssociatedFileOrFolder)
            {
                return;
            }

            String newPath = this.GetFilePathForName(this.MakeNameUnique(newName));

            // Attempt to move the existing associated file if possible
            try
            {
                File.Move(this.FullPath, newPath);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error moving existing project file during rename. The old file may still exist.", ex);
            }

            this.name = newPath;
            this.Save();
        }

        /// <summary>
        /// Occurs after a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the parent of this project item.
        /// </summary>
        public DirectoryItem Parent { get; set; }

        /// <summary>
        /// Gets or sets the file name for this project item.
        /// </summary>
        public virtual String Name
        {
            get
            {
                return this.name;
            }

            set
            {
                if (this.Name == value || !this.Name.IsValidFileName())
                {
                    return;
                }

                this.Rename(value);
                this.RaisePropertyChanged(nameof(this.Name));
            }
        }

        /// <summary>
        /// Gets or sets the description for this object.
        /// </summary>
        public virtual String Description
        {
            get
            {
                return this.description;
            }

            set
            {
                if (this.description == value)
                {
                    return;
                }

                this.description = value;
                this.RaisePropertyChanged(nameof(this.Description));
                this.Save();
            }
        }

        /// <summary>
        /// Gets or sets the hotkey for this project item.
        /// </summary>
        public virtual Hotkey HotKey
        {
            get
            {
                return this.hotkey;
            }

            set
            {
                if (this.hotkey == value)
                {
                    return;
                }

                this.hotkey = value;
                this.HotKey?.SetCallBackFunction(() => this.IsActivated = !this.IsActivated);
                this.RaisePropertyChanged(nameof(this.HotKey));
                this.Save();
            }
        }

        /// <summary>
        /// Gets or sets the unique identifier of this project item.
        /// </summary>
        [Browsable(false)]
        public Guid Guid
        {
            get
            {
                return this.guid;
            }

            set
            {
                if (this.guid == value)
                {
                    return;
                }

                this.guid = value;
                this.RaisePropertyChanged(nameof(this.Guid));
                this.Save();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not this item is activated.
        /// </summary>
        [Browsable(false)]
        public Boolean IsActivated
        {
            get
            {
                return this.isActivated;
            }

            set
            {
                lock (this.ActivationLock)
                {
                    if (this.isActivated == value)
                    {
                        return;
                    }

                    // Change activation state
                    Boolean previousValue = this.isActivated;
                    this.isActivated = value;
                    this.OnActivationChanged();

                    // Activation failed
                    if (this.isActivated == previousValue)
                    {
                        return;
                    }

                    this.RaisePropertyChanged(nameof(this.IsActivated));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating if this project item is enabled.
        /// </summary>
        [Browsable(false)]
        public virtual Boolean IsEnabled
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the display value to represent this project item.
        /// </summary>
        public virtual String DisplayValue
        {
            get
            {
                return String.Empty;
            }

            set
            {
            }
        }

        /// <summary>
        /// Gets the full path for this project item.
        /// </summary>
        [Browsable(false)]
        public String FullPath
        {
            get
            {
                return this.HasAssociatedFileOrFolder ? this.GetFilePathForName(this.Name) : this.Name;
            }
        }

        /// <summary>
        /// Gets the extension for this project item.
        /// </summary>
        public virtual String GetExtension()
        {
            return String.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether a file on disk is associated with this project item.
        /// </summary>
        protected Boolean HasAssociatedFileOrFolder { get; set; }

        /// <summary>
        /// Gets or sets a lock for activating project items.
        /// </summary>
        private Object ActivationLock { get; set; }

        /// <summary>
        /// Invoked when this object is deserialized.
        /// </summary>
        /// <param name="streamingContext">Streaming context.</param>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext streamingContext)
        {
            if (this.Guid == null || this.Guid == Guid.Empty)
            {
                this.guid = Guid.NewGuid();
            }

            this.ActivationLock = new Object();
        }

        /// <summary>
        /// Updates this project item. Resolves addresses and values.
        /// </summary>
        public virtual void Update()
        {
        }

        /// <summary>
        /// Clones the project item.
        /// </summary>
        /// <returns>The clone of the project item.</returns>
        public virtual ProjectItem Clone()
        {
            // Serialize this project item to a byte array
            using (MemoryStream serializeMemoryStream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ProjectItem));
                serializer.WriteObject(serializeMemoryStream, this);

                // Deserialize the array to clone the item
                using (MemoryStream deserializeMemoryStream = new MemoryStream(serializeMemoryStream.ToArray()))
                {
                    ProjectItem result = serializer.ReadObject(deserializeMemoryStream) as ProjectItem;

                    result?.ResetGuid();

                    return result;
                }
            }
        }

        /// <summary>
        /// Updates the hotkey, bypassing setters to avoid triggering view updates.
        /// </summary>
        /// <param name="hotkey">The hotkey for this project item.</param>
        public void LoadHotkey(Hotkey hotkey)
        {
            this.hotkey = hotkey;

            this.HotKey?.SetCallBackFunction(() => this.IsActivated = !this.IsActivated);
        }

        public void Dispose()
        {
            this.HotKey?.Dispose();
        }

        /// <summary>
        /// Randomizes the guid of this project item.
        /// </summary>
        public void ResetGuid()
        {
            this.Guid = Guid.NewGuid();
        }

        /// <summary>
        /// Event received when a key is released.
        /// </summary>
        /// <param name="key">The key that was released.</param>
        public void OnKeyPress(Key key)
        {
        }

        /// <summary>
        /// Event received when a key is down.
        /// </summary>
        /// <param name="key">The key that is down.</param>
        public void OnKeyRelease(Key key)
        {
        }

        /// <summary>
        /// Event received when a key is down.
        /// </summary>
        /// <param name="key">The key that is down.</param>
        public void OnKeyDown(Key key)
        {
        }

        /// <summary>
        /// Event received when a set of keys are down.
        /// </summary>
        /// <param name="pressedKeys">The down keys.</param>
        public void OnUpdateAllDownKeys(HashSet<Key> pressedKeys)
        {
            if (this.HotKey is KeyboardHotkey)
            {
                KeyboardHotkey keyboardHotkey = this.HotKey as KeyboardHotkey;
            }
        }

        /// <summary>
        /// Indicates that a given property in this project item has changed.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void RaisePropertyChanged(String propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Deactivates this item without triggering the <see cref="OnActivationChanged" /> function.
        /// </summary>
        protected void ResetActivation()
        {
            lock (this.ActivationLock)
            {
                this.isActivated = false;
                this.RaisePropertyChanged(nameof(this.IsActivated));
            }
        }

        /// <summary>
        /// Called when the activation state changes.
        /// </summary>
        protected virtual void OnActivationChanged()
        {
        }

        /// <summary>
        /// Overridable function indicating if this script can be activated.
        /// </summary>
        /// <returns>True if the script can be activated, otherwise false.</returns>
        protected virtual Boolean IsActivatable()
        {
            return true;
        }

        /// <summary>
        /// Resolves the name conflict for this unassociated project item.
        /// </summary>
        /// <returns>The resolved name, which appends a number at the end of the name to ensure uniqueness.</returns>
        private String MakeNameUnique(String newName)
        {
            if (!this.HasAssociatedFileOrFolder || this.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                return newName;
            }

            String newFilePath = this.GetFilePathForName(newName);

            try
            {
                if (this.Parent.ChildItems.Any(childItem => childItem.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    // Find all files that match the pattern of {newfilename #}, and extract the numbers
                    IEnumerable<Int32> neighboringNumberedFiles = this.Parent.ChildItems
                        .Where(childItem => childItem.Name.StartsWith(newName, StringComparison.OrdinalIgnoreCase))
                        .Select(childItem => childItem.Name.Substring(0, newName.Length).Trim())
                        .Where(childSuffix => SyntaxChecker.CanParseValue(DataTypeBase.Int32, childSuffix))
                        .Select(childSuffix => (Int32)Conversions.ParsePrimitiveStringAsPrimitive(DataTypeBase.Int32, childSuffix));

                    Int32 neighboringNumberedFileCount = neighboringNumberedFiles.Count();
                    IEnumerable<Int32> missingNumbersInSequence = Enumerable.Range(0, neighboringNumberedFileCount).Except(neighboringNumberedFiles);

                    // Find the first gap in the numbers. If no gap, just take the next number in the sequence
                    Int32 numberToAppend = missingNumbersInSequence.IsEmpty() ? neighboringNumberedFileCount + 1 : missingNumbersInSequence.First();
                    String suffix = numberToAppend == 0 ? String.Empty : " " + numberToAppend.ToString();
                    String resolvedName = Path.Combine(ProjectSettings.ProjectRoot, newName + suffix);

                    newName = resolvedName;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error resolving conflicting project name.", ex);
            }

            return newName;
        }

        String GetFilePathForName(String name)
        {
            return Path.Combine(this.Parent?.FullPath ?? ProjectSettings.ProjectRoot, name + this.GetExtension());
        }
    }
    //// End class
}
//// End namespace