namespace Squalr.Source.Mvvm.Converters
{
    using Squalr.Engine.Common.DataStructures;
    using Squalr.Engine.Common.Logging;
    using Squalr.Engine.Projects.Items;
    using Squalr.Source.ProjectExplorer.ProjectItems;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows.Data;

    public class ProjectItemDirectoryConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (value as IEnumerable<ProjectItem> != null)
            {
                FullyObservableCollection<ProjectItemView> projectItems = new FullyObservableCollection<ProjectItemView>();

                foreach (ProjectItem projectItem in value as IEnumerable<ProjectItem>)
                {
                    ProjectItemView projectItemView = projectItem.MappedView as ProjectItemView;

                    if (projectItemView == null)
                    {
                        projectItemView = this.ConvertToProjectItemView(projectItem);
                        projectItem.MappedView = projectItemView;
                    }

                    projectItems.Add(projectItemView);
                }

                return projectItems;
            }

            return null;
        }

        public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private ProjectItemView ConvertToProjectItemView(ProjectItem projectItem)
        {
            switch (projectItem)
            {
                case DirectoryItem _ when projectItem is DirectoryItem:
                    return new DirectoryItemView(projectItem as DirectoryItem);
                case ProjectItem _ when projectItem is PointerItem:
                    return new PointerItemView(projectItem as PointerItem);
                case ProjectItem _ when projectItem is ScriptItem:
                    return new ScriptItemView(projectItem as ScriptItem);
                case ProjectItem _ when projectItem is InstructionItem:
                    return new InstructionItemView(projectItem as InstructionItem);
                case ProjectItem _ when projectItem is DotNetItem:
                    return new DotNetItemView(projectItem as DotNetItem);
                case ProjectItem _ when projectItem is JavaItem:
                    return new JavaItemView(projectItem as JavaItem);
                case ProjectItem _ when projectItem is DolphinAddressItem:
                    return new DolphinItemView(projectItem as DolphinAddressItem);
                default:
                    Logger.Log(LogLevel.Error, "Unknown project item type");
                    return null;
            }
        }
    }
    //// End class
}
//// End namespace