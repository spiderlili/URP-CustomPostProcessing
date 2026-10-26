# Q: After importing the RenderGraph Samples, there are errors in the project: Assets/Samples/Universal Render Pipeline/17.4.0/URP RenderGraph Samples/Culling/CullingRenderPassRendererFeature.cs(125,43): error CS0117: 'RenderingUtils' does not contain a definition for 'CreateDrawingSettings'and Assets/Samples/Universal Render Pipeline/17.4.0/URP RenderGraph Samples/Culling/CullingRenderPassRendererFeature.cs(125,43): error CS0117: 'RenderingUtils' does not contain a definition for 'CreateDrawingSettings'. Why and how to fix them?

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
