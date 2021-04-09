using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// Reflection-based helper methods used to implement drag-and-drop
    /// of external files in the Project Browser (see
    /// ProjectBrowserDragAndDrop).
    /// </summary>
    public class ProjectBrowserExtensions
    {
        /// <summary>
        /// The current layout mode of a Project Browser (selected
        /// in the hamburger menu in the right corner of the Project
        /// window).
        /// </summary>
        public enum ProjectBrowserLayoutMode
        {
            OneColumn,
            TwoColumns
        };

        /// <summary>
        /// The type of an item in the Project Browser, as determined by whether it
        /// is contained within a tree area (e.g. folder tree) or list area (e.g.
        /// files list, search results list). It is necessary to differentiate tree
        /// items and list itmes because the trees and lists support different
        /// drag-and-drop operations. In particular, tree areas allow dropping
        /// files between vertically adjacent folders/files, whereas lists do not.
        /// </summary>
        public enum ItemType
        {
            TreeItem,
            ListItem
        };

        /// <summary>
        /// Get a reference to the Project Browser window that the user last
        /// interacted with. (It is possible to have multiple Project Browser
        /// windows open at the same time.)
        /// </summary>
        public static object GetLastInteractedProjectBrowser()
        {
            return ProjectBrowserReflection
                .LastInteractedProjectBrowser
                .GetValue(null);
        }

        /// <summary>
        /// Return the currently selected folder in the folder tree.
        ///
        /// Note: Using `Selection.assetGUIDs` to determine
        /// the selected folder doesn't work here because the selection
        /// is cleared whenever the Project Browser loses focus.
        /// This is a problem because the Project Browser does not
        /// necessarily have focus when a user drags files onto it. Another
        /// problem is that when the user selects items in both the left
        /// and right Project Browser panes, `Selection.assetGUIDs` will only
        /// contain the selected items in the right pane. 
        /// </summary>
        public static string GetSelectedProjectFolder()
        {
            object projectBrowser = GetLastInteractedProjectBrowser();
            return ProjectBrowserReflection
                .GetActiveFolderPath
                .Invoke(projectBrowser, null) as string;
        }
        
        
        /// <summary>
        /// Get the screen rect for the list area of the given Project
        /// Browser window.
        ///
        /// In "One Column Layout", the list area occupies the same area as the
        /// tree area, and is only active when a search filter is active. In
        /// "Two Column Layout", the list area is always active and occupies the
        /// right pane.
        /// </summary>
        public static Rect GetListAreaRect()
        {
            var projectBrowser = GetLastInteractedProjectBrowser();

            Rect listAreaRect = (Rect)ProjectBrowserReflection
                .ListAreaRect.GetValue(projectBrowser);

            // convert position from local coords (relative to current OnGUI
            // window) to global screen coords
            listAreaRect.position = GUIUtility.GUIToScreenPoint(listAreaRect.position);

            return listAreaRect;
        }

        
        /// <summary>
        /// Determine whether the project browser is in "One Column Layout"
        /// or "Two Column Layout".
        /// </summary>
        public static ProjectBrowserLayoutMode GetProjectBrowserLayoutMode()
        {
            var projectBrowser = GetLastInteractedProjectBrowser();
            
            var viewMode = ProjectBrowserReflection
                .ViewMode.GetValue(projectBrowser);

            if (viewMode.ToString() == "OneColumn")
                return ProjectBrowserLayoutMode.OneColumn;
            else
                return ProjectBrowserLayoutMode.TwoColumns;
        }

        /// <summary>
        /// Return true if a search filter is active in the
        /// project browser.
        /// </summary>
        public static bool IsSearchFilterActive()
        {
            var projectBrowser = GetLastInteractedProjectBrowser();
            
            var searchFilter = ProjectBrowserReflection
                .SearchFilter.GetValue(projectBrowser);

            return (bool)ProjectBrowserReflection
                .IsSearching.Invoke(searchFilter, null);
        }

        /// <summary>
        /// Return the item type (tree item or list item) for the given rect (given
        /// in screen coordinates). It is valuable to know which type of UI we are
        /// working with because trees and lists support different drag-and-drop
        /// behaviours. In particular, trees (e.g. folder tree) allow for dropping
        /// files between vertically adjacent items (as indicated by a blue
        /// horizontal line in the UI), whereas list areas do not allow dropping
        /// between items.
        /// </summary>
        public static ItemType GetItemType(Rect rect)
        {
            ProjectBrowserLayoutMode layoutMode
                = GetProjectBrowserLayoutMode();
            
            Rect listAreaRect = GetListAreaRect();

            if (layoutMode == ProjectBrowserLayoutMode.OneColumn)
            {
                return IsSearchFilterActive()
                    ? ItemType.ListItem : ItemType.TreeItem;
            }
            else
            {
                return listAreaRect.Contains(rect)
                    ? ItemType.ListItem : ItemType.TreeItem;
            }
        }

    }
}