namespace Squalr.Engine.Projects
{
    using Squalr.Engine.Projects.Items;
    using System;
    using System.IO;

    /// <summary>
    /// Defines a Squalr project. This is the root directory that contains all other project items.
    /// </summary>
    public class Project : DirectoryItem
    {
        /// <summary>
        /// Creates a new project from the given path or project name. If given as a project name, Squalr will use the user settings to decide where to place the folder.
        /// </summary>
        /// <param name="projectFilePathOrName">The project path, or the project name.</param>
        public Project(String projectFilePathOrName) : base(Project.ToDirectory(projectFilePathOrName), null)
        {
        }

        /// <summary>
        /// Converts a project name into a project path, if necessary.
        /// </summary>
        /// <param name="projectFilePathOrName">The project path, or the project name.</param>
        /// <returns></returns>
        private static String ToDirectory(String projectFilePathOrName)
        {
            if (!Path.IsPathRooted(projectFilePathOrName))
            {
                projectFilePathOrName = Path.Combine(ProjectSettings.ProjectRoot, projectFilePathOrName);
            }

            return projectFilePathOrName;
        }
    }
    //// End class
}
//// End namespace