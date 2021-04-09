using UnityEngine.Networking;

namespace Piglet
{
    public static class UnityWebRequestExtensions
    {
        /// <summary>
        /// Return true if an error occurred after sending this
        /// UnityWebRequest. This is a utility method that hides API
        /// changes to UnityWebRequest in Unity 2020.2.
        /// </summary>
        public static bool HasError(this UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    return true;
                default:
                    return false;
            }
#else
            return request.isNetworkError || request.isHttpError;
#endif
        }
    }
}
