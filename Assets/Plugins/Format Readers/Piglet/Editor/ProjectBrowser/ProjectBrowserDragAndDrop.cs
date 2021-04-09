#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Piglet
{
    /// <summary>
    /// Aside from ScriptedImporter, Unity does not provide an API for implementing
    /// custom drag-and-drop behaviour in the Project Browser.
    ///
    /// This class uses Unity's `EditorApplication.projectWindowItemOnGUI` event,
    /// which gets called for every visible file/folder in the Project Browser,
    /// to implement a hook for custom drag-and-drop events.
    ///
    /// The main data structure used by this code is a list that maps visible items
    /// in the Project Browser to screen rects. This mapping is rebuilt at the
    /// beginning of each GUI event (e.g. `EventType.Repaint`) and is used to
    /// determine the target project file/folder where the external files
    /// have been dropped.
    /// </summary>
    public static class ProjectBrowserDragAndDrop
    {
        /// <summary>
        /// Function prototype for user-specified drag-and-drop callbacks.
        /// </summary>
        /// <param name="targetPath">
        /// Absolute path for target folder/file where items were dropped.
        /// This folder/file is always located under the "Assets"
        /// folder of the currently opened project.
        /// </param>
        /// <param name="droppedPaths">
        /// Absolute paths of folders/files that were dropped. These
        /// folders/files may be located anywhere on the local file
        /// system.
        /// </param>
        public delegate void DragAndDropCallback(
            string targetPath, string[] droppedPaths);

        /// <summary>
        /// User-registered callbacks for handling drag-and-drop
        /// events in the Project Browser.
        /// </summary>
        public static DragAndDropCallback OnDragAndDrop;

        /// <summary>
        /// Maps the GUID for a row item (file or folder)
        /// to its screen rectangle.
        /// </summary>
        private struct ItemRect
        {
            public string guid;
            public Rect rect;
        }

        private static EventType _currentEventType;

        /// <summary>
        /// Mappings from GUID -> screen rectangle that were
        /// built while processing the previous UI Event.
        /// </summary>
        private static List<ItemRect> _itemRectsLastFrame;

        /// <summary>
        /// Mappings from GUID -> screen rectangle that have
        /// been built thus far, for the current UI Event.
        /// </summary>
        private static List<ItemRect> _itemRectsThisFrame;

        /// <summary>
        /// Describes the target position of a drag-and-drop operation relative to
        /// a Project Browser item (i.e. folder or file). During a drag-and-drop
        /// operation, the user may position the mouse cursor between two
        /// vertically adjacent items and a blue horizontal line will appear,
        /// indicating that the files will be dropped *between* those two items.
        /// The effect of dropping files between two adjacent items depends on
        /// whether the items are siblings in the tree or have a parent-child
        /// relationship.
        /// </summary>
        private enum DropPosition
        {
            AboveItem,
            UponItem,
            BelowItem
        };

        [InitializeOnLoadMethod]
        static void Setup()
        {
            // Note: EditorApplication.projectWindowItemOnGUI is invoked for each
            // item (i.e. file or folder) that is currently visible in the Project
            // Browser, in both the left pane (folder tree) and right pane (file
            // list). This callback provides a hook into the UnityEditor behaviour
            // that we need to implement custom drag-and-drop behaviour.
            EditorApplication.projectWindowItemOnGUI += ProjectItemOnGUI;

            _currentEventType = EventType.Used;
            _itemRectsLastFrame = new List<ItemRect>();
            _itemRectsThisFrame = new List<ItemRect>();
        }

        /// <summary>
        /// Callback that is invoked for each item (file or folder) that
        /// is currently visible in the left/right panes of the Project Browser.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="selectionRect"></param>
        private static void ProjectItemOnGUI(string guid, Rect selectionRect)
        {
            Event @event = Event.current;

            // Get a reference to the Project Browser window that the user last
            // interacted with. (It is possible to have multiple Project Browsers
            // open at the same time.)
            //
            // Note: There is no Unity API for doing this, so we have to use
            // reflection to access private/internal members.

            var projectBrowser = ProjectBrowserExtensions
                .GetLastInteractedProjectBrowser();

            // Get the position of the Project Browser window, in screen coordinates.

            Vector2 projectBrowserPosition = ((EditorWindow)projectBrowser).position.position;

            // For each event, we build a mapping of items -> rects. This mapping
            // is needed to determine if our drop target is located inside a tree
            // area or a list area, and to determine if the drop target is outside
            // of any items (e.g. dropping into an empty area of the files pane).
            //
            // I use `@event.rawType` here instead of `@event.type` because
            // `@event.type` is set to `EventType.Ignore` when the mouse is located
            // outside of the tree/list area containing the current item
            // (identified by `guid`).

            if (@event.rawType != _currentEventType)
            {
                _currentEventType = @event.rawType;

                List<ItemRect> temp = _itemRectsLastFrame;
                _itemRectsLastFrame = _itemRectsThisFrame;
                _itemRectsThisFrame = temp;
                _itemRectsThisFrame.Clear();
            }

            Vector2 localMousePosition = Event.current.mousePosition;
            Vector2 globalMousePosition = GUIUtility.GUIToScreenPoint(localMousePosition);

            // Record guid => screen rect mapping for the current item.

            Rect globalRect = new Rect(
                GUIUtility.GUIToScreenPoint(selectionRect.position),
                selectionRect.size);

            // Note: Non-assets in the Project Browser (e.g. "Favorites", "Packages")
            // have empty GUIDs.

            if (guid.Length > 0)
                _itemRectsThisFrame.Add(new ItemRect { guid = guid, rect = globalRect });

            // Note: When file(s) are dragged onto the right pane, the left pane
            // will have `@event.type == EventType.Ignore` and `@event.rawType ==
            // EventType.DragPerform`. (And likewise for items in the right pane
            // when file(s) are dragged onto the left pane.) I'm not 100% sure why
            // this happens, but I think it because the mouse events are outside
            // the active GUI clip area (e.g. `GUI.BeginClip()`,
            // `GUI.BeginScrollRect()`).

            if (@event.type == EventType.DragPerform || @event.rawType == EventType.DragPerform)
            {
                // Determine the target directory for the GLTF import (i.e.
                // directory where the model prefab and associated assets will be
                // created), based on the drag-and-drop target in the Project
                // Browser.
                //
                // If the GLTF file(s) are dragged onto a directory, import into
                // that directory. If the GLTF file(s) are dragged onto a file,
                // import into the parent directory of that file.  If the GLTF
                // file(s) are dragged onto an empty area, import into the
                // directory that is currently selected in the left pane.

                string dragTargetGuid = null;
                DropPosition dropPosition = DropPosition.UponItem;
                for (int i = 0; i < _itemRectsLastFrame.Count; ++i)
                {
                    Rect itemRect = _itemRectsLastFrame[i].rect;
                    string itemGuid = _itemRectsLastFrame[i].guid;

                    if (itemRect.Contains(globalMousePosition))
                    {
                        ProjectBrowserExtensions.ItemType itemType
                            = ProjectBrowserExtensions.GetItemType(itemRect);

                        string itemPath = UnityPathUtil.NormalizePathSeparators(
                            AssetDatabase.GUIDToAssetPath(itemGuid));

                        // TreeItem areas support dropping files between
                        // vertically adjacent folders/files (as indicated by a
                        // horizontal blue line in the UI), whereas
                        // list areas do not.
                        if (itemType == ProjectBrowserExtensions.ItemType.TreeItem)
                        {
                            // The height of the target region at the top/bottom of
                            // an item rect that corresponds to dropping files
                            // between items.
                            //
                            // This value is hardcoded to match Unity's internal
                            // value for `TreeViewGUI.k_HalfDropBetweenHeight`. For
                            // Unity's own implementation of this logic, see
                            // `TreeViewDragging.TryGetDropPosition`.
                            const float halfDropBetweenHeight = 4f;

                            if (globalMousePosition.y <= itemRect.yMin + halfDropBetweenHeight)
                                dropPosition = DropPosition.AboveItem;
                            else if (globalMousePosition.y >= itemRect.yMax - halfDropBetweenHeight)
                                dropPosition = DropPosition.BelowItem;
                            else
                                dropPosition = DropPosition.UponItem;
                        }

                        // The easy case: we are dropping file(s) directly onto a file/folder
                        // (rather than between two vertically adjacent files/folders).

                        if (dropPosition == DropPosition.UponItem)
                        {
                            dragTargetGuid = itemGuid;
                            break;
                        }

                        if (dropPosition == DropPosition.BelowItem)
                        {
                            // Special case: If we are dropping below the last item
                            // in a tree/list, then the drop target should be the
                            // parent folder of the target item.
                            //
                            // Note: In "Two Column Layout", `_itemRectsLastFrame`
                            // stores both tree items and list items in the same
                            // list. As a result, the current item (i) may be in
                            // the left pane (i.e. folder tree) while the next item
                            // (i + 1) is in the right pane (i.e. files list). In
                            // this case, we are not really dropping between items,
                            // but we are rather dropping after the last item in
                            // the folder tree.

                            ProjectBrowserExtensions.ItemType nextItemType
                                = ProjectBrowserExtensions.ItemType.TreeItem;

                            if (i + 1 < _itemRectsLastFrame.Count)
                            {
                                Rect nextItemRect = _itemRectsLastFrame[i + 1].rect;
                                nextItemType = ProjectBrowserExtensions
                                    .GetItemType(nextItemRect);
                            }

                            if (i + 1 >= _itemRectsLastFrame.Count
                                || nextItemType != itemType) {

                                // Special case: user is not allowed to drop into the
                                // parent folder of "Assets", so do nothing.
                                if (itemPath == "Assets")
                                    return;

                                string itemParentPath = Path.GetDirectoryName(itemPath);
                                dragTargetGuid = AssetDatabase.AssetPathToGUID(itemParentPath);

                                break;
                            }

                            // If the next item is a child file/folder of the
                            // current item, then the current item should be the
                            // drag target.  Otherwise, the current and next
                            // items are siblings and the drag target should
                            // be their shared parent folder.

                            string nextItemGuid = _itemRectsLastFrame[i + 1].guid;

                            string nextItemPath = UnityPathUtil.NormalizePathSeparators(
                                AssetDatabase.GUIDToAssetPath(nextItemGuid));

                            if (UnityPathUtil.GetParentDir(nextItemPath) == itemPath)
                            {
                                dragTargetGuid = itemGuid;
                                break;
                            }
                            else
                            {
                                dragTargetGuid = AssetDatabase.AssetPathToGUID(
                                    UnityPathUtil.GetParentDir(itemPath));
                                break;
                            }
                        }

                        if (dropPosition == DropPosition.AboveItem)
                        {
                            // If we are dropping above the topmost item in a
                            // tree/list.
                            //
                            // Note: This should never happen except when the user
                            // is drops files above the root "Assets" folder.  In
                            // the case that a subset of items is currently being
                            // shown in a scroll window, the tree/list window will
                            // automatically scroll up when the user hovers the
                            // mouse above the top visible item.

                            if (i == 0)
                                return;

                            // If the prev item is parent folder of the
                            // current item, then the previous item should be the
                            // drag target.  Otherwise, the current and previous
                            // items are siblings and the drag target should
                            // be their shared parent folder.

                            string prevItemGuid = _itemRectsLastFrame[i - 1].guid;

                            string prevItemPath = UnityPathUtil.NormalizePathSeparators(
                                AssetDatabase.GUIDToAssetPath(prevItemGuid));

                            if (UnityPathUtil.GetParentDir(itemPath) == prevItemPath)
                            {
                                dragTargetGuid = prevItemGuid;
                                break;
                            }
                            else
                            {
                                dragTargetGuid = AssetDatabase.AssetPathToGUID(
                                    UnityPathUtil.GetParentDir(itemPath));
                                break;
                            }
                        }

                    }

                }

                if (dragTargetGuid == null)
                {
                    // The user dragged the GLTF file(s) onto an empty area of the
                    // Project Browser. Use the currently selected folder in the
                    // left pane (i.e. folder tree) as the import directory.

                    dragTargetGuid = AssetDatabase.AssetPathToGUID(
                        ProjectBrowserExtensions.GetSelectedProjectFolder());
                }

                string dragTargetProjectPath = AssetDatabase.GUIDToAssetPath(dragTargetGuid);
                string dragTargetPath = UnityPathUtil.GetAbsolutePath(dragTargetProjectPath);

                // Invoke user-defined drag-and-drop callbacks

                OnDragAndDrop?.Invoke(dragTargetPath, DragAndDrop.paths);

            }
        }
    }
}

#endif
