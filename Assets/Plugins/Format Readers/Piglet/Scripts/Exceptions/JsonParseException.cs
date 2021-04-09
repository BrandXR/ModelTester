using System;

namespace Piglet
{
    /// <summary>
    /// This class is used to wrap Json.NET exceptions that occur
    /// during glTF imports, so that Piglet applications
    /// (e.g. PigletViewer) do not need to compile against the
    /// Json.NET DLL in order to catch/handle exceptions.
    /// </summary>
    public class JsonParseException : Exception
    {
        public JsonParseException(string message, Exception e)
            : base(message, e) {}
    }
}
