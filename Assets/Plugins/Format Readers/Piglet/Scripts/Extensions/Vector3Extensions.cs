using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    public static class Vector3Extensions
    {
        /// <summary>
        /// Return the vector component (x, y, or z) with
        /// the largest absolute value.
        /// </summary>
        public static float MaxComponent(this Vector3 v)
        {
            float max = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
            max = Mathf.Max(Mathf.Abs(v.z), max);
            return max;
        }
    }
}