using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// Utility methods for reading data from Android content URIs.
    /// Content URIs are often used in place of ordinary
    /// file paths/URIs since Android 7.0, because they
    /// enable more fine-grained control of file permissions.
    ///
    /// For background info, see:
    /// https://developer.android.com/reference/android/support/v4/content/FileProvider
    /// 
    /// As of Unity 2018.3, Unity has no built in support for reading
    /// data from content URIs (e.g. UnityWebRequest).
    /// </summary>
    public class ContentUriUtil
    {
        /// <summary>
        /// Coroutine to read all bytes from an Android content URI.
        /// </summary>
        static public IEnumerable<byte[]> ReadAllBytesEnum(Uri uri,
            Action<ulong, ulong> onProgress=null)
        {
            AndroidJavaObject stream = new AndroidJavaObject(
                "com.awesomesaucelabs.piglet.ContentUriStream",
                uri.ToString());

            ulong size = (ulong)stream.Call<long>("getSize");
            onProgress?.Invoke(0, size);
            yield return null;

            byte[] result = new byte[size];
            int bytesRead = 0;
            byte[] buffer;
            
            // note: read method returns null on EOF
            while ((buffer = stream.Call<byte[]>("read")) != null
                   && bytesRead < (int)size)
            {
                Array.Copy(buffer, 0, result, bytesRead, buffer.Length);
                bytesRead += buffer.Length;
                
                onProgress?.Invoke((ulong)bytesRead, size);
                yield return null;
            }

            yield return result;
        }
    }
}