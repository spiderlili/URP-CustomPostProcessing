> Important: Be sure to backup your project using source control before following the steps in this section. This process will convert assets, and Unity does not provide an undo option! If you use source control, you will be able to revert to previous versions of the assets if necessary.

What to expect:
- your scenes will turn to magenta pink: shaders used by the materials in a Built-In Render Pipeline project are not supported in URP ->  Window > Rendering > Render Pipeline Converter. Choose Convert Built-In to 2D (URP)
- Custom shaders are not converted using the Material Upgrade converter. they need to be converted manually by a graphics engineer.

If you create a new project using the Universal Render Pipeline or 3D (URP) templates: URP Assets are already available in the project:
1. UniversalRP_Renderer, a Renderer Data Asset that you can use to filter the layers the renderer works on, and intercept the rendering pipeline to customize how the scene is rendered. This way, you can facilitate the creation of high-quality effects). the UniversalRP_Renderer controls high-level rendering logic and passes for URP. It supports Forward and Deferred paths, and a 2D Renderer that enables features such as 2D Lights, 2D Shadows, and Light Blend Styles. You can even extend URP to create your own renderers.
2. The other URP Asset provides options for controlling the Quality, Lighting, Shadows and Post-processing settings.. You can use different URP Assets to control the Quality settings, a process outlined further down in this section. This Settings Asset is linked to the Renderer Data Asset via the Renderer List. When you create a new URP Asset, the Settings Asset will have a Renderer List containing a single item – the Renderer Data Asset created at the same time, set as the default. You can add alternative Renderer Data Assets to this list.

1. add the URP package to your project: it was not included in Unity before Unity 6.
2. Window > Package Manager: click the Packages drop-down to add URP to your project. Select the Unity Registry, followed by Universal RP. Click Download in the lower-right corner of the window if the URP package is not yet installed. click Install once it’s downloaded.
3. create a URP Asset: right-click in the Project window and choose Create > Rendering > URP Asset (with Universal Renderer). Name the asset. 

# TODO
- Intro to the Universal Render Pipeline for advanced Unity creators (Unity 6 edition): 16 / 189
