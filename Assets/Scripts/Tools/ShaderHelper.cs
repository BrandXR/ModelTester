using UnityEngine;

namespace BrandXR.Models
{
    public class ShaderHelper : MonoBehaviour
    {
        #region VARIABLES
        [SerializeField] private Shader unlitTransparentShader;
        [SerializeField] private Shader unlitShader;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Shader[] originalShaders;

        #endregion // END VARIABLES

        #region SETUP
        //-------------------------------------//
        public void Setup()
        //-------------------------------------//
        {
            GetShaders();
            GetChildren(this.transform);

        } // END Setup

        #endregion // END SETUP

        #region GET METHODS
        //---------------- Finds all of the renderers for this experience to assign shader or culling changes to ---------------------//

        /// <summary>
        /// Searches each child object and if there are children then it gets their children until there are no more.
        /// When it gets each child it will also check to see if that object has a Renderer, 
        /// if it does it will add it to the array of renderers.
        /// </summary>
        /// <param name="parent"></param>
        //-------------------------------------//
        private void GetChildren(Transform parent)
        //-------------------------------------//
        {
            foreach (Transform child in parent.transform)
            {
                GetChildren(child);
                GetChildRenderer(child);
            }

        } // END GetChildren

        //-------------------------------------//
        private void GetChildRenderer(Transform obj)
        //-------------------------------------//
        {
            renderers = obj.GetComponentsInChildren<Renderer>();

        } // END GetChildRenderer

        //-------------------------------------//
        private void GetShaders()
        //-------------------------------------//
        {
            unlitShader = Shader.Find("Custom/Unlit");
            unlitTransparentShader = Shader.Find("Custom/UnlitTransparentWithDepth");

        } // END GetShaders

        #endregion // END GET METHODS

        #region GET SHADER TYPE

        //-------------------------------------//
        public void GetShaderType()
        //-------------------------------------//
        {
            originalShaders = new Shader[renderers.Length];

            for (int i = 0; i < (renderers.Length); i++)
            {
                originalShaders[i] = renderers[i].material.shader;
            }

        } // END GetShaderTypes

        #endregion

        #region SET SHADER TYPE
        //---------------- Changes all default shaders attached to this experience to the Unlit with Depth Shader ---------------------//

        //-------------------------------------//
        public void SetShaderToPBR()
        //-------------------------------------//
        {
            SetShaderType( GLTFLoader.Options.ShaderType.PBR );

        } //END SetShaderToPBR

        //-------------------------------------//
        public void SetShaderToUnlit()
        //-------------------------------------//
        {
            SetShaderType( GLTFLoader.Options.ShaderType.UNLIT );

        } //END SetShaderToUnlit

        //-------------------------------------//
        public void SetShaderType(GLTFLoader.Options.ShaderType _shaderType)
        //-------------------------------------//
        {
            switch (_shaderType)
            {
                case GLTFLoader.Options.ShaderType.PBR:
                    if (renderers != null)
                    {
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            renderers[i].material.shader = originalShaders[i];
                        }
                    }
                    else
                    {
                        GetChildren(this.transform);

                        if (renderers != null)
                        {
                            for (int i = 0; i < renderers.Length; i++)
                            {
                                renderers[i].material.shader = originalShaders[i];
                            }
                        }
                        else
                        {
                            Debug.LogWarning("SetShaderType() // else // else // After searching again there were no renderers to be found. You need to attach this component to the correct object!");
                        }
                    }

                    break;
                case GLTFLoader.Options.ShaderType.UNLIT:

                    if (renderers != null)
                    {
                        foreach (Renderer rend in renderers)
                        {
                            if (rend.material.shader.ToString().Contains("Transparent"))
                            {
                                rend.material.shader = unlitTransparentShader;
                            }
                            else
                            {
                                rend.material.shader = unlitShader;
                            }
                        }
                    }
                    else
                    {
                        GetChildren(this.transform);

                        if (renderers != null)
                        {
                            foreach (Renderer rend in renderers)
                            {
                                if (rend.material.shader.ToString().Contains("Transparent"))
                                {
                                    rend.material.shader = unlitTransparentShader;
                                }
                                else
                                {
                                    rend.material.shader = unlitShader;
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("SetShaderType() // else // else // After searching again there were no renderers to be found. You need to attach this component to the correct object!");
                        }
                    }

                    break;
                default:
                    break;
            }

        } // END SetShaderType

        #endregion

        #region SET CULLING TYPE
        //---------------- Changes the culling mode to allow for double sided materials with our custom shaders ---------------------//

        //-------------------------------------//
        public void SetCullingType(GLTFLoader.Options.CullingType _cullingType)
        //-------------------------------------//
        {
            switch (_cullingType)
            {
                case GLTFLoader.Options.CullingType.BACK:
                    if (renderers != null)
                    {
                        foreach (Renderer rend in renderers)
                        {
                            rend.material.SetFloat("_Cull", 2);
                        }
                    }
                    else
                    {
                        GetChildren(this.transform);

                        if (renderers != null)
                        {
                            foreach (Renderer rend in renderers)
                            {
                                rend.material.SetFloat("_Cull", 2);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("SetCullingType() // else // else // After searching again there were no renderers to be found. You need to attach this component to the correct object!");
                        }
                    }

                    break;
                case GLTFLoader.Options.CullingType.NONE:

                    if (renderers != null)
                    {
                        foreach (Renderer rend in renderers)
                        {
                            rend.material.SetFloat("_Cull", 0);
                        }
                    }
                    else
                    {
                        GetChildren(this.transform);

                        if (renderers != null)
                        {
                            foreach (Renderer rend in renderers)
                            {
                                rend.material.SetFloat("_Cull", 0);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("SetCullingType() // else // else // After searching again there were no renderers to be found. You need to attach this component to the correct object!");
                        }
                    }

                    break;
                default:
                    break;
            }
        } // END SetCullingType

        #endregion // END SET METHODS

    } // END Class

}