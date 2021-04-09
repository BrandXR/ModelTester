using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BrandXR.Models.Demo
{
    public class ModelTester: MonoBehaviour
    {

        #region VARIABLES
        public string url;
        private GLTFLoader loader;
        public bool loadOnStart = false;
        public GLTFLoader.Options.ShaderType shaderType = GLTFLoader.Options.ShaderType.PBR;
        public GLTFLoader.Options.CullingType cullingType = GLTFLoader.Options.CullingType.BACK;

        #endregion

        #region STARTUP LOGIC

        //----------------------------------//
        public void Start()
        //----------------------------------//
        {
            if( loadOnStart )
            {
                LoadModel();
            }

        } //END Start

        #endregion

        #region LOAD MODEL

        //-----------------------------------//
        public void LoadModel( Action<string> OnDownloadComplete = null,
                                Action<float> OnDownloadProgress = null,
                                Action<string> OnDownloadError = null,
                                Action OnLoadComplete = null,
                                Action<float> OnLoadProgress = null,
                                Action<string> OnLoadError = null)
        //-----------------------------------//
        {
            DestroyModel();

            StartCoroutine( _LoadModel( OnDownloadComplete, OnDownloadProgress, OnDownloadError, OnLoadComplete, OnLoadProgress, OnLoadError ) );

        } //END LoadModel

        //-----------------------------------//
        public IEnumerator _LoadModel( Action<string> OnDownloadComplete = null,
                                Action<float> OnDownloadProgress = null,
                                Action<string> OnDownloadError = null,
                                Action OnLoadComplete = null,
                                Action<float> OnLoadProgress = null,
                                Action<string> OnLoadError = null )
        //-----------------------------------//
        {

            if( string.IsNullOrEmpty( url ) )
            {
                Debug.LogError( "url is empty" );
            }
            else
            {
                string name = Path.GetFileNameWithoutExtension( url );
                string ext = Path.GetExtension( url );
                string extractPath = Application.persistentDataPath + Path.DirectorySeparatorChar + name + ext;
                string cacheFolderPath = Application.persistentDataPath + Path.DirectorySeparatorChar + name;
                string glbPath = Application.persistentDataPath + Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar + name + ext;
                string gltfPath = Application.persistentDataPath + Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar + "scene.gltf";
                bool exists = false;

                if( GetComponent<GLTFLoader>() == null )
                    loader = gameObject.AddComponent<GLTFLoader>();
                else
                    loader = GetComponent<GLTFLoader>();

                if( ext == ".glb" )
                {
                    exists = File.Exists( glbPath );
                }
                else if( ext == ".zip" )
                {
                    if( Directory.Exists( cacheFolderPath ) )
                    {
                        string[ ] files = Directory.GetFiles( cacheFolderPath );

                        for( int i = 0; i < files.Length; i++ )
                        {
                            if( Path.GetExtension( files[ i ] ) == ".gltf" )
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                }

                if( !exists )
                {
                    UnityWebRequest webRequest = UnityWebRequest.Get( url );
                    webRequest.SendWebRequest();

                    while( !webRequest.isDone )
                    {
                        OnDownloadProgress?.Invoke( webRequest.downloadProgress );
                        yield return null;
                    }

                    if( webRequest.result == UnityWebRequest.Result.ConnectionError )
                    {
                        OnDownloadError?.Invoke( webRequest.error );
                        Debug.Log( webRequest.error );
                    }

                    File.WriteAllBytes( extractPath, webRequest.downloadHandler.data );

                    if( !Directory.Exists( cacheFolderPath ) )
                    {
                        Directory.CreateDirectory( cacheFolderPath );
                    }

                    if( ext != ".zip" )
                    {
                        File.Move( extractPath, glbPath );
                    }
                    else
                    {
#if !LZIP
                    OnDownloadError?.Invoke( "Missing LZIP scripting define symbol or plugin" );
                    Debug.LogError("Missing LZIP scripting define symbol or plugin, unable to extract GLTF");
                    yield break;
#else

#if !UNITY_WEBGL || UNITY_EDITOR
                        //When not using WEBGL, we can simply decompress the archive from the file path to its new location
                        try
                        {
                            int[ ] progressBuffer = { 0 };
                            lzip.decompress_File( extractPath, cacheFolderPath, progressBuffer );
                            File.Delete( extractPath );
                        }
                        catch
                        {
                            OnDownloadError?.Invoke( "Failed to extract archive" );
                            Debug.LogError( "Failed to extract archive" );
                            yield break;
                        }
#else
                    ulong size = lzip.getFileInfo( null, webRequest.downloadHandler.data );

                    if( lzip.ninfo != null && lzip.ninfo.Count > 0 )
                    {
                        for( int i = 0; i < lzip.ninfo.Count; i++ )
                        {
                            string entryPath = Path.Combine( cacheFolderPath, lzip.ninfo[ i ] );

                            if( lzip.ninfo[ i ].Contains( "/" ) )
                            {
                                string directory = lzip.ninfo[ i ].Substring( 0, lzip.ninfo[ i ].LastIndexOf( '/' ) );
                                directory = Path.Combine( cacheFolderPath, directory );

                                if( !Directory.Exists( directory ) )
                                {
                                    Directory.CreateDirectory( directory );
                                }
                            }

                            if( File.Exists( entryPath ) )
                            {
                                File.Delete( entryPath );
                            }

                            var decompressedBuffer = lzip.entry2Buffer( null, lzip.ninfo[ i ], webRequest.downloadHandler.data );
                            File.WriteAllBytes( Path.Combine( cacheFolderPath, lzip.ninfo[ i ] ), decompressedBuffer );

                        }
                    }
                    else
                    {
                        OnDownloadError?.Invoke( "Failed to extract archive" );
                        Debug.LogError( "Failed to extract archive" );
                        yield break;
                    }
#endif

#endif
                    }
                }

                loader.options.model_NormalizeScale = true;
                loader.options.model_Scale = 1f;
                loader.options.shaderType = shaderType;
                loader.options.cullingType = cullingType;
                
                string loadPath = glbPath;

                if( ext == ".zip" )
                {
                    string[ ] files = Directory.GetFiles( cacheFolderPath );

                    for( int i = 0; i < files.Length; i++ )
                    {
                        if( Path.GetExtension( files[ i ] ) == ".gltf" )
                        {
                            gltfPath = files[ i ];
                        }
                    }

                    loadPath = gltfPath;
                }

                OnDownloadComplete?.Invoke( loadPath );

                loader.LoadGLTF( loadPath,
                    ( GameObject go, Animation animation ) =>
                    {
                        OnLoadComplete?.Invoke();
                        go.transform.parent = transform;
                    },
                    ( string error ) =>
                    {
                        OnLoadError?.Invoke( error );
                        Debug.LogError( error );
                    },
                    ( float progress ) =>
                    {
                        OnLoadProgress?.Invoke( progress );
                        //Debug.Log( progress );
                    } );
            }

        } //END LoadModel

        #endregion

        #region DESTROY

        //-----------------------------------//
        public void DestroyModel()
        //-----------------------------------//
        {

            if( transform.childCount > 0 )
            {
                int childs = transform.childCount;
                
                for( int i = childs - 1; i > 0; i-- )
                {
                    GameObject.Destroy( transform.GetChild( i ).gameObject );
                }

                childs = transform.childCount;

                if( childs == 1 )
                {
                    Destroy( transform.GetChild( 0 ).gameObject );
                }
            }
            
        } //END DestroyModel

        #endregion

    } //END class

} //END namespace