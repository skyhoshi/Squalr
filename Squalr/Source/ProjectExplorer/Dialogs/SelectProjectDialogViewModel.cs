﻿using CommunityToolkit.Mvvm.Input;

namespace Squalr.Source.ProjectExplorer.Dialogs
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using Squalr.Engine.Common.Logging;
    using Squalr.Properties;
    using Squalr.View.Dialogs;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;

    public class SelectProjectDialogViewModel : ObservableObject
    {
        /// <summary>
        /// Singleton instance of the <see cref="SelectProjectDialogViewModel" /> class.
        /// </summary>
        private static Lazy<SelectProjectDialogViewModel> selectProjectDialogViewModelInstance = new Lazy<SelectProjectDialogViewModel>(
                () => { return new SelectProjectDialogViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        private String searchTerm;

        private String selectedProject;

        private String newProjectName;

        private SelectProjectDialogViewModel() : base()
        {
            this.UpdateSelectedProjectCommand = new RelayCommand<List<string>>((selectedItems) => this.SelectedProject = (selectedItems as IList)?.Cast<String>()?.FirstOrDefault());
            this.OpenProjectCommand = new RelayCommand<string>((project) => this.OpenProject(project));
            this.RenameProjectCommand = new RelayCommand<string>((project) => this.RenameProject(project));
            this.RenameSelectedProjectCommand = new RelayCommand(() => this.RenameProject(this.SelectedProject));
            this.NewProjectCommand = new RelayCommand(() => this.CreateNewProject());
            this.DeleteProjectCommand = new RelayCommand<string>((project) => this.DeleteProject(project));
            this.DeleteSelectedProjectCommand = new RelayCommand(() => this.DeleteProject(this.SelectedProject));
        }

        /// <summary>
        /// Gets the command to update the current project selection.
        /// </summary>
        public ICommand UpdateSelectedProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to open a project.
        /// </summary>
        public ICommand OpenProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to rename the given project.
        /// </summary>
        public ICommand RenameProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to rename the selected project.
        /// </summary>
        public ICommand RenameSelectedProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to create a new project.
        /// </summary>
        public ICommand NewProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to delete a project.
        /// </summary>
        public ICommand DeleteProjectCommand { get; private set; }

        /// <summary>
        /// Gets the command to delete the selected project.
        /// </summary>
        public ICommand DeleteSelectedProjectCommand { get; private set; }

        /// <summary>
        /// Gets a list of projects in the project root.
        /// </summary>
        public List<String> Projects
        {
            get
            {
                return Directory.EnumerateDirectories(SettingsViewModel.GetInstance().ProjectRoot).Select(path => new DirectoryInfo(path).Name).ToList();
            }
        }

        /// <summary>
        /// Gets a list of projects in the project root, filtered by the current search term.
        /// </summary>
        public List<String> FilteredProjects
        {
            get
            {
                return this.ProjectSearchTerm == null ? this.Projects : this.Projects.Select(project => project).Where(project => project.ToLower().Contains(this.ProjectSearchTerm.ToLower())).ToList();
            }
        }

        /// <summary>
        /// Gets or sets the project search term to filter project results.
        /// </summary>
        public String ProjectSearchTerm
        {
            get
            {
                return this.searchTerm;
            }

            set
            {
                this.searchTerm = value;
                this.OnPropertyChanged(nameof(this.ProjectSearchTerm));
                this.OnPropertyChanged(nameof(this.FilteredProjects));
            }
        }

        /// <summary>
        /// Gets or sets the current selected project.
        /// </summary>
        public String SelectedProject
        {
            get
            {
                return this.selectedProject;
            }

            set
            {
                this.selectedProject = value;
                this.OnPropertyChanged(nameof(this.SelectedProject));
            }
        }

        public String NewProjectName
        {
            get
            {
                return this.newProjectName;
            }

            set
            {
                this.newProjectName = value;
                this.OnPropertyChanged(nameof(this.NewProjectName));
                this.OnPropertyChanged(nameof(this.IsProjectNameValid));
                this.OnPropertyChanged(nameof(this.NewProjectNameStatus));
            }
        }

        public Boolean IsProjectNameValid
        {
            get
            {
                if (String.IsNullOrWhiteSpace(this.NewProjectName) || this.NewProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    Directory.Exists(Path.Combine(SettingsViewModel.GetInstance().ProjectRoot, this.NewProjectName)))
                {
                    return false;
                }

                return true;
            }
        }

        public String NewProjectNameStatus
        {
            get
            {
                if (this.NewProjectName != null)
                {
                    if (this.NewProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        return "Invalid project name";
                    }
                    else if (Directory.Exists(Path.Combine(SettingsViewModel.GetInstance().ProjectRoot, this.NewProjectName)) && !String.IsNullOrWhiteSpace(this.NewProjectName))
                    {
                        return "Project already exists";
                    }
                }

                return String.Empty;
            }
        }

        private SelectProjectDialog SelectProjectDialog { get; set; }

        /// <summary>
        /// Gets a singleton instance of the <see cref="SelectProjectDialogViewModel" /> class.
        /// </summary>
        /// <returns>A singleton instance of the class.</returns>
        public static SelectProjectDialogViewModel GetInstance()
        {
            return SelectProjectDialogViewModel.selectProjectDialogViewModelInstance.Value;
        }

        public void ShowDialog(Window owner, Action<String> projectPathCallback)
        {
            this.SelectedProject = String.Empty;
            this.SelectProjectDialog = new SelectProjectDialog() { Owner = owner };

            if (this.SelectProjectDialog.ShowDialog() == true)
            {
                String projectPath = Path.Combine(SettingsViewModel.GetInstance().ProjectRoot, this.SelectedProject);

                if (!String.IsNullOrEmpty(projectPath))
                {
                    projectPathCallback?.Invoke(projectPath);
                }
            }
        }

        private void OpenProject(String project)
        {
            this.SelectedProject = project;
            this.SelectProjectDialog.SelectProject(this.SelectedProject);
        }

        private void RenameProject(String project)
        {
            RenameProjectDialogViewModel renameProjectDialog = RenameProjectDialogViewModel.GetInstance();

            if (renameProjectDialog.ShowDialog(this.SelectProjectDialog, project) == true)
            {
                this.OnPropertyChanged(nameof(this.Projects));
                this.OnPropertyChanged(nameof(this.FilteredProjects));
            }
        }

        private void CreateNewProject()
        {
            CreateProjectDialogViewModel createProjectDialog = CreateProjectDialogViewModel.GetInstance();

            if (createProjectDialog.ShowDialog(this.SelectProjectDialog) == true)
            {
                this.OpenProject(createProjectDialog.NewProjectName);
            }
        }

        private void DeleteProject(String project)
        {
            if (String.IsNullOrEmpty(project))
            {
                Logger.Log(LogLevel.Warn, "No project was selected to delete.");
                return;
            }

            String projectPath = Path.Combine(SettingsViewModel.GetInstance().ProjectRoot, project);

            if (!Directory.Exists(projectPath))
            {
                Logger.Log(LogLevel.Error, "Project does not exist on disk.");
                return;
            }

            if (DeleteProjectDialogViewModel.GetInstance().ShowDialog(this.SelectProjectDialog, project))
            {
                this.OnPropertyChanged(nameof(this.Projects));
                this.OnPropertyChanged(nameof(this.FilteredProjects));
            }
        }
    }
    //// End class
}
//// End namespace