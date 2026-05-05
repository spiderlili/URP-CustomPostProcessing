using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlitRenderFeature : ScriptableRendererFeature
{
    public MyFeatureSettings Settings = new MyFeatureSettings();
    private BlitRenderPass renderPass;

    private RTHandle renderTextureHandle;

    public override void Create()
    {
        renderPass = new BlitRenderPass(
            "Custom Pass",
            Settings.WhenToInsert,
            Settings.MaterialToBlit
        );
    }

    // Called every frame once per camera
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Settings.IsEnabled) {
            // Can do nothing this frame if we want
            return;
        }

        // Gather up and pass any extra information our pass will need.
        // In this case we're getting the camera's color buffer target
        
        // Remove old code: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/upgrade-guide-2023-2.html
        // var cameraColorTargetIdent = renderer.cameraColorTarget;
        // renderPass.Setup(cameraColorTargetIdent);

        // Ask the renderer to add our pass.
        // Could queue up multiple passes and/or pick passes to use
        renderer.EnqueuePass(renderPass);
    }
    
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        // The target is used after allocation
        renderPass.Setup(renderer.cameraColorTarget);
    }

    [Serializable]
    public class MyFeatureSettings
    {
        // Free to put whatever we want here, public fields will be exposed in the inspector
        public bool IsEnabled = true;
        public RenderPassEvent WhenToInsert = RenderPassEvent.AfterRendering;
        public Material MaterialToBlit;
    }
}
