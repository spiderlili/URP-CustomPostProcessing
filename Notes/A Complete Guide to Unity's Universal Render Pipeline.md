# Shaders
## Converting custom unlit shaders to URP
## Working with Properties and includes
## tips when updating shaders for URP
## Post processing using Renderer Features
## Stencils

# URP Tips & Tricks
## Pipeline callbacks
## Post processing
## Camera Stacking

# Performance, Debugging, Profiling
## Performance
- pipeline settings
  - Enable the SRP Batcher to use the new batching method. The SRP Batcher will automatically batch together meshes that use the same shader variant, thereby reducing draw calls. If you have numerous dynamic objects in your scene, this can be a useful way to gain performance. 
  - Disable features that your project does not require, such as depth texture and opaque texture. 
- lighting
  - baked lighting is one of the best ways to improve the performance of your scene.
- camera settings
  - URP enables you to disable unwanted renderer processes on your cameras for performance optimization. This is useful if you’re targeting both high- and low-end devices in your project. Disabling expensive processes, such as post-processing, shadow rendering, or depth texture can reduce visual fidelity but improve performance on low-end devices.
  - occlusion culling: By default, the Camera in Unity will always draw everything in the Camera’s frustum, including geometry that might be hidden behind walls or other objects. There’s no point in drawing geometry that the player can’t see, and that takes up precious milliseconds. This is where occlusion culling comes in. Occlusion culling is best suited to a scene where significant numbers of objects might be masked when another item appears between them and the Camera.
## Frame Debugger
## Profiling
## URP Asset quality tiers

# TODO
- https://www.udemy.com/course/unity-urp/?srsltid=AU7gw4XS_jjIJ_cBGDIxYeKYf9ZBzUrQX3Rs5QeUGKTs2JZYJ_Ewb87T&couponCode=MT260928G2ANEW
- https://www.youtube.com/watch?v=NFBr21V0zvU
