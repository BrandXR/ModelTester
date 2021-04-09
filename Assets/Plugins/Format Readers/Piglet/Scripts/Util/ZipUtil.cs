using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

namespace Piglet
{
    /// <summary>
    /// Utility methods for working with .zip files.
    /// </summary>
    public class ZipUtil
    {
        /// <summary>
        /// Return true if the given byte array is a zip archive.
        /// </summary>
        static public bool IsZipData(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return IsZipData(stream);
            }
        }

        /// <summary>
        /// Return true if the given stream is a zip archive.
        /// </summary>
        static public bool IsZipData(Stream stream)
        {
            try
            {
                var zip = new ZipFile(stream);
                
                // This is the recommended method for checking if a
                // file is a valid zip archive.  For further info,
                // see: https://stackoverflow.com/a/9539846/12989671
                return zip.TestArchive(false, TestStrategy.FindFirstError, null);
            }
            catch (ZipException)
            {
                return false;
            }
        }

        /// <summary>
        /// Extract the given zip archive entry (directory/file) and
        /// write it to `outputStream`.
        /// </summary>
        static public IEnumerator UnzipEntryEnum(
            ZipFile archive, ZipEntry entry, Stream outputStream)
        {
            using (var inputStream = archive.GetInputStream(entry))
            {
                if (!entry.IsFile)
                    yield break;
                
                var copyTask = StreamUtil.CopyStreamEnum(inputStream, outputStream);
                while (copyTask.MoveNext())
                    yield return null;
            }
        }

        /// <summary>
        /// Return if the given zip file contains a
        /// .gltf or .glb file.
        /// </summary>
        static public bool ContainsGltfFile(string zipPath)
        {
            ZipEntry entry = null;
            foreach (var result in GetEntry(zipPath, new Regex("\\.(gltf|glb)$")))
                entry = result;
            return entry != null;
        }

        /// <summary>
        /// Get the byte content of the first file in the zip archive whose
        /// filename matches `regex`.
        /// </summary>
        static public IEnumerable<byte[]> GetEntryBytes(byte[] zipData, Regex regex)
        {
            bool matcher(ZipEntry candidate) => regex.IsMatch(candidate.Name);
            
            byte[] data = null;
            foreach (var result in GetEntryBytes(zipData, matcher))
            {
                data = result;
                yield return null;
            }

            yield return data;
        }
        
        /// <summary>
        /// Get the byte content of the first file in the zip archive whose
        /// filename exactly matches `filename`.
        /// </summary>
        static public IEnumerable<byte[]> GetEntryBytes(byte[] zipData, string filename)
        {
            // Clean up the filename to follow conventions for zip paths.
            // For example, if the given filename uses backslashes (`\`) as
            // path separators, change them to forward slashes (`/`).
            filename = ZipEntry.CleanName(filename);
            
            bool matcher(ZipEntry candidate) => candidate.Name == filename;
            
            byte[] data = null;
            foreach (var result in GetEntryBytes(zipData, matcher))
            {
                data = result;
                yield return null;
            }

            yield return data;
        }
        
        /// <summary>
        /// Run the `matcher` function on each ZipEntry in the given archive,
        /// and return the byte content for the first ZipEntry that yields
        /// a value of true. Return null otherwise.
        /// </summary>
        static public IEnumerable<byte[]> GetEntryBytes(byte[] zipData,
            Func<ZipEntry, bool> matcher)
        {
            using (var inputStream = new MemoryStream(zipData))
            {
                using (var archive = new ZipFile(inputStream))
                {
                    ZipEntry entry = null;
                    foreach (var result in GetEntry(archive, matcher))
                    {
                        entry = result;
                        yield return null;
                    }

                    if (entry == null)
                    {
                        yield return null;
                        yield break;
                    }

                    byte[] data = new byte[entry.Size];
                    using (var outputStream = new MemoryStream(data))
                    {
                        var unzipTask = UnzipEntryEnum(archive, entry, outputStream);
                        while (unzipTask.MoveNext())
                            yield return null;
                    }

                    yield return data;
                }
            }
        }
        
        /// <summary>
        /// Return the path of the first .gltf/.glb found in the zip
        /// archive at `zipPath`, or return null otherwise.
        /// </summary>
        static public IEnumerable<ZipEntry> GetEntry(string zipPath, Regex regex)
        {
            using (var stream = File.OpenRead(zipPath))
            {
                foreach (var result in GetEntry(stream, regex))
                    yield return result;
            }
        }

        /// <summary>
        /// Return the ZipEntry for the first file in the
        /// zip data whose name matches the regular
        /// expression, or null if no such file is found.
        /// </summary>
        static public IEnumerable<ZipEntry> GetEntry(byte[] zipData, Regex regex)
        {
            using (var inputStream = new MemoryStream(zipData))
            {
                ZipEntry entry = null;
                foreach (var result in GetEntry(inputStream, regex))
                {
                    entry = result;
                    yield return null;
                }

                yield return entry;
            }
        }

        /// <summary>
        /// Return the ZipEntry for the first file in the
        /// zip stream whose name matches the regular
        /// expression, or null if no such file is found.
        /// </summary>
        static public IEnumerable<ZipEntry> GetEntry(Stream zipStream, Regex regex)
        {
            using (var archive = new ZipFile(zipStream))
            {
                ZipEntry entry = null;
                foreach (var result in GetEntry(archive, regex))
                {
                    entry = result;
                    yield return null;
                }

                yield return entry;
            }
        }

        /// <summary>
        /// Return the ZipEntry for the first file in the
        /// zip archive whose name matches the regular
        /// expression, or null if no such file is found.
        /// </summary>
        static public IEnumerable<ZipEntry> GetEntry(ZipFile archive, Regex regex)
        {
            bool matcher(ZipEntry candidate) => regex.IsMatch(candidate.Name);

            ZipEntry entry = null;
            foreach (var result in GetEntry(archive, matcher))
            {
                entry = result;
                yield return null;
            }

            yield return entry;
        }

        /// <summary>
        /// Run the `matcher` function on each ZipEntry in the given
        /// zip archive and return the first ZipEntry that yields a value
        /// of true. Otherwise return null.
        /// </summary>
        static public IEnumerable<ZipEntry> GetEntry(ZipFile archive,
            Func<ZipEntry, bool> matcher)
        {
            foreach (ZipEntry entry in archive)
            {
                if (!entry.IsFile)
                    continue;
                
                if (matcher.Invoke(entry))
                {
                    yield return entry;
                    yield break;
                }

                yield return null;
            }
        }
    }
}
