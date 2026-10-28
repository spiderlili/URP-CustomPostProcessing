### Q: After importing the RenderGraph Samples, there are errors in the project: Assets/Samples/Universal Render Pipeline/17.4.0/URP RenderGraph Samples/Culling/CullingRenderPassRendererFeature.cs(125,43): error CS0117: 'RenderingUtils' does not contain a definition for 'CreateDrawingSettings'and Assets/Samples/Universal Render Pipeline/17.4.0/URP RenderGraph Samples/Culling/CullingRenderPassRendererFeature.cs(125,43): error CS0117: 'RenderingUtils' does not contain a definition for 'CreateDrawingSettings'. Why and how to fix them?

Found the likely cause: your project defines its own global RenderingUtils class. I'm checking it now.

Assets/Samples/Universal Render Pipeline/17.4.0/URP RenderGrap…/CullingRenderPassRendererFeature.cs)
Added 2 lines, removed 1 line
        foreach (ShaderTagId sid in forwardOnlyShaderTagIds)
            m_ShaderTagIdList.Add(sid);

        var drawSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, universa
lRenderingData, cameraData, lightData, sortFlags);
        // Fully qualified: the project's own global RenderingUtils class (Assets/Scripts/C
ustomPostProcessing/RenderUtils.cs) would otherwise shadow URP's.
        var drawSettings = UnityEngine.Rendering.Universal.RenderingUtils.CreateDrawingSett
ings(m_ShaderTagIdList, universalRenderingData, cameraData, lightData, sortFlags);

        var param = new RendererListParams(cullResults, drawSettings, filterSettings);
        passData.rendererListHandle = renderGraph.CreateRendererList(param);

Both errors are the same one.

Why it happened: Your project has its own RenderingUtils class in Assets/Scripts/CustomPostProcessing/RenderUtils.cs. It sits in the global namespace and only has SetMaterialBlendMode. In the sample, the name RenderingUtils resolves to your class before URP's UnityEngine.Rendering.Universal.RenderingUtils. Your class has no CreateDrawingSettings, so you get CS0117. URP 17.4.0 does have that method, so the package itself is fine.

Fix: In CullingRenderPassRendererFeature.cs, line 125 now calls UnityEngine.Rendering.Universal.RenderingUtils.CreateDrawingSettings(...) with the full namespace, and I added a comment explaining why.

If you'd rather keep the samples untouched, you could instead put your class in a namespace or rename it, for example to BlendingUtils. That means updating every place that calls RenderingUtils.SetMaterialBlendMode. Other URP samples may hit the same name clash later.

Nothing else in the project calls your RenderingUtils, so I'll rename it. That fixes this error and any other sample that uses URP's class.

The error came from the same name clash as before, and I fixed it by renaming your class. 

Your global RenderingUtils class in Assets/Scripts/CustomPostProcessing/RenderUtils.cs shadowed URP's RenderingUtils. The RendererList sample hit the same clash as the Culling one. I renamed your class to BlendingUtils. Nothing else in the project referenced RenderingUtils or SetMaterialBlendMode, so no other code needed changing. Both samples now resolve to URP's class.

The fully qualified call I added in the Culling sample is now redundant but harmless. I also shortened its comment. You can revert that line to plain RenderingUtils.CreateDrawingSettings(...) if you prefer.

If you use RenderingUtils.SetMaterialBlendMode anywhere outside Assets, for example in another project or a package, change it to BlendingUtils.SetMaterialBlendMode.


### Q: I saw a forum post which says the Graphics.Blit API might be deprecated in the future for URP and I'm not sure if that's true, would you be able to confirm if we can still use Graphics.Blit?
- it still works, even in Unity 7. But is not recommended for use in URP during frame rendering. Instead you should use renderGraph.AddBlitPass or AddCopyPass  or use Core RP Blitter class as there's some behind the scenes stuff that Graphics.Blit doesn't cover. For simple full screen passes, consider using the Full-Screen Pass render feature in your RenderData instead.
If you're literally just modifying a RenderTexture outside of the render loop, then you can still safely use Graphics.Blit.

### Q: in the UIDualBlurRawTexture scene there is a DualBlurController script attached to RawImage under UIDualBlurLayer. the script uses Graphics.Blit which is not recommended for use in URP during frame rendering. Instead you should use renderGraph.AddBlitPass or AddCopyPass  or use Core RP Blitter class as there's some behind the scenes stuff that Graphics.Blit doesn't cover. For simple full screen passes, consider using the Full-Screen Pass render feature in your RenderData instead. Is it possible to rewrite that for URP?

A:
Yes, it's possible, and I've rewritten it. 

Your script grabs the screen in WaitForEndOfFrame, after the render graph has finished. That rules out renderGraph.AddBlitPass and AddCopyPass, which only work inside a render feature. A Full-Screen Pass feature doesn't fit either, because the result goes into a RawImage texture. So I used the Core RP Blitter class recorded into a CommandBuffer.

Assets/Scripts/DualBlurController.cs
- Every Graphics.Blit is now a Blitter.BlitTexture call, recorded into one CommandBuffer and run once with Graphics.ExecuteCommandBuffer.
- The vertical flip uses the blitter's scale/bias ((1,-1,0,1)) instead of Graphics.Blit's scale/offset. The down and up passes and the render texture setup are otherwise unchanged.

Assets/Shaders/PostProcessing/DualBlurBlit.shader
- The shader used a quad vertex and _MainTex, which Blitter doesn't supply. I rewrote it in URP HLSL using URP's Blit.hlsl (a fullscreen triangle and _BlitTexture).
- The shader name, the _BlurRange property and both passes (0 = down, 1 = up) are unchanged, so your materials and the script's Shader.Find still work.
- The kernel weights are identical. The texel size is now read from the texture dimensions instead of _MainTex_TexelSize.

The same shader can now also be used in a Full-Screen Pass or a render graph pass later if you move the blur into the pipeline.

If the image comes out flipped, change the flip's scale/bias on the Blitter.BlitTexture(cmd, sourceRT, ...) line in DualBlurController.cs to (1,1,0,0).

Both errors came from line 129, the flip blit. The overload I used (BlitTexture(cmd, texture, scaleBias, mipLevel, bilinear)) only accepts an RTHandle and a RasterCommandBuffer. Your RenderTexture and CommandBuffer didn't match it. The overloads that take a material do accept a plain CommandBuffer and a RenderTexture.

Fix:
- Assets/Shaders/PostProcessing/DualBlurBlit.shader has a new pass 2, DualBlurCopy, which just samples and outputs the texture.
- Assets/Scripts/DualBlurController.cs now does the flip with Blitter.BlitTexture(cmd, sourceRT, new Vector4(1f, -1f, 0f, 1f), _blurMaterial, 2).

The blur passes are unchanged.

The render texture arrays are sized when the component is enabled. Changing blurIterations in the Inspector doesn't go through SetBlurIterations, so the arrays kept their old size. The blur loops then ran with the new count and indexed past the end of the arrays (line 136, the downsample loop). ReleaseRT had the same problem, because it also looped up to blurIterations.

Fix:
- Rebuild on mismatch: after the end-of-frame wait, CaptureAndBlur checks whether the array length matches blurIterations. If not, it releases and recreates the render textures before blurring.
- Release by array length: ReleaseRT now releases whatever is in the arrays and no longer reads blurIterations.

Changing blurRange in the Inspector is safe. It's only read as a shader value at capture time. It never caused the error.

### Vertex colour in URP
`output.color = unity_SpriteColor;` use this instead of _Color

`unity_SpriteColor` is important otherwise it will break batching if vertex color is used!
