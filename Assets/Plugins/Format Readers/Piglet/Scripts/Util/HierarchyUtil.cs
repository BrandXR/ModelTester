using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Piglet
{
    public static class HierarchyUtil
    {
        /// <summary>
        /// Calculate the world space axis-aligned bounding box
        /// for a hierarchy of game objects containing zero or more meshes.
        /// If the hierarchy does not contain any meshes, then
        /// return value will be null (i.e. Bounds.HasValue == false).
        /// </summary>
        public static Bounds? GetRendererBoundsForHierarchy(GameObject o)
        {
            Bounds? bounds = null;

            foreach (var result in GetRendererBoundsForHierarchyEnum(o))
                bounds = result;

            return bounds;
        }

        /// <summary>
        /// Calculate the world space axis-aligned bounding box
        /// for a hierarchy of game objects containing zero or more meshes.
        /// If the hierarchy does not contain any meshes, then
        /// return value will be null (i.e. Bounds.HasValue == false).
        /// </summary>
        public static IEnumerable<Bounds?> GetRendererBoundsForHierarchyEnum(GameObject o)
        {
            var stopwatch = new Stopwatch();
            foreach (var bounds in GetRendererBoundsForHierarchyEnum(o, stopwatch))
                yield return bounds;
        }

        /// <summary>
        /// Calculate the world space axis-aligned bounding box
        /// for a hierarchy of game objects containing zero or more meshes.
        /// If the hierarchy does not contain any meshes, then
        /// return value will be null (i.e. Bounds.HasValue == false).
        /// </summary>
        public static IEnumerable<Bounds?> GetRendererBoundsForHierarchyEnum(
            GameObject o, Stopwatch stopwatch)
        {
            const int MILLISECONDS_PER_YIELD = 10;

            Bounds? bounds = null;

            // Note: Renderer.bounds returns the bounding box
            // in world space, whereas Mesh.bounds return the
            // bounding box in local space.

            Renderer renderer = o.GetComponent<Renderer>();
            if (renderer != null) {
                bounds = renderer.bounds;
                if (stopwatch.ElapsedMilliseconds >= MILLISECONDS_PER_YIELD)
                {
                    yield return bounds;
                    stopwatch.Restart();
                }
            }

            foreach (Transform child in o.transform)
            {
                Bounds? childBounds = null;
                foreach (var result in
                    GetRendererBoundsForHierarchyEnum(child.gameObject, stopwatch))
                {
                    childBounds = result;
                    if (stopwatch.ElapsedMilliseconds >= MILLISECONDS_PER_YIELD)
                    {
                        yield return bounds;
                        stopwatch.Restart();
                    }
                }

                if (childBounds.HasValue) {
                    if (!bounds.HasValue) {
                        bounds = childBounds;
                    } else {
                        Bounds tmp = bounds.Value;
                        tmp.Encapsulate(childBounds.Value);
                        bounds = tmp;
                    }
                }
            }

            yield return bounds;
        }

        /// <summary>
        /// Scale a hierarchy of meshes so that the longest
        /// dimension of its world-space axis-aligned bounding box
        /// is equal to `targetSize`.
        /// </summary>
        public static IEnumerable Resize(GameObject root, float targetSize)
        {
            // Scale model up/down to a standard size, so that the
            // largest dimension of its bounding box is equal to `DefaultModelSize`.

            Bounds? bounds = null;

            foreach (var result in GetRendererBoundsForHierarchyEnum(root))
            {
                bounds = result;
                yield return null;
            }

            // In the case that the hierarchy contains no meshes, the
            // return value from GetRendererBoundsForHierarchy will
            // be null.
            if (!bounds.HasValue)
                yield break;

            // avoid divide by zero
            var size = bounds.Value.extents.MaxComponent();
            if (size < 0.000001f)
                yield break;

            // scale hierarchy to target size
            Vector3 scale = root.transform.localScale;
            float scaleFactor = targetSize / size;
            root.transform.localScale = scale * scaleFactor;
        }
    }
}