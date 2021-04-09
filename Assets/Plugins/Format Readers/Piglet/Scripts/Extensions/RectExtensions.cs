using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    public static class RectExtensions
    {
        /// <summary>
        /// Return true if rect1 completely contains in rect2, or false otherwise.
        /// </summary>
        public static bool Contains(this Rect rect1, Rect rect2)
        {
            return rect1.Contains(new Vector2(rect2.x, rect2.y))
                   && rect1.Contains(new Vector2(rect2.x + rect2.width, rect2.y))
                   && rect1.Contains(new Vector2(rect2.x + rect2.width, rect2.y + rect2.height))
                   && rect1.Contains(new Vector2(rect2.x, rect2.y + rect2.height));
        }
    }
}