using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Threading.Tasks;

[ExecuteInEditMode]
public class UIGaussianBlurLayer : MonoBehaviour
{
    // it's best to use RawImage only for backgrounds or temporary visible graphics: this will create an extra draw call with each RawImage present
    public UnityEngine.UI.RawImage rawImage;
    public Shader shader;

    // Higher value = faster performance due to bigger gaps between neighbouring pixels
    [Range(0, 6), Tooltip("Use a higher value for better performance")]
    public int downSampleNum = 2;
    
    [Range(0.0f, 20.0f), Tooltip("Use a higher value for stronger blur")]
    public float BlurSpreadSize = 3.0f;

    [Range(0, 4), Tooltip("Use a lower value as higher values are worse for performance")]
    public int BlurIterations = 3;
    public int delayInMsBeforDisableComponents = 1;

    [SerializeField] private Camera camera;
    private RenderTexture rt;
    private Material mat;
    private string shaderName = "PostProcessing/RapidGaussianBlur";
    private Color color;

    #region MaterialGetAndSet
    Material material
    {
        get
        {
            if (mat == null)
            {
                mat = new Material(shader);
                // commonly used for GameObjects which are created by a script and are purely under the script's control:
                // the GameObject is not shown in the Hierarchy, not saved to the Scene, and not unloaded by Resources.UnloadUnusedAssets.
                mat.hideFlags = HideFlags.HideAndDontSave;
            }

            return mat;
        }
    }
    #endregion
    
    private void Start()
    {
        camera = GetComponent<Camera>();
        shader = Shader.Find(shaderName);
        color = rawImage.color;
        color.a = 1f;
    }

    private void Cleanup()
    {
        if (mat)
        {
            // This function should only be used when writing editor code since the delayed destruction (Object.Destroy) will never be invoked in edit mode 
            DestroyImmediate(mat, true);
        }

        if (rawImage.texture)
        {
            // Release a temporary texture allocated with GetTemporary: Later calls to GetTemporary will reuse the RenderTexture created earlier if possible.
            // When no one has requested the temporary RenderTexture for a few frames it will be destroyed.
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private void OnEnable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    // Event function that Unity calls after a Camera has finished rendering, that allows you to modify the Camera's final image.
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Graphics.Blit(src, dst);
        if (!gameObject.activeInHierarchy && enabled)
        {
            return;
        }

        if (!camera || !shader || rt != null)
        {
            return;
        }

        float widthMod = 1.0f / (1.0f * (1 << downSampleNum));
        material.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod);

        // TODO: adjust the texture offset and texture scale in the x direction to compensate the distortion OR use a mask on parent
        // Use math to work it out when you know your texture size and your quad size. 
        // https://discussions.unity.com/t/rendering-into-part-of-a-render-texture/636179/25
        int renderWidth = src.width >> downSampleNum;
        int renderHeight = src.height >> downSampleNum;
        rt = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.RGB111110Float);
        rt.filterMode = FilterMode.Bilinear;
        
        // Uses a shader to copy the pixel data from a texture into a render target.
        // This method copies pixel data from a texture on the GPU to a render texture. This is one of the fastest ways to copy a texture.
        Graphics.Blit(src, rt, material, 0);

        for (int i = 0; i < BlurIterations; i++)
        {
            float iterationOffsets = (i * 1.0f);
            material.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod + iterationOffsets);
            
            // Pass 1 for vertical blur
            // Allocate a temporary render texture. This function is optimized for when you need a quick RenderTexture to do some temporary calculations.
            // Release it using ReleaseTemporary as soon as you're done with it, so another call can start reusing it if needed.
            // Internally Unity keeps a pool of temporary render textures, a call to GetTemporary most often just returns an already created one (if the size and format matches). These temporary render textures are actually destroyed when they aren't used for a couple of frames.
            // If doing a series of post-processing blits, it's best for performance to get and release a temporary render texture for each blit, instead of getting one or two render textures upfront and reusing them. This is mostly beneficial for mobile (tile-based) 
            RenderTexture tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.RGB111110Float);
            Graphics.Blit(rt, tempBuffer, material, 1);
            RenderTexture.ReleaseTemporary(rt);
            rt = tempBuffer;
            // Pass 2 for horizontal blur
            tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.RGB111110Float);
            Graphics.Blit(rt, tempBuffer, mat, 2);
            RenderTexture.ReleaseTemporary(rt);
            rt = tempBuffer;
        }
        rawImage.texture = rt;
        rawImage.color = color;

    # if !UNITY_EDITOR
        DisableComponentsAfterDelay();
    # endif
    }

    #if !UNITY_EDITOR
    private async void DisableComponentsAfterDelay()
    {
        await Task.Delay(delayInMsBeforDisableComponents);
        // Disable all components that's not needed after raw texture is created for better performance on device
        camera.enabled = false;
        enabled = false;
    }
    #endif

    // Enable testing of parameters in editor only
    private void OnValidate()
    {
    #if UNITY_EDITOR
        Cleanup();
        shader = Shader.Find(shaderName);
        camera.enabled = true;
        enabled = true;
    #endif
    }
}
