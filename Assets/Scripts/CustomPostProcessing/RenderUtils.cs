using UnityEngine;

public enum BlendingMode
{
    Opaque,
    Transparent,
    Add,
    Multiply,
    PremultipliedTransparency
}

public static class RenderingUtils
{
    public static void SetMaterialBlendMode(BlendingMode blendingMode, Material material, int sourceBlendId, int destinationBlendId)
    {
        switch (blendingMode) {
            case BlendingMode.Transparent:
                material.SetFloat(sourceBlendId, 5f);
                material.SetFloat(destinationBlendId, 10f);
                break;
            case BlendingMode.Opaque:
                material.SetFloat(sourceBlendId, 1f);
                material.SetFloat(destinationBlendId, 0f);
                break;
            case BlendingMode.Multiply:
                material.SetFloat(sourceBlendId, 2f);
                material.SetFloat(destinationBlendId, 0f);
                break;
            case BlendingMode.Add:
                material.SetFloat(sourceBlendId, 1f);
                material.SetFloat(destinationBlendId, 1f);
                break;
            case BlendingMode.PremultipliedTransparency:
                material.SetFloat(sourceBlendId, 1f);
                material.SetFloat(destinationBlendId, 10f);
                break;
        }
    }
}

