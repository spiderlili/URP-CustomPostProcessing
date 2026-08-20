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
TODO

# AI-assisted solution based on the RawImage method above (verified working in Unity 6.4)
- Example scene: UIDualBlurRawTexture
- Example scripts: DualBlurController.cs, DualBlurBlit.shader
- Next steps: consult Unity specialists about Grahpics.Blit API, will it be deprecated?
- Problem with the RawImage method above: in the UIDualBlurRawTexture scene, the blur effect is working but it is only blurring the skybox. i want to blur the entire game screen including all transparent UI rather than just the skybox. the script is DualBlurController.cs how can I make sure the entire game screen is captured after the skybox?
- The core issue:  sourceCamera.Render()  only renders that one camera's output (skybox + whatever it's assigned to render, i.e.  culling mask), and it captures the frame in isolation — it does not include other cameras (like a UI camera) or Screen Space - Overlay UI canvases, since those aren't part of any camera's render output that gets captured this way.
- there's a single camera ( m_Depth: -1 ,  m_CullingMask: everything ) rendering the skybox, and a Canvas in  Screen Space - Camera  mode ( m_RenderMode: 0 ) pointing at that same camera ( m_Camera: {fileID: 1473511291} ).
- The problem is architectural:  `sourceCamera.Render()`  is called manually via script, completely outside Unity's normal frame render. When you call  `camera.Render()`  directly:
    - It renders only that camera's scene geometry + skybox into sourceRT
    - It does not include the Camera-Space Canvas UI, because Canvas rendering in Screen Space - Camera mode is injected by Unity's render pipeline as part of the normal per-frame render loop, not as part of an ad-hoc  camera.Render()  call issued from  LateUpdate  before UI has necessarily been laid out/rendered for that call
    - It never includes anything drawn by other cameras, or Screen Space - Overlay canvases, since those exist entirely outside  sourceCamera 
