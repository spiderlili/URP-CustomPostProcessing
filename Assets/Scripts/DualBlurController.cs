using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DualBlurController : MonoBehaviour
{
    [Header("Blur Settings")]
    [Range(1, 5)] 
    [SerializeField] private int blurIterations = 5;      // Sample iterations
    
    [Range(0.1f, 5f)] 
    [SerializeField] private float blurRange = 1.0f;      

    [Header("Target Setup")]
    [SerializeField] private Camera sourceCamera;         // Render source camera
    [SerializeField] private RenderTextureFormat rtFormat = RenderTextureFormat.DefaultHDR;

    [SerializeField] private bool isUpdate;
    
    private RawImage _rawImage;
    private Material _blurMaterial;
    private RenderTexture[] _downRT;  // downsample RT array
    private RenderTexture[] _upRT;     // upsample RT array
    private bool _isUpdating;          // Test blur result at runtime

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _rawImage.color = Color.white;
        _blurMaterial = new Material(Shader.Find("PostProcessing/DualBlur"));
        if (sourceCamera == null) sourceCamera = Camera.main;
    }

    private void OnEnable()
    {
        InitializeRT();
        UpdateBlur();
    }

    private void OnDisable()
    {
        ReleaseRT();
    }

    private void InitializeRT()
    {
        int width = Screen.width;
        int height = Screen.height;

        _downRT = new RenderTexture[blurIterations];
        _upRT = new RenderTexture[blurIterations];

        for (int i = 0; i < blurIterations; i++)
        {
            width = Mathf.Max(width / 2, 1);
            height = Mathf.Max(height / 2, 1);

            _downRT[i] = CreateRT(width, height);
            _upRT[i] = CreateRT(width, height);
        }
    }

    private RenderTexture CreateRT(int width, int height)
    {
        var rt = new RenderTexture(width, height, 0, rtFormat)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return rt;
    }

    private void ReleaseRT()
    {
        for (int i = 0; i < blurIterations; i++)
        {
            if (_downRT[i] != null) _downRT[i].Release();
            if (_upRT[i] != null) _upRT[i].Release();
        }
    }

    private void UpdateBlur()
    {
        if (_isUpdating) return;

        _isUpdating = true;
        
        // Render source image to initial RT
        // FIXED: Changed depth buffer from 0 to 24 to satisfy Render Graph API camera target requirements
        // URP Render Graph API require a Camera's target Render Texture to have a depth buffer to properly render the scene geometry.
        var sourceRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, rtFormat);
        sourceCamera.targetTexture = sourceRT;
        sourceCamera.Render();
        sourceCamera.targetTexture = null;

        // Set Shader parameters
        _blurMaterial.SetFloat("_BlurRange", blurRange);

        // Execute Dual Blur pipeline
        RenderTexture currentRT = sourceRT;

        // DownSample
        for (int i = 0; i < blurIterations; i++)
        {
            Graphics.Blit(currentRT, _downRT[i], _blurMaterial, 0);
            currentRT = _downRT[i];
        }

        // UpSample
        for (int i = blurIterations - 1; i >= 0; i--)
        {
            Graphics.Blit(currentRT, _upRT[i], _blurMaterial, 1);
            currentRT = _upRT[i];
        }

        // Output final result to RawImage
        _rawImage.texture = currentRT;

        // Release temporary RT
        RenderTexture.ReleaseTemporary(sourceRT);

        _isUpdating = false;
    }

    private void LateUpdate()
    {
        if(isUpdate && !_isUpdating) UpdateBlur();
    }

    public void SetBlurIterations(float value)
    {
        if (_isUpdating) return;
        
        int newValue = Mathf.RoundToInt(value);
        if (newValue != blurIterations)
        {
            blurIterations = newValue;
            OnBlurSettingsChanged();
        }
    }

    public void SetBlurRange(float value)
    {
        blurRange = value;
        // 不触发完整更新，only update Shader parameter
        _blurMaterial.SetFloat("_BlurRange", blurRange);
    }

    private void OnBlurSettingsChanged()
    {
        ReleaseRT();
        InitializeRT();
        UpdateBlur();
    }
}