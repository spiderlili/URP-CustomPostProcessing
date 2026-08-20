# Unity URP实现(伪)UI半透明磨砂(模糊)效果
原理实际上是获取屏幕画面，通过Shader模糊处理之后再覆盖到摄像机或UI上显示出来. 有2种方案：

1. 后处理效果方案: 实现一个自定义后处理，但是这样会影响整个画面/摄像机，而且渲染时机不好控制，一旦开启后处理，无时无刻都在计算
2. RawImage method: 把处理的画面转成RawImage，单独丢到某个UI上来精准控制，而且要求不高的话可以只在UI出现的那一帧处理一次节省资源。只糊一张图，随时开关的伪模糊

## Shader
Shader部分我这里以双重模糊(Dual Blur)举例，当然还有其他算法，推荐这篇文章：

下面是一个 Dual Blur（双重模糊） 的 Unity Shader，用于 高效实现高质量模糊效果，常用于 Bloom、UI 背景模糊、景深 等后处理效果，主要实现了2个函数。

1. Pass 0 (DownSample): 降采样 + 模糊：从原图采样 5 个点（中心 + 四角），平均后输出一半分辨率
2. Pass 1 (UpSample): 升采样 + 模糊：从低分辨率图采样 8 个点（4 角 + 4 边），插值后输出全分辨率

```
Shader "Hidden/DualBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRange ("Blur Range", Float) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Pass 0: DownSample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_downsample
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[5] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv[0] = v.uv;
                o.uv[1] = v.uv + float2(-1, -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[2] = v.uv + float2(-1,  1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[3] = v.uv + float2(1,  -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[4] = v.uv + float2(1,   1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                return o;
            }

            half4 frag_downsample (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv[0]) * 4;
                col += tex2D(_MainTex, i.uv[1]);
                col += tex2D(_MainTex, i.uv[2]);
                col += tex2D(_MainTex, i.uv[3]);
                col += tex2D(_MainTex, i.uv[4]);
                return col * 0.125; // sum / 8.0
            }
            ENDCG
        }

        // Pass 1: UpSample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_upsample
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[8] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv[0] = v.uv + float2(-1, -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[1] = v.uv + float2(-1,  1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[2] = v.uv + float2(1,  -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[3] = v.uv + float2(1,   1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[4] = v.uv + float2(-2,  0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[5] = v.uv + float2(0,  -2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[6] = v.uv + float2(2,   0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                o.uv[7] = v.uv + float2(0,   2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                return o;
            }

            half4 frag_upsample (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv[0]) * 2;
                col += tex2D(_MainTex, i.uv[1]) * 2;
                col += tex2D(_MainTex, i.uv[2]) * 2;
                col += tex2D(_MainTex, i.uv[3]) * 2;
                col += tex2D(_MainTex, i.uv[4]);
                col += tex2D(_MainTex, i.uv[5]);
                col += tex2D(_MainTex, i.uv[6]);
                col += tex2D(_MainTex, i.uv[7]);
                return col * 0.0833; // sum / 12.0
            }
            ENDCG
        }
    }
}
```

## 基于RawImage的方案
创建一个DualBlurController.cs，然后把下面的代码粘进去 (TODO: replace Graphics.Blit with Blitter API)：
```
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class DualBlurController : MonoBehaviour
{
    [Header("Blur Settings")]
    [Range(1, 5)] 
    [SerializeField] private int blurIterations = 5;      // 采样次数
    
    [Range(0.1f, 5f)] 
    [SerializeField] private float blurRange = 1.0f;      // 模糊范围

    [Header("Target Setup")]
    [SerializeField] private Camera sourceCamera;         // 渲染源相机
    [SerializeField] private RenderTextureFormat rtFormat = RenderTextureFormat.DefaultHDR;

    [SerializeField] private bool isUpdate;
    
    private RawImage _rawImage;
    private Material _blurMaterial;
    private RenderTexture[] _downRT;  // 降采样RT数组
    private RenderTexture[] _upRT;     // 升采样RT数组
    private bool _isUpdating;          // 标记是否正在更新

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _rawImage.color = Color.white;
        _blurMaterial = new Material(Shader.Find("Hidden/DualBlur"));
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
        
        // 1. 渲染源画面到初始RT
        var sourceRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, rtFormat);
        sourceCamera.targetTexture = sourceRT;
        sourceCamera.Render();
        sourceCamera.targetTexture = null;

        // 2. 设置Shader参数
        _blurMaterial.SetFloat("_BlurRange", blurRange);

        // 3. 执行Dual Blur流程
        RenderTexture currentRT = sourceRT;

        // DownSample阶段
        for (int i = 0; i < blurIterations; i++)
        {
            Graphics.Blit(currentRT, _downRT[i], _blurMaterial, 0);
            currentRT = _downRT[i];
        }

        // UpSample阶段
        for (int i = blurIterations - 1; i >= 0; i--)
        {
            Graphics.Blit(currentRT, _upRT[i], _blurMaterial, 1);
            currentRT = _upRT[i];
        }

        // 4. 最终结果输出到RawImage
        _rawImage.texture = currentRT;

        // 5. 释放临时RT
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
        // 不触发完整更新，只更新Shader参数
        _blurMaterial.SetFloat("_BlurRange", blurRange);
    }

    private void OnBlurSettingsChanged()
    {
        ReleaseRT();
        InitializeRT();
        UpdateBlur();
    }
}
```