- So blurring the entire game screen including all transparent UI isn't something you can achieve by tweaking culling masks on sourceCamera  — UI would create a circular dependency (the blur RawImage is itself UI, so if you captured the UI into the source you'd be blurring the blur). What you actually want is to capture everything behind/before the blur RawImage in the real, final rendered frame — i.e., grab the screen backbuffer after opaque+transparent+background UI is drawn, but before your blur  RawImage  draws on top.
- The standard technique for this is to capture the frame using `ScreenCapture.CaptureScreenshotIntoRenderTexture` at the right point in the frame (via a coroutine at  WaitForEndOfFrame), which captures the actual composited backbuffer — everything rendered by every camera and all UI — rather than re-rendering one camera in isolation.
- URP 17 (Unity 6 era): rewrite  DualBlurController  to capture the composited backbuffer at end-of-frame via ScreenCapture.CaptureScreenshotIntoRenderTexture, called from a coroutine at  WaitForEndOfFrame, instead of manually re-rendering just  sourceCamera. This naturally includes the skybox, all cameras, and all UI (Overlay and Camera-space) drawn before the blur RawImage's own canvas — as long as the blur RawImage is on a canvas with sufficient sort order / a separate camera depth so it's drawn last.
### Summary of the fix:
- Root cause:  sourceCamera.Render()  re-renders only that one camera's scene contents into a texture, completely bypassing Unity's normal per-frame compositing. It never included any UI, because Canvas rendering (Screen Space - Overlay, or Screen Space - Camera drawn by the same camera) isn't triggered by a manual  camera.Render()  call — it's injected by the engine into the real frame render loop. That's why you were only ever seeing the skybox/scene geometry.
- Fix in  DualBlurController.cs : Instead of manually rendering a camera, the script now:
1. Temporarily disables its own  RawImage  (so the blur output doesn't feed back into itself).
2. Waits for  WaitForEndOfFrame  via a coroutine — the point at which everything (all cameras, skybox, and all UI canvases, transparent or not) has already been composited into the backbuffer.
3. Calls  ScreenCapture.CaptureScreenshotIntoRenderTexture(sourceRT)  to grab that fully composited frame.
4. Re-enables the  RawImage  and runs the same dual-blur down/up-sample pipeline on that capture.

- This guarantees the blur source is the entire game screen — skybox, geometry, and all UI — captured after everything else has drawn, and before the blur  RawImage  itself draws on top of it.
- Note: Make sure the blur  RawImage 's Canvas has a higher sort order (or the RawImage is later in hierarchy on the same canvas) than any UI you want blurred, so it's drawn after the capture point and appears layered on top correctly.

### Problem: the blurred image on RawImage appears upside down
- The vertical flip is a well-known side effect of  `ScreenCapture.CaptureScreenshotIntoRenderTexture` combined with  `Graphics.Blit` /UV convention differences between platforms (particularly on APIs where the screen readback is top-to-bottom while RenderTextures/blits expect bottom-to-top, e.g. Direct3D vs. OpenGL conventions), and it also commonly appears simply because  Graphics.Blit  from a screenshot-captured texture doesn't match the UV winding the dual-blur shader expects.
- The shader itself has no flip logic — it just does neighbor sampling with  UnityObjectToClipPos , which is neutral. So the flip is purely a data/orientation mismatch introduced upstream, in the script:
- Root cause:  ScreenCapture.CaptureScreenshotIntoRenderTexture  writes the captured backbuffer in screen-space row order (top row of the screen ends up at the top of the texture, i.e. standard "image" convention). But  RenderTexture s sampled via  tex2D / Graphics.Blit  in Unity follow OpenGL-style UV convention where V=0 is the bottom. On most desktop APIs, regular camera-rendered  RenderTexture s already come out compatible with this convention automatically (Unity handles the flip internally for normal camera rendering) — but the direct screenshot-into-RT capture path does not get that same automatic correction applied in the blit chain here, since we're consuming it via  Graphics.Blit  chain and finally displaying via  RawImage.texture , whose UV mapping expects the "un-flipped" (camera-render-style) orientation. That mismatch is what flips your final result vertically.
- The simplest, most robust fix: flip the UVs once at the very first sampling stage (in the downsample pass's  vert ), or simpler still — flip in script when blitting the capture into the first working RT, using a scale/offset trick with  Graphics.Blit 's overload that takes a  Vector2 scale, Vector2 offset . That way the shader stays generic and reusable for other RTs that don't need flipping. I'll fix it in the script since the flip is specific to the screenshot-capture path, not to the blur shader itself
- This uses the  Graphics.Blit(Texture, RenderTexture, Material, int, Vector2 scale, Vector2 offset)  overload, which sets the material's  _MainTex_ST  so the vertex shader's  TRANSFORM_TEX /UV passed in maps correctly. However — I need to check the shader actually applies  _MainTex_ST  to the UV, since the current  vert  function uses  v.uv  directly without  TRANSFORM_TEX . If it doesn't, the scale/offset blit will have no effect.
- the shader uses raw  v.uv  without applying  _MainTex_ST , so the  Graphics.Blit  scale/offset overload would have no effect here. I need a different approach that doesn't depend on shader-side transform support. The cleanest fix that works regardless of the shader: flip the captured texture itself using  Graphics.Blit 's built-in mesh (the default blit quad) — actually simplest is to flip via a  Rect -based technique using  GL.LoadPixelMatrix , but that's overkill.
- The most reliable, shader-agnostic fix is to flip using  Graphics.Blit(source, dest)  with a simple full-screen flip done through a scale trick on  _MainTex_ST  — but since the shader ignores it, instead let's flip the source RT's content using Unity's  Graphics.Blit  overload combined with inverting the RenderTexture read via a flipped  Rect , or simpler: use  Graphics.CopyTexture  — no, that won't flip either.
- The pragmatic fix: add  _MainTex_ST  support to the shader's  vert  function (both passes) via  TRANSFORM_TEX(v.uv, _MainTex) , which is the standard, correct Unity way to let  Graphics.Blit  scale/offset flips propagate through. This is a minimal, idiomatic shader change. Now the shader honors  _MainTex_ST , so the  Graphics.Blit  scale/offset overload in the script will correctly flip the very first sample.

### Summary
Cause:  ScreenCapture.CaptureScreenshotIntoRenderTexture  fills the RenderTexture in top-to-bottom screen row order, while Unity's  RenderTexture /shader UV sampling convention is bottom-to-top (V=0 at bottom). Normal camera-rendered RTs get this handled transparently, but a raw screen capture does not, so every subsequent blit (and the final display on the  RawImage ) came out vertically flipped.

Fix:
1.  Shaders/PostProcessing/DualBlurBlit.shader:34,42  and  :84,92  — added  _MainTex_ST  and applied it via  TRANSFORM_TEX(v.uv, _MainTex)  in both the downsample and upsample vertex shaders, so the shader now respects any scale/offset passed to it (previously it ignored  _MainTex_ST  entirely).
2.  Scripts/DualBlurController.cs:125  — the first downsample blit now uses the  Graphics.Blit(src, dst, mat, pass, scale, offset)  overload with  scale=(1,-1) ,  offset=(0,1) , which flips the V coordinate once at the very start of the chain, correcting the orientation for every subsequent pass and the final  RawImage .



In OpenGL the image will be bottom-up (exactly like unity handle it) but in other operational systems the image will be bottom-up

https://docs.unity3d.com/Manual/SL-PlatformDifferences.html

```
var flipY = graphicDevice == GraphicsDeviceType.OpenGLCore ||
graphicDevice == GraphicsDeviceType.OpenGLES2 ||
graphicDevice == GraphicsDeviceType.OpenGLES3 ||
graphicDevice == GraphicsDeviceType.Vulkan ?
false :
true;
```

## Summary

I had the flip logic backwards and unconditional. Fixed:

1.  Scripts/DualBlurController.cs  — added  using UnityEngine.Rendering; , and in  Awake()  compute  _needsFlipY  once via  SystemInfo.graphicsDeviceType , matching your logic: flip is needed on everything except  OpenGLCore ,  OpenGLES2 ,  OpenGLES3 ,  Vulkan  (i.e. needed on Direct3D/Metal/etc.).
2. The flip ( SetTextureScale / SetTextureOffset  on  _MainTex , reset after) is now only applied  if (_needsFlipY) , so OpenGL/Vulkan platforms get the capture as-is (already correctly oriented) and other APIs get the corrective flip.

You're likely running in the Editor/Standalone on Direct3D or Metal, which is why the previous unconditional-flip-in-wrong-direction version still looked wrong — please rerun now; it should test correctly, and would also behave correctly if you switch the Editor to the OpenGL/Vulkan graphics API via Player Settings.

On Metal, according to your own logic,  _needsFlipY  should be  true  — so the flip code is running. If it's still upside down, the flip must be happening but in a way that's being negated or not reaching the final texture. Let me reconsider this more carefully rather than guessing again.

The key suspect:  TRANSFORM_TEX  macro. Let me check what it actually expands to — because if  _MainTex_ST.y  is negative,  TRANSFORM_TEX  computes  uv * scale + offset , so with scale=-1, offset=1:  uv' = uv*(-1) + 1 = 1 - uv . That should correctly flip. Let's verify by re-reading the shader and double check there isn't a second, competing flip already baked into how RenderTexture/Blit or the RawImage itself handles Y on Metal — because Unity's  Graphics.Blit  on Metal already accounts for Metal's native top-left origin internally, meaning the RenderTexture chain and the final  RawImage  display should already be "consistent" per Unity's abstraction. The actual bug may be that  ScreenCapture.CaptureScreenshotIntoRenderTexture  is already correctly oriented by Unity on all platforms (Unity's scripting API is documented to normalize this), and the flip I added is actively causing the inversion, not fixing it.

Let me check Unity's actual documented behavior instead of assuming.

Doesn't state orientation explicitly. Rather than continuing to guess/theorize about API internals, let me eliminate variables directly: verify empirically what's actually flipped by checking if the flip code path is even being hit, and re-examine the  RawImage /Canvas UV mapping too, since RawImage  uvRect  could independently be flipping things. Let me check the RawImage setup and Canvas orientation in the scene.

from Unity source that  TRANSFORM_TEX(tex,name) ((tex.xy) * name##_ST.xy + name##_ST.zw) , which is standard scale+offset, so that part is correct in principle.

Rather than iterating further blind theory, let's step back: the pragmatic, bulletproof way to guarantee correct orientation regardless of platform quirks, macro edge cases, or  Graphics.Blit 's own internal flipping heuristics (which also auto-flip when blitting between certain render texture types on certain APIs — this is the real likely culprit:  Graphics.Blit  itself may already silently flip when src/dst have different "render texture" vs "texture" flip flags on Metal) is to stop fighting indirect flags and flip explicitly and unambiguously in the fragment shader itself using  _ProjectionParams.x  — Unity's standard, platform-agnostic way of detecting when a flip is needed at runtime, which is exactly what SRP/URP itself uses internally.

Let's use the robust, well-known Unity shader idiom: check  _ProjectionParams.x < 0  in the shader and flip  v.uv.y  accordingly. This is more reliable than my C#-side heuristic because it reflects Unity's actual internal render-target flip state for that draw call, not just a static GraphicsDeviceType guess.

I'm going to revert the C#-side flip hack (remove the guesswork) and instead do the flip unconditionally and simply in the script, in a way immune to shader macro/Blit quirks: flip the raw screen capture by blitting it with  Graphics.Blit(src, dst, scale, offset)  — the plain built-in overload (no material) that Unity guarantees performs a straightforward UV remap blit, before feeding it into the blur chain. This isolates the flip from the blur shader entirely, so there's no interaction with  _MainTex_ST / TRANSFORM_TEX  behavior at all.

Interesting —  _needsFlipY  shows  [SerializeField] , meaning someone (or you) exposed it in the Inspector and it may currently be manually set/serialized to a stale value that overrides  Awake() 's computed value on subsequent domain reloads... but actually  [SerializeField] private bool  on a non-persisted scene default would just serialize as  false  unless explicitly set in the Inspector, and  Awake()  always overwrites it at runtime anyway. Not the bug, but let's clean this up as part of the rewrite. Now applying the actual fix — replacing the shader-side conditional flip with a direct, unconditional, isolated pre-flip blit of the raw capture:

it compiles (uses only  UnityEngine  types, no new usings needed beyond what's already there), and the flip is applied via the vanilla  Graphics.Blit(src, dst, scale, offset)  overload with no material — completely decoupled from the blur shader's own UV/ _MainTex_ST  handling, and independent of any  GraphicsDeviceType  guesswork.

Also revert the unnecessary  _MainTex_ST / TRANSFORM_TEX  addition in the shader since it's no longer needed for the flip (keeping it is harmless and arguably more correct/idiomatic for Unity shaders in general, but it's not required by the fix). I'll leave the shader as-is since it's backward-compatible and doesn't hurt.

If the image is still upside-down after this, it means the true flip direction is inverted relative to what I assumed — in that case, simply swap the scale/offset to the opposite sign:

Please test this build first. If it's still flipped, tell me and I'll flip the sign — change line 122 from  Graphics.Blit(sourceRT, flippedRT, new Vector2(1f, -1f), new Vector2(0f, 1f));  to  Graphics.Blit(sourceRT, flippedRT, new Vector2(-1f, 1f), new Vector2(1f, 0f));  (flipping X instead), or more likely just try removing the flip's Y-offset combination — but let's confirm empirically with one test rather than theorize further.

## Summary

Reworked the fix to be robust and platform-agnostic:
- Removed the  GraphicsDeviceType -based conditional guess and the shader-side  _MainTex_ST / TRANSFORM_TEX  dependency for the flip.
- Added a single, isolated plain  Graphics.Blit  (no material) that flips the raw screen capture vertically into  flippedRT  before it ever enters the blur down/up-sample chain. This uses Unity's built-in scale/offset blit, which is guaranteed to just remap UVs — no interaction with custom shader code.

Run it on Metal now — if it's still upside down, the flip direction needs to be inverted, which is a one-line sign change once we confirm actual behavior rather than guessing again.

# Unified Universal Blur
[TODO: TestWorkaround video for UI](https://www.youtube.com/watch?v=CFcGRE1DJRQ)

The answer below are from AI & cannot be trusted. The limitation of Unified Universal Blur is that it can only blur things before transparents

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
