using System;
using System.Reflection;
using UnityEditor;

namespace Piglet
{
    /// <summary>
    /// The various fields and methods that need to be accessed by
    /// reflection, in order to implement drag-and-drop in the
    /// Project Browser (see ProjectBrowserDragAndDrop). 
    /// </summary>
    public static class ProjectBrowserReflection
    {
        public static Type ProjectBrowserType
        {
            get
            {
                return Assembly.GetAssembly(typeof(Editor))
                    .GetType("UnityEditor.ProjectBrowser");
            }
        }

        public static FieldInfo LastInteractedProjectBrowser
        {
            get
            {
                var projectBrowserType = ProjectBrowserType;
                return projectBrowserType.GetField(
                    "s_LastInteractedProjectBrowser",
                    BindingFlags.Static | BindingFlags.Public);
            }
        }

        public static MethodInfo GetActiveFolderPath
        {
            get
            {
                var projectBrowserType = ProjectBrowserType;
                return projectBrowserType.GetMethod("GetActiveFolderPath",
                    BindingFlags.InvokeMethod
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance);
            }
        }

        public static FieldInfo ListAreaRect
        {
            get
            {
                return ProjectBrowserType.GetField(
                    "m_ListAreaRect",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);
            }
        }

        public static FieldInfo ViewMode
        {
            get
            {
                return ProjectBrowserType.GetField(
                    "m_ViewMode",
                    BindingFlags.NonPublic
                    | BindingFlags.Instance);
            }
        }

        public static FieldInfo SearchFilter
        {
            get
            {
                return ProjectBrowserType.GetField(
                    "m_SearchFilter",
                    BindingFlags.NonPublic
                    | BindingFlags.Instance);
            }
        }
        
        public static MethodInfo IsSearching
        {
            get
            {
                var searchFilterType = Assembly.GetAssembly(typeof(Editor))
                    .GetType("UnityEditor.SearchFilter");
                return searchFilterType.GetMethod("IsSearching",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }
    }
}