/*******************************************************************
 * GLTFLoader.cs
 * 
 * Loads a locally stored GLTF model from PersistentData storage
 * Uses GLTFUtility plugin, and requires #GLTFUTILITY scripting define symbol in project settings
 * 
 *******************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BrandXR.Tools;

#if GLTFUTILITY
using Siccity.GLTFUtility;
#endif

#if PIGLET
using Piglet;
#endif

namespace BrandXR.Models
{
    public class GLTFLoader: MonoBehaviour
    {
#region VARIABLES

        public enum RepivotLocation
        {
            NONE,
            CENTER,
            BOTTOM_CENTER

        } //END enum

#if PIGLET
        private GltfImportTask pigletImportTask;
        private bool importInProgress;
#endif

#if RASCAL
        private RASCALSkinnedMeshCollider rascalSkinnedMeshCollider;
#endif

        //Used in the editor once to set the global light value used by GLTFUtility, 
        //if we don't set this our models will be black when imported with the AR system turned off
        private static bool hasEditorGlobalLightBeenSet = false;

#pragma warning disable IDE0044 // Add readonly modifier 
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CS0649 // Value never assigned
        private Action<GameObject, Animation> onSuccess;
        private Action<string> onError;
        private Action<float> onProgress;
#pragma warning restore CS0649 // Value never assigned
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0044 // Add readonly modifier

        public ShaderHelper shaderHelper; //Used to reference and assign shader and culling type to this model.

        public class Options
        {
            public Transform model_Parent = null;
            public bool model_NormalizeScale = true;
            public float model_Scale = 1f;
            public RepivotLocation model_RepivotLocation = RepivotLocation.BOTTOM_CENTER;
            public AnimationClip[ ] animations;
            public WrapMode animation_WrapMode = WrapMode.Loop;
            public string animation_Name = "";
            public int animation_Element = 0;
            public float progressMinimum = 0f;
            public float progressMaximum = 1f;
            public enum Format
            {
                GLTF,
                GLB
            }
            public Format format = Format.GLTF;
            public enum ColliderType
            {
                None,
                Mesh
            }
            public ColliderType collider = ColliderType.None;
            public enum ShaderType
            { 
                PBR,
                UNLIT
            }
            public ShaderType shaderType = ShaderType.PBR;
            public enum CullingType
            { 
                BACK,
                NONE
            }
            public CullingType cullingType = CullingType.BACK;
            
            public Options(
                 Transform parent = null,
#if GLTFUTILITY
                AnimationClip[] animations = null,
#endif
                bool normalizeScale = true, 
                float scale = 1f, 
                bool normalizeVerticalPosition = true,
                RepivotLocation repivotLocation = RepivotLocation.BOTTOM_CENTER, 
                WrapMode wrapMode = WrapMode.Loop, 
                bool autoplayAnimation = true, 
                string animationName = "", 
                int animationElement = 0, 
                float progressMinimum = 0f, 
                float progressMaximum = 1f, 
                bool addUserControlsTransform = false,
                ColliderType collider = ColliderType.None,
                Format format = Format.GLTF,
                ShaderType shader = ShaderType.PBR,
                CullingType culling = CullingType.BACK)
            {
                this.model_Parent = parent;
#if GLTFUTILITY
                this.animations = animations;
#endif
                this.model_NormalizeScale = true;
                this.model_Scale = scale;
                this.model_RepivotLocation = repivotLocation;
                this.animation_WrapMode = wrapMode;
                this.animation_Name = animationName;
                this.animation_Element = animationElement;
                this.progressMinimum = progressMinimum;
                this.progressMaximum = progressMaximum;
                this.collider = collider;
                this.format = format;
                this.shaderType = shader;
                this.cullingType = culling;
            }
        }
        public Options options = new Options();
#endregion

#region STARTUP LOGIC
        //------------------------------------------------------//
        public void Start()
        //------------------------------------------------------//
        {
            SetEditorGlobalLight();

        } //END Start

        //------------------------------------------------------//
        /// <summary>
        /// Sets the global light used by the generated GLTFUtility material shaders.
        /// In editor these light values are never set, so we need to ensure they are set at least once ourselves.
        /// </summary>
        private void SetEditorGlobalLight()
        //------------------------------------------------------//
        {

            if( !hasEditorGlobalLightBeenSet )
            {
                hasEditorGlobalLightBeenSet = true;
                
                // Set _GlobalColorCorrection to white in editor, if the value is not set, all
                // materials using light estimation shaders will be black.
                Shader.SetGlobalColor( "_GlobalColorCorrection", Color.white );

                // Set _GlobalLightEstimation for backward compatibility.
                Shader.SetGlobalFloat( "_GlobalLightEstimation", 1f );
            }

        } //END SetEditorGlobalLight
#endregion

#region CORE LOGIC
        //---------------------------------------//
        public void LoadGLTF( 
            string path, 
            Action<GameObject, Animation> onSuccess, 
            Action<string> onError = null, 
            Action<float> onProgress = null )
        //---------------------------------------//
        {

#if !GLTFUTILITY && !PIGLET
            onError?.Invoke( "Missing GLTFUtility GLTF loader plugin" ); //"GLTFLoader.cs LoadGLTF() GLTFUTILITY scripting define symbol or plugin is missing from project, unable to continue"
#else
            if( string.IsNullOrEmpty( path ) )
            {
                onError?.Invoke( "Path to 3D model is missing" ); //"GLTFLoader.cs LoadGLTF() path is null, unable to load"
                return;
            }
            else if( !File.Exists( path ) )
            {
                Debug.LogError( "GLTFLoader.cs LoadGLTF() model could not be found in local storage.path = " + path );
                onError?.Invoke( "3D model does not exist in local storage" ); 
                return;
            }
            else if( onSuccess == null )
            {
                onError?.Invoke( "Missing OnSuccess Delegate" ); //"GLTFLoader.cs LoadGLTF() Cannot continue, the onSuccess action cannot be null"
                return;
            }

            this.onSuccess = onSuccess;
            this.onError = onError;
            this.onProgress = onProgress;

            if( options == null )
            {
                options = new Options();
            }
            
            StaticCoroutine.Start( Load( path ) );
#endif

        } //END LoadGLTF

#endregion

#region GLTFUTILITY LOADING LOGIC
#if GLTFUTILITY
        //---------------------------------------//
        private IEnumerator Load( string path )
        //---------------------------------------//
        {
            options.format = path.Contains(".gltf") ? Options.Format.GLTF : Options.Format.GLB;

            ImportSettings importSettings = new ImportSettings();
            importSettings.materials = true;
            importSettings.useLegacyClips = true;

            //WebGL cannot use LoadAsync because it uses threading
#if UNITY_WEBGL && !UNITY_EDITOR
            if( options.format == Options.Format.GLTF )
            {
                Importer.LoadFromFile( path, _LoadComplete, Format.GLTF, onError );
            }
            else //.glb
            {
                Importer.LoadFromFile( path, _LoadComplete, Format.GLB, onError );
            }
#else
            if( options.format == Options.Format.GLTF )
            {
                Importer.ImportGLTFAsync( path, importSettings, _LoadComplete, LoadProgress, onError );
            }
            else //.glb
            {
                Importer.ImportGLBAsync( path, importSettings, _LoadComplete, LoadProgress, onError );
            }
#endif
            yield break;

        } //END Load

        //---------------------------------------//
        private void LoadProgress( float progress, string loadType = "" )
        //---------------------------------------//
        {
#if GLTFUTILITY
            if (loadType == "GLTFMaterial")
            {
                progress = MathHelper.Map(progress, 0f, 1f, 0, 0.5f); 
            }
            else if (loadType == "GLTFMesh")
            {
                progress = MathHelper.Map(progress, 0f, 1f, 0.5f, 1);
            }
#endif
            //Debug.Log( "GLTFLoader.cs LoadProgress( " + progress + " ) mapped = " + MathHelper.Map( progress, 0f, 1f, options.progressMinimum, options.progressMaximum ) );
            onProgress?.Invoke(MathHelper.Map(progress, 0f, 1f, options.progressMinimum, options.progressMaximum));

        } //END LoadProgress

        //---------------------------------------//
        private void _LoadComplete( GameObject modelRoot, AnimationClip[] animations )
        //---------------------------------------//
        {
            if( modelRoot != null )
            {
                //GLTFUtility brings models in rotated incorrectly, fix that now
                if( options.format == Options.Format.GLTF )
                {
                    modelRoot.transform.localEulerAngles = new Vector3( modelRoot.transform.localEulerAngles.x, modelRoot.transform.localEulerAngles.y, 180f );
                }
                else //.glb
                {
                    //Debug.Log( "modelRoot = " + modelRoot.name + ", rot = " + modelRoot.transform.localEulerAngles );
                    modelRoot.transform.localEulerAngles = new Vector3( 0f, 180f, 0f );
                }

                options.animations = animations;
                LoadComplete( modelRoot );
            }
            else
            {
                onError?.Invoke( "Load completed but did not return model" );
            }

        } //END _LoadComplete

#endif
#endregion

#region PIGLET LOADING LOGIC
#if PIGLET
        //---------------------------------------//
        private IEnumerator Load( string data )
        //---------------------------------------//
        {
            pigletImportTask = RuntimeGltfImporter.GetImportTask( data );
            pigletImportTask.OnProgress = LoadProgress;
            pigletImportTask.OnCompleted = _LoadComplete;
            importInProgress = true;

            yield break;

        } //END Load

        //----------------------------------------//
        void Update()
        //----------------------------------------//
        {
            if( importInProgress )
            {
                pigletImportTask.MoveNext();
            }

        } //END Update

        //------------------------------------------------------------------------//
        private void LoadProgress( GltfImportStep step, int completed, int total )
        //------------------------------------------------------------------------//
        {
            //Debug.LogFormat( "{0}: {1}/{2}", step, completed, total );
            onProgress?.Invoke( MathHelper.Map( (float)step, 0f, Enum.GetNames( typeof(GltfImportStep) ).Length, options.progressMinimum, options.progressMaximum ) );

        } //END LoadProgress

        //------------------------------------------------------------------------//
        private void _LoadComplete( GameObject modelRoot )
        //------------------------------------------------------------------------//
        {
            importInProgress = false;

            if( modelRoot != null )
            {
                LoadComplete( modelRoot );
            }
            else
            {
                onError?.Invoke( "Load completed but did not return model" );
            }

        } //END _LoadComplete
#endif
            #endregion

#region CORE LOADING LOGIC

        //---------------------------------------//
        private void LoadComplete( GameObject modelRoot )
        //---------------------------------------//
        {
            if( modelRoot == null )
                onError?.Invoke( "GLTFLoader.cs LoadComplete() modelRoot does not exist" );

            try
            {
                if( options.model_Parent != null )
                {
                    SetParent( modelRoot, options.model_Parent );
                }

                if( options.model_NormalizeScale )
                {
                    Resize( modelRoot, options.model_Scale );
                }

                Transform repivot = Repivot( modelRoot, options.model_RepivotLocation );

                Transform adjustments = CreateAdjustmentsTransform( modelRoot.transform );

                AddUserControlsTransform( adjustments );

                SetMeshProperties( modelRoot );

                UpdateShaderType(.01f, this.options.shaderType);
                UpdateCullingType(.01f, this.options.cullingType);

#if GLTFUTILITY
                Animation animationPlayer = SetupAnimations( modelRoot, options.animations );
                onSuccess?.Invoke( repivot.gameObject, animationPlayer );
#elif PIGLET
                Animation animationPlayer = SetupAnimations( modelRoot, options.animation_Name, options.animation_Element );
                onSuccess?.Invoke( repivot.gameObject, animationPlayer );
#endif

            }
            catch( Exception e )
            {
                Debug.LogError( e.ToString() );
                onError?.Invoke( "Failed to load 3D model" );
            }

        } //END LoadComplete

#if GLTFUTILITY
        //-----------------------------------------------------------------//
        private Animation SetupAnimations( GameObject go, AnimationClip[ ] animations )
        //-----------------------------------------------------------------//
        {
            //Debug.Log( "options.animation_Name = " + options.animation_Name + ", element = " + options.animation_Element + ", animations.length = " + animations.Length );
            Animation animationPlayer = go.AddComponent<Animation>() as Animation;
            animationPlayer.wrapMode = options.animation_WrapMode;

            if( animations != null && animations.Length > 0 )
            {
                bool foundAnimationToPlay = false;

                for( int i = 0; i < animations.Length; i++ )
                {
                    animationPlayer.AddClip( animations[ i ], animations[ i ].name );

                    //Check if the animation element or name is the same as this clip
                    if( ( i != -99 && i == options.animation_Element ) ||
                        ( animations[ i ].name == options.animation_Name ) )
                    {
                        //Debug.Log( "Playing Animations[" + i + "] = " + animations[ i ].clip.name );
                        animationPlayer.clip = animations[ i ];
                        animationPlayer.Play( animations[ i ].name );
                        foundAnimationToPlay = true;
                    }
                    else
                    {
                        //Debug.Log( "anim[" + i.ToString() + "] name = " + animations[i].clip.name + ", element = " + i );
                    }
                }

                //We can auto-play the 1st animation tied to a model if we pass in the keyword 'any' as our requested animation
                if( !foundAnimationToPlay && options.animation_Name.ToLower() == "any" )
                {
                    //Debug.Log( "Playing Animations[0] = " + options.animation_Name );
                    animationPlayer.clip = animations[ 0 ];
                    animationPlayer.Play( animations[ 0 ].name );
                }

            }

            return animationPlayer;

        } //END SetupAnimations
#endif

#if PIGLET
        //-----------------------------------------------------------------//
        private Animation SetupAnimations( GameObject go, string animationName, int animationElement )
        //-----------------------------------------------------------------//
        {
            if( go.GetComponent<Animation>() == null )
                return null;
            if( go.GetComponent<AnimationList>() == null )
                return null;

            AnimationList animationList = go.GetComponent<AnimationList>();
            Animation animationPlayer = go.GetComponent<Animation>();
            animationPlayer.wrapMode = options.animation_WrapMode;
            bool foundAnimationToPlay = false;

            //Remove the first 'blank' animation from the animationList object
            animationPlayer.RemoveClip( animationList.Clips[ 0 ] );
            animationList.Names.RemoveAt( 0 );
            animationList.Clips.RemoveAt( 0 );

            //Debug.Log( "animationName = " + animationName + ", element = " + animationElement + ", animationList.Clips.Count = " + animationList.Clips.Count );

            for( int i = 0; i < animationList.Names.Count; i++ )
            {
                if( i == animationElement || animationList.Names[ i ] == animationName )
                {
                    //Debug.Log( "Playing Animations[" + i + "] = " + animationList.Clips[ i ].name );
                    animationPlayer.clip = animationList.Clips[ i ];
                    animationPlayer.Play( animationList.Clips[ i ].name );
                    foundAnimationToPlay = true;
                }
            }

            //If we just want to play the first animation in the list, do that!
            //The first animation in Piglet is always the standard pose, so skip that one
            if( !foundAnimationToPlay && animationName.ToLower() == "any" && 
                animationList.Names.Count > 1 )
            {
                animationPlayer.clip = animationList.Clips[1];
                animationPlayer.Play( animationList.Clips[1].name );
            }

            return animationPlayer;

        } //END SetupAnimations
#endif

        //-----------------------------------------------------------------//
        /// <summary>
        /// Resizes the renderers to fit within a number of unity units
        /// //https://answers.unity.com/questions/517200/how-do-i-permanently-resize-a-model-with-children.html
        /// </summary>
        private void Resize( GameObject go, float desiredSizeInUnits )
        //-----------------------------------------------------------------//
        {

            Dictionary<Transform, Vector3> originalValues = new Dictionary<Transform, Vector3>();

            //Before we resize this model, we need to temporarily change any parent transforms to be 1, 1, 1 in local scale
            if( go.transform.parent != null )
            {
                Transform currentParent = go.transform.parent.transform;

                while( currentParent != null )
                {
                    if( currentParent.transform.localScale != Vector3.one )
                    {
                        originalValues.Add( currentParent, currentParent.localScale );
                        currentParent.transform.localScale = Vector3.one;
                    }

                    currentParent = currentParent.parent;
                }

            }

            //Now we can begin resizing the model
            Bounds combinedBounds = new Bounds( go.transform.position, new Vector3( 0, 0, 0 ) );
            Renderer[ ] renderers = go.GetComponentsInChildren<Renderer>();

            foreach( Renderer r in renderers )
            {
                combinedBounds.Encapsulate( r.bounds );
            }

            float size = combinedBounds.size.x;

            if( size < combinedBounds.size.y )
            {
                size = combinedBounds.size.y;
            }
            if( size < combinedBounds.size.z )
            {
                size = combinedBounds.size.z;
            }

            if( Mathf.Abs( desiredSizeInUnits - size ) < 0.01f )
            {
                //No need to resize
                return;
            }

            float scale = desiredSizeInUnits / size;

            go.transform.localScale *= scale;

            //To finish, let's restore all of the parent's to their original scales
            if( go.transform.parent != null && originalValues != null && originalValues.Count > 0 )
            {
                foreach( KeyValuePair<Transform, Vector3> keyValuePair in originalValues )
                {
                    keyValuePair.Key.localScale = keyValuePair.Value;
                }
            }

        } //END Resize

        //-----------------------------------------------------------------//
        /// <summary>
        /// Force the pivot of the GameObject to be at the center of all the models renderers
        /// </summary>
        /// <param name="go">The GameObject with renderer components on it or on a child GameObject</param>
        public Transform Repivot( GameObject go, RepivotLocation pivotLocation = RepivotLocation.BOTTOM_CENTER )
        //-----------------------------------------------------------------//
        {
            //Create new gameobject for repivot
            Transform modelParent = go.transform.parent;
            Transform repivot = new GameObject( "Repivot" ).transform;

            //Make repivot new parent of model
            repivot.SetParent( modelParent );
            repivot.localPosition = Vector3.zero;
            repivot.localEulerAngles = Vector3.zero;
            repivot.localScale = Vector3.one;

            //If we don't need to change the pivot, then we are good to go
            if( pivotLocation == RepivotLocation.NONE )
                return repivot;

            //Debug.Log( String.Format("GLTFLoader.cs Repivot( {0}, {1} )", go.name, pivotLocation.ToString() ) );

            //Get the visible bounds of the model
            Renderer[ ] renderers = go.GetComponentsInChildren<Renderer>();

            if( renderers.Length == 0 )
            {
                //Debug.LogWarning(String.Format("Repivot called on gameobject with no renderers ({0}).", go.name));
                return repivot;
            }

            Bounds combinedBounds = renderers[ 0 ].bounds;

            for( int i = 1; i < renderers.Length; i++ )
            {
                combinedBounds.Encapsulate( renderers[ i ].bounds );
            }

            //Determine position of new pivot
            Vector3 newPivotPosition = Vector3.zero;

            if( pivotLocation == RepivotLocation.CENTER )
            {
                newPivotPosition = combinedBounds.center;
            }
            else if( pivotLocation == RepivotLocation.BOTTOM_CENTER )
            {
                newPivotPosition = combinedBounds.center;
                newPivotPosition.y -= combinedBounds.extents.y;
            }


            repivot.position = newPivotPosition;


            go.transform.SetParent( repivot );

            //Move repivot to center of parent
            repivot.localPosition = Vector3.zero;

            //Reset repivot's rotation to zero
            repivot.localEulerAngles = Vector3.zero;

            return repivot;

        } //END Repivot

        //-------------------------------------------------------------//
        private Transform CreateAdjustmentsTransform( Transform rootNode )
        //-------------------------------------------------------------//
        {
            Transform adjustments = new GameObject( "Model Adjustments" ).transform;
            adjustments.SetParent( rootNode.parent );
            adjustments.localPosition = rootNode.localPosition;
            adjustments.localEulerAngles = rootNode.localEulerAngles;
            adjustments.localScale = rootNode.localScale;

            rootNode.transform.SetParent( adjustments );
            rootNode.transform.localPosition = Vector3.zero;
            rootNode.transform.localEulerAngles = Vector3.zero;
            rootNode.transform.localScale = Vector3.one;

            return adjustments;

        } //END CreateAdjustmentsTransform

        //-------------------------------------------------------------//
        private void AddUserControlsTransform( Transform adjustments )
        //-------------------------------------------------------------//
        {
            //Add a gameObject in-between the model root and the action model transform, which will be used for user defined rotation and scaling

            //Debug.Log( "AddUserControlsTransform() modelRoot.name = " + modelRoot.name );

            Transform existingChild = adjustments.GetChild( 0 );

            Transform userControls = new GameObject( "User Rotation And Scale" ).transform;
            userControls.SetParent( adjustments );
            userControls.localPosition = Vector3.zero;
            userControls.localEulerAngles = Vector3.zero;
            userControls.localScale = Vector3.one;

            existingChild.transform.SetParent( userControls );

        } //END AddUserControlsTransform

        #endregion

#region MESH LOADING LOGIC

        //-------------------------------------------------------------//
        private void SetMeshProperties(GameObject modelRoot)
        //-------------------------------------------------------------//
        {
            if( modelRoot != null && gameObject.activeSelf )
            {
                //Run through with Rascal to generate Rascal colliders for all existing skinned meshes
                //Rascal CANNOT generate meshColliders for SkinnedMeshRenderers with empty bone transforms
#if RASCAL
                //Rascal should not update continuous and instead will update with explicit commands to the 
                //Start() method returns when object is tapped
                rascalSkinnedMeshCollider = modelRoot.AddComponent<RASCALSkinnedMeshCollider>();
                rascalSkinnedMeshCollider.immediateStartupCollision = true;
                rascalSkinnedMeshCollider.enableUpdatingOnStart = false;
#if UNITY_WEBGL
                rascalSkinnedMeshCollider.useThreadedColMeshBaking = false;
#endif
                StartCoroutine( ISetMeshProperties( modelRoot ) );
#else
                AddMeshCollider( modelRoot );
                //Debug.Log("GLTFLoader//SetMeshProperties//Missing RASCAL Plugin, using fallback means of assigning colliders.");
#endif
            }

        } //End SetMeshProperties

        //-------------------------------------------------------------//
        private IEnumerator ISetMeshProperties(GameObject modelRoot)
        //-------------------------------------------------------------//
        {
#if RASCAL
            yield return new WaitUntil(() => rascalSkinnedMeshCollider.processed == true);

            //Collect all renderers in the Model Object
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();

            //Cycle through renderers
            foreach (Renderer renderer in renderers)
            {
                SkinnedMeshRenderer existingSkinMeshRenderer = renderer.gameObject.GetComponent<SkinnedMeshRenderer>();
                MeshCollider meshCollider = null;

                //If this renderer is not a SkinnedMeshRenderer, generate non-rascal mesh colliders
                if (existingSkinMeshRenderer == null)
                {
                    //Add Collider
                    meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
                    meshCollider.convex = false;
                }
            }
#else
            yield return null;
            Debug.LogError("GLTFLoader//ISetMeshProperties//RASCAL Plugin is not loaded.");
#endif

        } //End ISetMeshProperties

        /// <summary>
        /// Deprecated method consider removing
        /// </summary>
        /// <param name="root"></param>
        //-----------------------------------------------------------------//
        private void AddMeshCollider(GameObject root)
        //-----------------------------------------------------------------//
        {

            //Find all the children of this root
            List<GameObject> children = AllChilds(root);

            //Find all the Renderers in this model
            List<Renderer> renderers = new List<Renderer>();

            foreach (GameObject child in children)
            {
                if (child.GetComponent<Renderer>() != null)
                {
                    //Debug.Log( "Found Skinned Mesh Renderer = " + child.name );
                    renderers.Add(child.GetComponent<Renderer>());
                }
            }

            //Add a convex MeshCollider to every MeshRenderer
            if (renderers != null && renderers.Count != 0)
            {
                MeshCollider meshCollider = null;

                foreach (Renderer mesh in renderers)
                {
                    meshCollider = mesh.gameObject.AddComponent<MeshCollider>();
                    meshCollider.convex = false;

                    if (mesh.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        SkinnedMeshRenderer skinnedMeshRenderer = mesh.GetComponent<SkinnedMeshRenderer>();
                        meshCollider.sharedMesh = skinnedMeshRenderer.sharedMesh;
                    }
                }
            }

        } //END AddMeshCollider

        //---------------------------------------------------------//
        private List<GameObject> AllChilds(GameObject root)
        //---------------------------------------------------------//
        {
            List<GameObject> result = new List<GameObject>();

            if (root.transform.childCount > 0)
            {
                foreach (Transform VARIABLE in root.transform)
                {
                    Searcher(result, VARIABLE.gameObject);
                }
            }
            return result;

        } //END AllChilds

        //----------------------------------------------------------------//
        private void Searcher(List<GameObject> list, GameObject root)
        //----------------------------------------------------------------//
        {
            list.Add(root);

            if (root.transform.childCount > 0)
            {
                foreach (Transform VARIABLE in root.transform)
                {
                    Searcher(list, VARIABLE.gameObject);
                }
            }

        } //END Searcher

        //-----------------------------------------------------------------//
        private void SetParent(GameObject go, Transform parent)
        //-----------------------------------------------------------------//
        {
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

        } //END SetParent

        #endregion

#region SET SHADER TYPE

        //-------------------------------------------------------------//
        public void UpdateShaderType(float delay, Options.ShaderType shaderType)
        //-------------------------------------------------------------//
        {
            StartCoroutine(IUpdateShaderType(delay, shaderType));

        } //END UpdateShaderType

        //-------------------------------------------------------------//
        private IEnumerator IUpdateShaderType(float delay, Options.ShaderType shaderType)
        //-------------------------------------------------------------//
        {
            yield return new WaitForSeconds(delay);

            if (this.gameObject.GetComponent<ShaderHelper>() == null)
            {
                shaderHelper = this.gameObject.AddComponent<ShaderHelper>();
            }

            shaderHelper.Setup();
            shaderHelper.GetShaderType();
            shaderHelper.SetShaderType(shaderType);

        } //END IUpdateShaderType

        #endregion

#region SET CULLING TYPE

        //-------------------------------------------------------------//
        public void UpdateCullingType(float delay, Options.CullingType cullingType)
        //-------------------------------------------------------------//
        {
            StartCoroutine(IUpdateCullingType(delay, cullingType));

        } //END UpdateCullingType

        //-------------------------------------------------------------//
        private IEnumerator IUpdateCullingType(float delay, Options.CullingType cullingType)
        //-------------------------------------------------------------//
        {
            yield return new WaitForSeconds(delay);

            if (this.gameObject.GetComponent<ShaderHelper>() == null)
            {
                shaderHelper = this.gameObject.AddComponent<ShaderHelper>();
            }

            shaderHelper.SetCullingType(cullingType);

        } //END IUpdateCullingType

        #endregion

    } //END class

} //END namespace