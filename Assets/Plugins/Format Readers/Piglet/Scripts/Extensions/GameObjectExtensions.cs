using UnityEngine;

namespace Piglet
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// If a MonoBehaviour of type T is attached to this GameObject,
        /// remove it. Otherwise do nothing.
        public static void RemoveComponent<T>(this GameObject gameObject)
            where T : Object
        {
            Object component = gameObject.GetComponent<T>();
            if (component == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(component);
            else
                Object.DestroyImmediate(component);
        }

        /// <summary>
        /// If this GameObject has a MonoBehaviour of type T,
        /// return it. Otherwise, add a new MonoBehaviour of type T
        /// and return that instead.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject)
            where T : Component
        {
            T result = gameObject.GetComponent<T>();
            if (result == null)
                result = gameObject.AddComponent<T>();
            return result;
        }

        /// <summary>
        /// Return the path to a descendant GameObject (`descendant`)
        /// as a slash-separated list of GameObject names
        /// (e.g. "LeftLeg/Foot/BigToe"). The Unity animation system
        /// uses such paths to identify animation targets within a
        /// hierarchy of GameObjects. Note that the returned path
        /// always starts with a first-level child of this GameObject
        /// (`root`), and does not include the `root` GameObject itself.
        /// In the case that `descendant` is the same GameObject as
        /// `root`, the returned path will be the empty string. In the
        /// case that `descendant` is not actually a descendant of
        /// `root`, the returned path will be null.
        /// </summary>
        public static string GetPathToDescendant(this GameObject root, GameObject descendant)
        {
            var transform = descendant.transform;
            string path = "";

            while (transform != root.transform)
            {
                path = path.Length == 0 ? transform.name : $"{transform.name}/{path}";

                transform = transform.parent;

                // if `descendant` is not actually a descendant of `root`
                if (transform == null)
                    return null;
            }

            return path;
        }
    }
}
