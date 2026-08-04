using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

// Template for compute shader
public class RingHighlightRendererFeature : ScriptableRendererFeature
{
    [SerializeField] RingBlurHighlightRendererFeatureSettings settings;
    RingBlurHighlightRendererFeaturePass m_ScriptablePass;

    /// The Create() method is responsible for instantiating the Render Pass
    /// and configuring its initial state
    public override void Create()
    {
        m_ScriptablePass = new RingBlurHighlightRendererFeaturePass(settings);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        // You can request URP color texture and depth buffer as inputs by uncommenting the line below,
        // URP will ensure copies of these resources are available for sampling before executing the render pass.
        // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
        //m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

        // You can request URP to render to an intermediate texture by uncommenting the line below.
        // Use this option for passes that do not support rendering directly to the backbuffer.
        // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
        //m_ScriptablePass.requiresIntermediateTexture = true;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    // This is where we tell the renderer to use the Render Pass created in the Create() method.
    // During this stage, the Renderer Feature registers the pass with the active
    // renderer so it can be executed as part of the frame rendering process.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Check if the system support compute shaders, if not, make an early exit.
        if (!SystemInfo.supportsComputeShaders)
        {
            UnityEngine.Debug.LogWarning("Device Does not support compute shaders. The pass will be skipped.");
            return;
        }
        // Skip the render pass if the compute shader is null.
        if (settings.computeShader == null)
        {
            UnityEngine.Debug.LogWarning("The compute shader is null. The pass will be skipped.");
            return;
        }
        renderer.EnqueuePass(m_ScriptablePass);
    }

    // Use this class to pass around settings from the feature to the pass
    // Define the user-configurable parameters that appear in the inspector
    [Serializable]
    public class RingBlurHighlightRendererFeatureSettings
    {
        public ComputeShader computeShader;
        [Range(1.0f, 50f)] 
        public float blurRadius = 20f;
        [Range(0.0f, 1000.0f)] 
        public float radius = 50f;
        [Range(0.0f, 50.0f)] 
        public float softenEdge = 10f;
        [Range(0.0f, 1.0f)] 
        public float shade = 0.5f;
        public string trackedObjectName = "Hips"; 
    }

    class RingBlurHighlightRendererFeaturePass : ScriptableRenderPass
    {
        private int m_Kernel; // stores the index of the compute shader kernel that will be executed
        private int m_HorzKernel;
        
        // readable name for Render Graph Viewer
        private String m_BlurPassName = "BlurHighlight"; 
        private String m_RingPassName = "RingHIghlight";    
        
        // cache the transform of the tracked object in the scene
        // convert the tracked object's world position to screen space & used as the centre of the highlight ring -> follow the character as it moves 
        private Transform m_Transform; 
        readonly RingBlurHighlightRendererFeatureSettings settings;

