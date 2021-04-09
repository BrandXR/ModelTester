using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace BrandXR.Models.Demo
{
    public class GLTFLoadTest: MonoBehaviour
    {

        #region VARIABLES
        public string url;
        private GLTFLoader loader;
        #endregion

        #region STARTUP LOGIC

        //----------------------------------//
        public IEnumerator Start()
        //----------------------------------//
        {
            if( string.IsNullOrEmpty( url ) )
            {
                Debug.LogError( "url is empty" );
            }
            else
            {
                string cachePath = Application.persistentDataPath + Path.DirectorySeparatorChar + Path.GetFileName( url );

                if( GetComponent<GLTFLoader>() == null )
                    loader = gameObject.AddComponent<GLTFLoader>();
                else
                    loader = GetComponent<GLTFLoader>();

                UnityWebRequest webRequest = new UnityWebRequest( url );
                webRequest.SendWebRequest();
                while( !webRequest.isDone )
                    yield return null;

                if( webRequest.result == UnityWebRequest.Result.ConnectionError )
                    Debug.Log( webRequest.error );

                File.WriteAllBytes( cachePath, webRequest.downloadHandler.data );

                loader.LoadGLTF( cachePath,
                    ( GameObject go, Animation animation ) =>
                    {
                        go.transform.parent = transform;
                    },
                    ( string error ) =>
                    {
                        Debug.LogError( error );
                    },
                    ( float progress ) =>
                    {
                        Debug.Log( progress );
                    } );
            }
            
        } //END Start

        #endregion

    } //END class

} //END namespace