### Unity Engine implementation:
1. 创建Canvas，在里面加入1个Panel覆盖全屏，把Image组件换成Raw Image，注意颜色不透明度得给满，不然会穿帮。
2. 把DualBlurController脚本丢进Raw Image object，把玩家视角的摄像机拖到参数里
3. 虽然现在看是白茫茫一片，但是启动游戏效果就出来了
4. 你可能会发现有滑块可以调节，但是调节了却没效果，这是因为没有勾选Is Update选项，默认只会在第一帧模糊画面，勾选之后就能自由调节，但是也会在每一帧都重新渲染画面。
并且还可以通过修改颜色或者叠加UI来实现各种效果。

### Pros & Cons of RawImage:
- Pros: 只对指定UI矩形生效，可做局部毛玻璃；不勾选isUpdate就只在首帧渲染，几乎零消耗；代码简单，内置管线也能跑。
- Cons: 本质是“截屏再糊”，游戏画面会穿帮（摄像机移动、物体动画需反复刷新）；多层UI穿插时层级难调；半透明UI叠加要自己再画一层颜色。
- Suitable usecase: 弹窗背景、暂停面板、设置界面等“静止或低频刷新”的局部模糊。

## Post-processing

# Unified Universal Blur
[TODO: Workaround video for UI](https://www.youtube.com/watch?v=CFcGRE1DJRQ)

### how Unity's Universal Render Pipeline (URP) and UI system handle drawing order: why a single Canvas fails for UI blur
- Overlay Canvases draw completely outside the URP pipeline at the very end of the frame. The blur render feature cannot see any of those background UI elements to blur them because they haven't been drawn yet when the screen is captured.
- Camera Canvases render during URP's Transparent pass. However, URP can only inject a render feature (like a blur screen grab) before or after the entire transparent queue finishes. It cannot interrupt a single Canvas mid-draw to take a snapshot of the background elements before drawing the foreground elements.

### The Camera Stacking Workaround
Split your UI across 2 cameras using URP's Camera Stacking feature -> forces Unity to finish rendering the scene and the background UI, capture the blur texture, and then render the foreground UI on top of it.

1. Configure the Base Camera: Use your Main Camera (set its Render Type to Base) to render your 3D scene. Create a Canvas set to Screen Space - Camera, attach the Main Camera to it, and place all of your background UI elements (the ones you want to be blurred) inside this Canvas.
2. Set the Blur Injection Point:Assign the Unified Blur Render Feature to the Renderer Data used by your Main Camera. Ensure the injection point is set to run after transparents (e.g., AfterRenderingTransparents or AfterRenderingPostProcessing) so the capture includes your background Canvas.
3. Add an Overlay Camera: Create a second Camera and set its Render Type to Overlay. Select your Main Camera, scroll down to the Stack section in the inspector, and add your new Overlay Camera to the list.
4. Build the Foreground Canvas: Create a second Canvas set to Screen Space - Camera and assign the Overlay Camera to it. Place your Blur UI Image (using the UniversalBlurUI material) at the back of this Canvas hierarchy. Place your sharp foreground UI elements in front of the Blur Image in the hierarchy.Because the Main Camera completely finishes its render loop before the Overlay Camera starts, the global blur texture is fully updated with the background UI. When the Overlay Camera draws your foreground Canvas, the Blur UI image successfully samples that background, and the rest of your foreground UI draws perfectly sharp on top.

### what if I have a scene with 2D sprites and UI images rather than a 3D scene?
The logic remains almost exactly the same. Unity’s Universal Render Pipeline (URP) treats 2D sprites (using SpriteRenderer) as transparent geometry. Because of this, the Camera Stacking workaround handles a 2D scene just as effectively as a 3D scene.
The main difference is ensuring both cameras are set up for 2D (Orthographic) and that your camera sizes match perfectly so the layers align.

1. Configure the Base 2D Camera: Select your Main Camera, set its Projection to Orthographic, and set its Render Type to Base. This camera will render your 2D scene (SpriteRenderer objects). Create a Canvas set to Screen Space - Camera, attach this Main Camera, and place your background UI elements here.
2. Add the Blur to your 2D Renderer: Locate the 2D Renderer Data asset your URP profile is using (often named Renderer2DData). Add the Unified Blur Render Feature to it. Set the injection point to AfterRenderingTransparents. This ensures the camera captures the screen after all your 2D sprites and background UI have been drawn.
3. Create the Overlay Camera: Create a second camera, set its Projection to Orthographic, and set its Render Type to Overlay. Crucial: Ensure the Size property of this Overlay camera exactly matches the Size of your Base camera, otherwise your UI and blur will misalign. Add this Overlay camera to the Main Camera's Stack.
4. Build the Foreground Canvas: Create your second Canvas set to Screen Space - Camera and assign the Overlay Camera to it. Place your Blur UI Image at the top of this Canvas hierarchy (so it draws first/in the back), and place your crisp foreground UI elements below it.

Note on sorting layers: In a standard 2D Unity game, you usually rely on Sorting Layers to dictate what draws in front of what. When you use Camera Stacking, Camera order completely overrides Sorting Layers. Even if a Sprite has a Sorting Layer of "Foreground" and an order of 9999, if it is rendered by the Base Camera, it will always appear behind everything rendered by the Overlay Camera. Make sure any 2D elements that need to appear in front of the blur panel are either moved to the Foreground UI Canvas, or rendered by a third camera added to the end of the stack.

# TODO
- https://zhuanlan.zhihu.com/p/1956060293634459269
- https://zhuanlan.zhihu.com/p/499488452
- [Blit in URP](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/customize/blit-overview.html)
- https://github.com/lukakldiashvili/Unified-Universal-Blur
- https://www.youtube.com/watch?v=eAR8XYIMUxQ
- https://www.youtube.com/watch?v=CFcGRE1DJRQ
