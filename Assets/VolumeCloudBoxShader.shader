Shader "Hidden/VolumeCloudBox"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
       // _StepSize("Step Size", Range(0.1, 5)) = 0.5
       // _StepCount("Step Count", Int) = 64
       // _Density("Density", Range(0, 1)) = 0.05
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 100

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                    float3 viewVector : TEXCOORD1;
                };

                sampler2D _MainTex;
                sampler2D _CameraDepthTexture;
                float3 BoundsMin;
                float3 BoundsMax;
                float _StepSize;
                int _StepCount;
                float _Density;

                sampler3D _PerlinTex;
                sampler3D _WorleyTex;
                float _NoiseScale;
                float _DetailScale;

                sampler2D blueNoise;
                float _Threshold;

                // 在CGPROGRAM部分声明
                float _shapeNoiseWeights;
                float _detailNoiseWeights;

                float3 _LightColor;

                float3 _colA;
                float3 _colB;

                float4 _MainLightColor;
                float3 _MainLightDir;

                float _LightIntensity;
                float _LightAbsorption;

                // 相位函数
                float _ScatteringAnisotropy;

                sampler2D _WeatherMap;
                float4 _WeatherMap_ST; // Scale/Offset

                float _WeatherScale;

                float _colorOffset1;
                float _colorOffset2;

                
                float  _xy_Speed_zw_Warp;

                float _heightWeights=0.5;

                float edgeWeight = 0.5;

                float _shapeTiling;

                float _detailTiling;

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;

                    // 计算视图向量（世界空间）
                    float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                    o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                    return o;
                }

                // 标准化坐标到云盒空间
                float3 GetNormalizedPos(float3 worldPos)
                {
                    return (worldPos - BoundsMin) / (BoundsMax - BoundsMin);
                }

                float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
                {
                    return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
                }

               
                // 优化的AABB射线相交检测
                float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir)
                {
                    float3 invRayDir = 1.0 / (rayDir + 1e-6); // 避免除以零

                    //计算AABB两个边界点对应三个轴的相交时间t
                    float3 t0 = (boundsMin - rayOrigin) * invRayDir;
                    float3 t1 = (boundsMax - rayOrigin) * invRayDir;
                    float3 tmin = min(t0, t1);
                    float3 tmax = max(t0, t1);

                    //计算完全进入（最晚进入轴）和离开（最早离开轴）的时间
                    float dstA = max(max(tmin.x, tmin.y), tmin.z);
                    float dstB = min(tmax.x, min(tmax.y, tmax.z));

                    //光线与AABB盒相交<==>  tenter<texit   &&   texit>0
                    return float2(max(0, dstA), max(0, dstB - dstA));
                }

               

                float GetCloudSampleDensity(float3 rayPos, float3 boundsMin, float3 boundsMax)
                {
                    float speedShape = _Time.y * _xy_Speed_zw_Warp;
                    float speedDetail = _Time.y * _xy_Speed_zw_Warp;


                    float3 uvwShape = rayPos * _shapeTiling + float3(speedShape, speedShape * 0.2, 0)* _shapeTiling;
                    float3 uvWeather = rayPos * _detailTiling + float3(speedDetail, speedDetail * 0.2, 0)* speedDetail;

                    // 1. 采样天气图（R通道控制垂直衰减）
                    float2 weatherUV = GetNormalizedPos(uvWeather).xz / _WeatherScale; // 缩放系数根据场景调整
                    float4 weatherMap = tex2D(_WeatherMap, weatherUV);


                    float3 uv = GetNormalizedPos(uvwShape);

                    // 采样基础噪声（Perlin决定大形状）
                    float perlinNoise = tex3Dlod(_PerlinTex, float4(uv * _NoiseScale, 0)).r;

                    // 采样细节噪声（Worley决定小结构）
                    float worleyNoise = tex3Dlod(_WorleyTex, float4(uv * _DetailScale, 0)).r;
                    
                    float blueMap = tex2D(blueNoise, weatherUV).r;
                   
                    // 简单混合（Perlin为主体，Worley作为细节遮罩）
                    float finalNoise = max(perlinNoise, worleyNoise * 0.3);

                    if (weatherMap.r > 0.005)
                    {
                        float gMin = remap(weatherMap.x, 0, 1, 0.1, 0.5);
                        float heightPercent = (rayPos.y - BoundsMin.y) / (BoundsMax.y - BoundsMin.y);
                        float heightGradient = saturate(remap(heightPercent, 0.0, weatherMap.r, 1, 0)) * saturate(remap(heightPercent, 0.0, gMin, 0, 1));

                        heightGradient *= edgeWeight;

                        float4 normalizedShapeWeights = _shapeNoiseWeights / dot(_shapeNoiseWeights, 1);
                        float shapeFBM = dot(finalNoise, normalizedShapeWeights) * heightGradient;
                        float baseShapeDensity = shapeFBM+0.01;
                        if (baseShapeDensity > 0)
                        {
                            float detailFBM = pow(worleyNoise, _detailNoiseWeights);
                            float oneMinusShape = 1 - baseShapeDensity;
                            float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
                            float cloudDensity = baseShapeDensity;
                            if (detailFBM > 0.3)
                            {
                                cloudDensity -= detailFBM * _detailNoiseWeights;
                            }

                            
                            return saturate(cloudDensity * _Density);
                        }
                        return 0;
                        

                    }
                    return 0;

                }

                

                float3 lightmarch(float3 position, float dstTravelled)
                {
                    float3 dirToLight = _MainLightDir.xyz;
                    //灯光方向与边界框求交，超出部分不计算
                    float dstInsideBox = rayBoxDst(BoundsMin, BoundsMax, position, 1 / dirToLight).y;
                    float stepSize = dstInsideBox / 10;
                    float totalDensity = 0;

                    for (int step = 0; step < 8; step++) //灯光步进次数
                    {
                        position += dirToLight * stepSize; //向灯光步进
                        //totalDensity += max(0, SampleCloudDensity(position) * stepSize); /
                        totalDensity += max(0, GetCloudSampleDensity(position, BoundsMin, BoundsMax)); // 步进的时候采样噪音累计受灯光影响密度
                    }
                    float transmittance = exp(-totalDensity * _LightAbsorption);

                    //将重亮到暗映射为 3段颜色 ,亮->灯光颜色 中->ColorA 暗->ColorB
                    float3 cloudColor = lerp(_colA, _LightColor, saturate(transmittance * _colorOffset1));
                    cloudColor = lerp(_colB, cloudColor, saturate(pow(transmittance * _colorOffset2, 3)));
                    return _Threshold + transmittance * (1 - _Threshold) * cloudColor;
                }

                



                float HG(float a, float g) {
                    float g2 = g * g;
                    return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
                }


                float PhaseFunction(float cosAngle)
                {
                    // 1. 计算双向散射
                    float forward = HG(cosAngle, 0.7);  // 主前向散射
                    float backward = HG(cosAngle, 0.2); // 次后向散射

                    // 2. 等比例混合
                    float blended = lerp(forward, backward, 0.5);

                    // 3. 添加基础照明并缩放
                    return blended;
                }

                // 光线步进函数
                float4 rayMarchClouds(float3 startPos, float3 direction, float maxDistance)
                {
                    float totalDensity = 1;
                    float3 lightEnergy = 0; // 存储散射光累积
                    float3 lightDir = normalize(_MainLightDir); // 平行光方向
                    float stepSize = _StepSize;
                    int steps = min(_StepCount, (int)(maxDistance / stepSize));

                    [loop]
                    for (int i = 0; i < steps; i++)
                    {
                        float3 samplePos = startPos + direction * (i * stepSize);
                       

                        if (all(samplePos > BoundsMin) && all(samplePos < BoundsMax))
                        {
                            // 1. 采样当前点密度
                            //float density = GetCloudSampleDensity(samplePos, BoundsMin, BoundsMax) * _Density;
                            float density = 0;
                            
                            density = GetCloudSampleDensity(samplePos, BoundsMin, BoundsMax);
                            // 2. 计算光照衰减（阴影）
                           //float shadow = GetLightTransmittance(samplePos, lightDir);

                            float3 shadow = lightmarch(samplePos, stepSize);

                            // 3. 应用相位函数计算散射
                            float scattering = PhaseFunction(dot(direction, lightDir));

                            // 4. 累积光照效果
                            lightEnergy += density * shadow * scattering * _LightIntensity;

                            totalDensity *= exp(-density * stepSize);
                            // 5. 累积密度         
                            
                            if (totalDensity < 0.01)
                                break;
                        }
                    }

                    // 最终合成：密度 + 光照
                    return float4(lightEnergy, saturate(1-totalDensity));
                }

                float4 frag(v2f i) : SV_Target
                {
                    float4 sceneColor = tex2D(_MainTex, i.uv);
                    float3 rayOrigin = _WorldSpaceCameraPos;
                    float3 rayDir = normalize(i.viewVector);

                    // 获取场景深度
                    float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                    float sceneDist = depth * length(i.viewVector);

                    // 计算与云盒的相交
                    float2 rayBoxInfo = rayBoxDst(BoundsMin, BoundsMax, rayOrigin, rayDir);
                    float dstToBox = rayBoxInfo.x;
                    float dstInsideBox = rayBoxInfo.y;

                    // 计算实际步进距离
                    float marchStart = max(0, dstToBox);
                    float marchEnd = min(sceneDist, dstToBox + dstInsideBox);
                    float marchDist = max(0, marchEnd - marchStart);

                    if (marchDist > 0)
                    {
                        float3 entryPoint = rayOrigin + rayDir * marchStart;
                        float4 cloudData = rayMarchClouds(entryPoint, rayDir, marchDist);

                        // 获取主光源颜色并应用
                        float3 finalColor = _LightColor * _MainLightColor.rgb * _MainLightColor.a;
                        finalColor = lerp(finalColor, _colA, _colorOffset1);
                        finalColor = lerp(finalColor, _colB, _colorOffset2);

                        float3 cloudColor = cloudData.rgb * finalColor;
                        float cloudAlpha = cloudData.a;

                        // 与背景混合
                        float3 asceneColor = tex2D(_MainTex, i.uv).rgb;
                        return float4(asceneColor * (1 - cloudAlpha) + cloudColor, 1);
                    }
                    
                    return sceneColor;
                }
                ENDCG
            }
        }
}