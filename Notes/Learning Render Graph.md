#  Introduction to the Render Graph in Unity 6 (2025)

### What is Render Graph?
- a system built on top of Unity's scriptable render pipeline:
  - automatically optimises runtime rendering performance -> allows for broader & safer access to the pipeline's resources, reduces the amount of memory URP uses & makes memory management more efficient. only allocates resources the frame actually uses -> no longer need to write complicated logic to handle resource allocation & account for rare worst-case scenarios. 
  - generates correct synchronization points between the compute & graphics queues -> reduces frame time.
    - Graphics queues handle rendering tasks (vertex/pixel shaders, rasterization)
    - Compute queues (often used for Async Compute) specialize in general-purpose parallel computing (GPGPU) like physics or AI 
  - improves the way you customise & maintain the render pipeline
- Render Graph enables us to create full-screen effects via custom passes
- you don't have to compromise on style for better performance or reliability

### Render Graph Viewer: visualise how render passes use frame resources & debug the rendering process
> in CS: graphs are data structures with nodes & connections -> visualise the render pipeline in a more intuitive way
- Window > Analysis > Render Graph Viewer
- Render Graph Viewer shows:
  - the render passes in the order that URP executes them: from Initialize Frame to Blit Post Processing
  - the resources the render passes use in the order that URP creates them
  - colored grids represent access blocks that indicate how the render passes use the resources
  - blue lines show that URP has merged the passes indicated into a single, more efficient pass

#### Example: how to see where a renderer feature sits in your render pipeline: 
- Assets > Settings > ForwardRenderer (Universal Renderer Data) with a renderer feature (.e.g. SSAO)
- Blit SSAO pass in Render Graph Viewer: the effect is applied as a block operation, efficiently transferring & modifying pixel data from these source textures to create a new texture with the SSAO effect applied
  - source textures = green access blocks
  - irrelevant / ignored textures = grey blocks 
  - this SSAO pass reads the camera depth texture & the camera normals texture: resources used to decide where the shadowing should occur; then it reads & writes to screen space occlusion textures `_ScreenSpaceOcclusionTexture`.
  - globe icon indicates this render pass sets this texture as a global resource
- there are a few other Blits:
  - Blit Bloom Mipmaps 
  - Blit Post Prcessing 
- switch off SSAO, refresh & switch to game view -> SvSAO pass is no longer part of the pipeline

2 tools to examine the render passes in Window > Analysis:
1. Rendering Debugger: visualises lighting, rendering, material characteristics; can customise this with your own visualisations in Rendering Debug. Depth overlay in Map Overlays is particularly useful - adjust Camera's Clipping Planes values
2. Frame Debugger: check the render passes & draw calls in the rendering loop. info mirrors the render graph viewer .e.g. DrawDepthNormalPrepass -> SSAO -> DrawOpaqueObjects

### What is a blit?
- Bit Block Transfer: a common CG operation for manipulating large blocks of pixels
- instead of processing pixels individually, Blit treat sections of a frame as blocks & apply operations to these blocks.

### What is a renderer feature? 
- a renderer feature can be utilised at any stage of the pipeline to influence the final render

# Example: creating a post-processing dither renderer feature using a material to process each pixel in the image
- Create > Scripting > URP Renderer Feature: DitherEffectRendererFeature - a C# script containing a template for a renderer feature with 2 classes:
  - `ScriptableRendererFeature`: Renderer Feature manager class that specifies the settings, manages the lifecycle & configuration of the passes to set up the task. there might be multiple passes within a feature.
  - `ScriptableRenderPass`: worker class that defines the actual rendering logic & actions

# Blur example
[cont: 0649](https://www.bilibili.com/video/BV1G8gozxE2m/?spm_id_from=333.337.search-card.all.click&vd_source=c52aacd3530e7f3dabe93deb78074f99)

# TODO
- Unity 6 update to ebook: Universal Render Pipeline for advanced Unity creators - example on how to create a full-screen color tint
- online documentation example: creating a blur renderer feature (seems to have been deleted - obsolete)
- [Unity 6 Render Graph documentation](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/render-graph.html)
- https://catlikecoding.com/unity/custom-srp/6-2-0/