        public RingBlurHighlightRendererFeaturePass(RingBlurHighlightRendererFeatureSettings settings)
        {
            this.settings = settings;
            if (settings.computeShader != null)
            {
                m_HorzKernel = settings.computeShader.FindKernel("HorzPass");
                m_Kernel = settings.computeShader.FindKernel("Highlight");
            }
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        // allows the Render Graph to transfer the required data from the recording stage to the execution stage of the pass.
        // Variables correspond directly to the parameters declared in the compute shader
        private class ComputePassData
        {
            public ComputeShader compute;
            public int kernel;
            public TextureHandle source;
            public TextureHandle destination;
            public int width;
            public int height;
            public float radius;
            public float edgeWidth;
            public float shade;
            public Vector3 center;
            public int horzKernel;
            public TextureHandle horzBlur;
            public float blurRadius;
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands. This contain the actual GPU dispatch logic.
        static void ExecutePass(ComputePassData data, ComputeGraphContext context)
        {
            if (data.compute == null)
                return;
            
            // Bind the source texture to the compute shader. Use SetComputeTextureParam() to
            // associate the source texture declared in the compute shader with data.sourceTextureHandle.
            // This allows the compute shader to read from the current camera color buffer.
            context.cmd.SetComputeTextureParam(
                data.compute,
                data.horzKernel,
                "source",
                data.source);
            
            context.cmd.SetComputeTextureParam(
                data.compute,
                data.horzKernel,
                "horzBlur",      
                data.horzBlur);   
            
            context.cmd.SetComputeTextureParam(
                data.compute,
                data.kernel,
                "source",      
                data.source);   
            
            context.cmd.SetComputeTextureParam(
                data.compute,
                data.kernel,
                "horzBlur",      
                data.horzBlur);   
            
            // Bind the destination texture as a writable UAV. Use SetComputeTextureParam() 
            // to assign the destination texture. This texture is created with random write enabled
            // and will receive the output generated by the compute shader.
            context.cmd.SetComputeTextureParam(
                data.compute,
                data.kernel,
                "destination",
                data.destination);
            
            // Set all required shader parameters. Pass the scalar and vector values to the compute
            // shader using the appropriate parameter methods. Ensure these names match the
            // variables defined inside the compute shader so the GPU receives the correct execution
            // data.
            context.cmd.SetComputeIntParam(data.compute, "width", data.width);
            context.cmd.SetComputeIntParam(data.compute, "height", data.height);
            context.cmd.SetComputeFloatParam(data.compute, "radius", data.radius);
            context.cmd.SetComputeFloatParam(data.compute, "edgeWidth", data.edgeWidth);
            context.cmd.SetComputeFloatParam(data.compute, "shade", data.shade);
            context.cmd.SetComputeVectorParam(data.compute, "center", data.center);
            context.cmd.SetComputeFloatParam(data.compute, "blurRadius", data.blurRadius);
  
            // This matches the thread group size (8, 8, 1) defined in the Highlight kernel and ensures
            // full screen coverage.
            int gx = Mathf.CeilToInt(data.width / 8.0f);
            int gy = Mathf.CeilToInt(data.height / 8.0f);
            
            // Dispatch the compute shader. Call DispatchCompute() with the compute shader,
            // kernel index, calculated group counts to execute the kernel across the entire
            // render target.
            context.cmd.DispatchCompute(data.compute, data.horzKernel, gx, gy, 1);
            context.cmd.DispatchCompute(data.compute, data.kernel, gx, gy, 1);
        }
        
        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        // RecordRenderGraph acts like a GPU planner. Use it to describe to the Render Graph which resources are required, such as
        // textures or buffers (inputs and outputs), and which shader or command buffer operations
        // should be executed.
        // Render Graph uses this information to automatically manage resource lifetimes, schedule execution efficiently,
        // and avoid unnecessary memory allocations or redundant work.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {   
            if (settings.computeShader == null) return;
            
            const string passName = "Render Custom Pass - Ring Highlight";
            
            // Fetch the frame data textures
            var resourceData = frameData.Get<UniversalResourceData>();
            
            // Provide information about the active camera.
            // Use this data to obtain screen dimensions & convert world positions into screen space coordinates
            var cameraData = frameData.Get<UniversalCameraData>();
            
            // Access the activeColorTexture, which represents the current camera output at this
            // stage of the rendering pipeline, and use it as the input source for the compute shader.
            // Verify that the texture is valid before continuing.
            TextureHandle source = resourceData.activeColorTexture;

            if (!source.IsValid())
            {
                return;
            }

            // Get a texture descriptor from the source texture to ensure
            // the output texture matches the required format & resolution.
            // TODO: ArgumentException: The passed in texture handle does not have a valid descriptor. (This is most commonly cause by the handle referencing a built-in texture such as the system back buffer.)
            // Parameter name: handle
            // https://docs.unity3d.com/6000.4/Documentation/Manual/urp/render-graph-optimize.html
            
            var desc = renderGraph.GetTextureDesc(source);
            
            // Disable MSAA because unordered access views (UAVs), required for compute shaders,
            // cannot operate on multi-sampled textures. Turn off MSAA on URP asset.
            desc.msaaSamples = MSAASamples.None;
            // Enable random write access by setting enableRandomWrite to true, allowing the compute shader to write directly into the texture as an RWTexture2D.
            desc.enableRandomWrite = true;
            
            // identify this texture during debugging and when inspecting the Render Graph Viewer
            desc.name = $"_ComputeHorzOutput_{m_BlurPassName}";
            TextureHandle horzBlur = renderGraph.CreateTexture(desc);
            
            // Create the output texture to store the result generated by the compute shader.
            desc.name = $"_ComputeOutput_{m_RingPassName}";
            TextureHandle computeOutput = renderGraph.CreateTexture(desc);

            // The camera pixel width & height are retrieved to determine the dispatch size and to
            // calculate screen space parameters.
            int width = cameraData.camera.pixelWidth;
            int height = cameraData.camera.pixelHeight;

            // The radius value is converted from a normalized % into pixel space.
            // This ensures the effect scales correctly regardless of resolution.
            float rad = (settings.radius / 100.0f) * height;
            
            // Calculate the edge width based on the radius to control the softness of the highlight transition
            float edgeWidth = rad * settings.softenEdge / 100.0f;
            
            // Initialize the center position to the middle of the screen using bit-shift operations for efficiency.
            Vector3 center = new(width >> 1, height >> 1);
            
            if ( m_Transform != null)
            {
                // Convert the tracked object’s world position into screen space and
                // update the center position so the highlight ring follows the target object.
                Vector3 pos = cameraData.camera.WorldToScreenPoint(m_Transform.position);
                center.x = pos.x;
                center.y = pos.y;
            }
            
            // Add a compute pass and store the returned builder.
            // use this pattern to ensure Dispose() is called when the scope ends,
            // even if an exception occurs, improving memory management & keeping the pass registration self-contained
            // Populate the ComputePassData output (passData) with all input parameters required
            // by the compute dispatch, including the compute shader reference, kernel index, source
            // and destination texture handles, resolution values, the circle settings (radius, edge
            // width, shade, and center). Ensure these values match the compute shader parameters
            // expected during execution.
            using (var builder = renderGraph.AddComputePass<ComputePassData>($"{m_BlurPassName} Compute", out var passData))
            {
                // Check whether the tracked transform has already been cached.
                // If not, locate the object using its configured name and store its transform reference to avoid repeated searches.
                if (m_Transform == null && settings.trackedObjectName != "")
                {
                    GameObject go = GameObject.Find(settings.trackedObjectName);
                    if (go)
                    {
                        m_Transform = go.transform;
                    }
                }
                
                passData.compute = settings.computeShader;
                passData.kernel = m_Kernel;
                passData.source = source;
                passData.destination = computeOutput;
                passData.width = width;
                passData.height = height;
                passData.radius = settings.radius;
                passData.edgeWidth = edgeWidth;
                passData.shade = settings.shade;
                passData.center = center;

                passData.horzKernel = m_HorzKernel;
                passData.horzBlur = horzBlur;
                passData.blurRadius = settings.blurRadius;
                
                // Declare resource access for the pass by setting AccessFlags.Read for the source
                // texture and AccessFlags.Write for the destination texture. Use these flags to
                // communicate intended texture usage to the Render Graph so it can build correct
                // dependencies and schedule the pass safely.
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseTexture(passData.horzBlur, AccessFlags.ReadWrite);
                builder.UseTexture(passData.destination, AccessFlags.Write);

                // Register the execution callback with SetRenderFunc() and passing a
                // static function that receives ComputePassData and ComputeGraphContext.
                // Route this callback to ExecutePass(data, ctx) so the Render Graph can invoke the compute dispatch at execution time,
                // after the required code is implemented inside ExecutePass()
                builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext ctx) =>
                    ExecutePass(data, ctx));            }

            // The compute shader will write to the computeOutput texture. To avoid a blit: set this
            // texture as cameraColor, so subsequent passes use it as the activeColorTexture.
            resourceData.cameraColor = computeOutput;
        }
    }
}
