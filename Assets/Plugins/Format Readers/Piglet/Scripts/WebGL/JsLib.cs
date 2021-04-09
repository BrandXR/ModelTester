#if UNITY_WEBGL
using System;
using System.Runtime.InteropServices;

namespace Piglet
{
    /// <summary>
    /// Declarations of Javascript methods that make them callable from C#.
    ///
    /// The Javascript code for these functions is found in
    /// Assets/Plugins/WebGL/Piglet.jslib.
    /// </summary>
    public static class JsLib
    {
        /// <summary>
        /// Return a localhost URL through which the given data
        /// (byte[] array) can be read.  This method is a wrapper
        /// around the Javascript method `URL.createObjectURL`.
        /// </summary>
        [DllImport("__Internal")]
        public static extern string CreateObjectUrl(byte[] data, int size);
    }
}

#endif
