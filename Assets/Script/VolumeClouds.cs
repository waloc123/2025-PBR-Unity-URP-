using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[RequireComponent(typeof(PostProcessVolume))]
public class VolumeClouds : PostProcessEffectSettings
{
    public Vector3 boundsMin = new Vector3(-50, 0, -50);
    public Vector3 boundsMax = new Vector3(50, 100, 50);
    public Texture3D shapeNoise;
    public Texture3D detailNoise;
    [Range(0, 1)] public float density = 0.1f;
    [Range(0, 1)] public float lightAbsorption = 0.5f;
}


public sealed class VolumeCloudsRenderer : PostProcessEffectRenderer<VolumeClouds>
{
    public override void Render(PostProcessRenderContext context)
    {
        var sheet = context.propertySheets.Get(Shader.Find("Hidden/VolumeClouds"));
        sheet.properties.SetVector("_BoundsMin", settings.boundsMin);
        sheet.properties.SetVector("_BoundsMax", settings.boundsMax);
        sheet.properties.SetTexture("_ShapeNoise", settings.shapeNoise);
        sheet.properties.SetTexture("_DetailNoise", settings.detailNoise);
        sheet.properties.SetFloat("_Density", settings.density);
        sheet.properties.SetFloat("_LightAbsorption", settings.lightAbsorption);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }
}