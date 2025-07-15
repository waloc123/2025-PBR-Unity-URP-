using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumeCloudFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        
        public Vector3 boundsMin = new Vector3(-50, 0, -50);
        public Vector3 boundsMax = new Vector3(50, 100, 50);

        [Range(0.1f, 5f)] public float stepSize = 0.5f;
        [Range(8, 256)] public int stepCount = 64;
        [Range(0, 1)] public float density;

        [Header("Noise Settings")]
        public Texture3D perlinNoise;
        public Texture3D worleyNoise;
        [Range(0.1f, 10f)] public float noiseScale = 1.0f;
        [Range(0.5f, 5f)] public float detailScale = 2.0f;
        [Range(0f, 3f)] public float densityThreshold = 0.3f;

        [Header("Lighting")]
        [Range(0f,0.99f)] public float _shapeNoiseWeight = 0.7f;
        [Range(0f, 0.99f)] public float detailNoiseWeights= 0.3f;

        [Header("Lighting Settings")]
        public Color lightColor = Color.white;
        public Color _colorA = Color.white;
        public Color _colorB = Color.white;


        [Range(1f, 20f)] public float lightIntensity = 5f;
        [Range(0.1f, 5f)] public float lightAbsorption = 1f;
        [Range(0f, 0.99f)] public float scatteringAnisotropy = 0.7f;

        [Range(0f, 0.99f)] public float _colorOffset1 = 0.5f;
        [Range(0f, 0.99f)] public float _colorOffset2 = 0.5f;

        [Range(0f, 5f)] public float _shapeTiling;
        [Range(0f, 5f)] public float _detailTiling;

        [Range(0f, 5f)]
        public float _xy_Speed_zw_Warp;


        [Header("Weather Shape")]


        public Texture2D WeatherMap;

        public Texture2D blueNoise;
        public float WeatherScale=0.1f;

        [Range(0f, 0.99f)] public float _heightWeights = 0.5f;
        [Range(0f, 0.99f)] public float edgeWeight = 0.5f;


    }

    public Settings settings = new Settings();

    class VolumeCloudBoxPass : ScriptableRenderPass
    {
        private Material material;
        private Settings settings;

        public VolumeCloudBoxPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            // 创建材质
            Shader shader = Shader.Find("Hidden/VolumeCloudBox");
            if (shader == null) return;
            material = new Material(shader);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("VolumeCloudBox");

            // 获取主光源信息

            // 传递主光源颜色和强度
            cmd.SetGlobalColor("_MainLightColor", renderingData.lightData.visibleLights[0].light.color *
                renderingData.lightData.visibleLights[0].light.intensity);
            // 传递主光源方向
            cmd.SetGlobalVector("_MainLightDir", -renderingData.lightData.visibleLights[0].light.transform.forward);
            

            // 传递其他参数
            cmd.SetGlobalVector("BoundsMin", settings.boundsMin);
            cmd.SetGlobalVector("BoundsMax", settings.boundsMax);
            cmd.SetGlobalFloat("_StepSize", settings.stepSize);
            cmd.SetGlobalInt("_StepCount", settings.stepCount);
            cmd.SetGlobalFloat("_Density", settings.density);

            cmd.SetGlobalTexture("_PerlinTex", settings.perlinNoise);
            cmd.SetGlobalTexture("_WorleyTex", settings.worleyNoise);
            cmd.SetGlobalFloat("_NoiseScale", settings.noiseScale);
            cmd.SetGlobalFloat("_DetailScale", settings.detailScale);
            cmd.SetGlobalFloat("_Threshold", settings.densityThreshold);

            cmd.SetGlobalFloat("_shapeNoiseWeight", settings._shapeNoiseWeight);

            cmd.SetGlobalFloat("_detailNoiseWeights", settings.detailNoiseWeights);

            cmd.SetGlobalColor("_LightColor", settings.lightColor);
            cmd.SetGlobalColor("_colA", settings._colorA);
            cmd.SetGlobalColor("_colB", settings._colorB);

            cmd.SetGlobalFloat("_LightIntensity", settings.lightIntensity);
            cmd.SetGlobalFloat("_LightAbsorption", settings.lightAbsorption);
            cmd.SetGlobalFloat("_ScatteringAnisotropy", settings.scatteringAnisotropy);

            cmd.SetGlobalTexture("_WeatherMap", settings.WeatherMap);
            cmd.SetGlobalTexture("blueNoise", settings.blueNoise);

            cmd.SetGlobalFloat("_WeatherScale", settings.WeatherScale);

            cmd.SetGlobalFloat("_colorOffset1", settings._colorOffset1);
            cmd.SetGlobalFloat("_colorOffset2", settings._colorOffset2);

            cmd.SetGlobalFloat("_heightWeights", settings._heightWeights);
            cmd.SetGlobalFloat("edgeWeight", settings.edgeWeight);

            cmd.SetGlobalFloat("_shapeTiling", settings._shapeTiling);
            cmd.SetGlobalFloat("_detailTiling", settings._detailTiling);

            cmd.SetGlobalFloat("_xy_Speed_zw_Warp", settings._xy_Speed_zw_Warp);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            cmd.GetTemporaryRT(Shader.PropertyToID("_TempRT"), renderingData.cameraData.cameraTargetDescriptor);

            cmd.Blit(source, Shader.PropertyToID("_TempRT"), material);
            cmd.Blit(Shader.PropertyToID("_TempRT"), source);

            cmd.ReleaseTemporaryRT(Shader.PropertyToID("_TempRT"));
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // 辅助方法：获取主光源
      
    }

    VolumeCloudBoxPass pass;

    public override void Create()
    {
        pass = new VolumeCloudBoxPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(pass);
        }
    }
}


