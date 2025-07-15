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

                // ��CGPROGRAM��������
                float _shapeNoiseWeights;
                float _detailNoiseWeights;

                float3 _LightColor;

                float3 _colA;
                float3 _colB;

                float4 _MainLightColor;
                float3 _MainLightDir;

                float _LightIntensity;
                float _LightAbsorption;

                // ��λ����
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

                    // ������ͼ����������ռ䣩
                    float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                    o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                    return o;
                }

                // ��׼�����굽�ƺпռ�
                float3 GetNormalizedPos(float3 worldPos)
                {
                    return (worldPos - BoundsMin) / (BoundsMax - BoundsMin);
                }

                float remap(float original_value, float original_min, float original_max, float new_min, float new_max)
                {
                    return new_min + (((original_value - original_min) / (original_max - original_min)) * (new_max - new_min));
                }

               
                // �Ż���AABB�����ཻ���
                float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir)
                {
                    float3 invRayDir = 1.0 / (rayDir + 1e-6); // ���������

                    //����AABB�����߽���Ӧ��������ཻʱ��t
                    float3 t0 = (boundsMin - rayOrigin) * invRayDir;
                    float3 t1 = (boundsMax - rayOrigin) * invRayDir;
                    float3 tmin = min(t0, t1);
                    float3 tmax = max(t0, t1);

                    //������ȫ���루��������ᣩ���뿪�������뿪�ᣩ��ʱ��
                    float dstA = max(max(tmin.x, tmin.y), tmin.z);
                    float dstB = min(tmax.x, min(tmax.y, tmax.z));

                    //������AABB���ཻ<==>  tenter<texit   &&   texit>0
                    return float2(max(0, dstA), max(0, dstB - dstA));
                }

               

                float GetCloudSampleDensity(float3 rayPos, float3 boundsMin, float3 boundsMax)
                {
                    float speedShape = _Time.y * _xy_Speed_zw_Warp;
                    float speedDetail = _Time.y * _xy_Speed_zw_Warp;


                    float3 uvwShape = rayPos * _shapeTiling + float3(speedShape, speedShape * 0.2, 0)* _shapeTiling;
                    float3 uvWeather = rayPos * _detailTiling + float3(speedDetail, speedDetail * 0.2, 0)* speedDetail;

                    // 1. ��������ͼ��Rͨ�����ƴ�ֱ˥����
                    float2 weatherUV = GetNormalizedPos(uvWeather).xz / _WeatherScale; // ����ϵ�����ݳ�������
                    float4 weatherMap = tex2D(_WeatherMap, weatherUV);


                    float3 uv = GetNormalizedPos(uvwShape);

                    // ��������������Perlin��������״��
                    float perlinNoise = tex3Dlod(_PerlinTex, float4(uv * _NoiseScale, 0)).r;

                    // ����ϸ��������Worley����С�ṹ��
                    float worleyNoise = tex3Dlod(_WorleyTex, float4(uv * _DetailScale, 0)).r;
                    
                    float blueMap = tex2D(blueNoise, weatherUV).r;
                   
                    // �򵥻�ϣ�PerlinΪ���壬Worley��Ϊϸ�����֣�
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
                    //�ƹⷽ����߽���󽻣��������ֲ�����
                    float dstInsideBox = rayBoxDst(BoundsMin, BoundsMax, position, 1 / dirToLight).y;
                    float stepSize = dstInsideBox / 10;
                    float totalDensity = 0;

                    for (int step = 0; step < 8; step++) //�ƹⲽ������
                    {
                        position += dirToLight * stepSize; //��ƹⲽ��
                        //totalDensity += max(0, SampleCloudDensity(position) * stepSize); /
                        totalDensity += max(0, GetCloudSampleDensity(position, BoundsMin, BoundsMax)); // ������ʱ����������ۼ��ܵƹ�Ӱ���ܶ�
                    }
                    float transmittance = exp(-totalDensity * _LightAbsorption);

                    //����������ӳ��Ϊ 3����ɫ ,��->�ƹ���ɫ ��->ColorA ��->ColorB
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
                    // 1. ����˫��ɢ��
                    float forward = HG(cosAngle, 0.7);  // ��ǰ��ɢ��
                    float backward = HG(cosAngle, 0.2); // �κ���ɢ��

                    // 2. �ȱ������
                    float blended = lerp(forward, backward, 0.5);

                    // 3. ��ӻ�������������
                    return blended;
                }

                // ���߲�������
                float4 rayMarchClouds(float3 startPos, float3 direction, float maxDistance)
                {
                    float totalDensity = 1;
                    float3 lightEnergy = 0; // �洢ɢ����ۻ�
                    float3 lightDir = normalize(_MainLightDir); // ƽ�йⷽ��
                    float stepSize = _StepSize;
                    int steps = min(_StepCount, (int)(maxDistance / stepSize));

                    [loop]
                    for (int i = 0; i < steps; i++)
                    {
                        float3 samplePos = startPos + direction * (i * stepSize);
                       

                        if (all(samplePos > BoundsMin) && all(samplePos < BoundsMax))
                        {
                            // 1. ������ǰ���ܶ�
                            //float density = GetCloudSampleDensity(samplePos, BoundsMin, BoundsMax) * _Density;
                            float density = 0;
                            
                            density = GetCloudSampleDensity(samplePos, BoundsMin, BoundsMax);
                            // 2. �������˥������Ӱ��
                           //float shadow = GetLightTransmittance(samplePos, lightDir);

                            float3 shadow = lightmarch(samplePos, stepSize);

                            // 3. Ӧ����λ��������ɢ��
                            float scattering = PhaseFunction(dot(direction, lightDir));

                            // 4. �ۻ�����Ч��
                            lightEnergy += density * shadow * scattering * _LightIntensity;

                            totalDensity *= exp(-density * stepSize);
                            // 5. �ۻ��ܶ�         
                            
                            if (totalDensity < 0.01)
                                break;
                        }
                    }

                    // ���պϳɣ��ܶ� + ����
                    return float4(lightEnergy, saturate(1-totalDensity));
                }

                float4 frag(v2f i) : SV_Target
                {
                    float4 sceneColor = tex2D(_MainTex, i.uv);
                    float3 rayOrigin = _WorldSpaceCameraPos;
                    float3 rayDir = normalize(i.viewVector);

                    // ��ȡ�������
                    float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                    float sceneDist = depth * length(i.viewVector);

                    // �������ƺе��ཻ
                    float2 rayBoxInfo = rayBoxDst(BoundsMin, BoundsMax, rayOrigin, rayDir);
                    float dstToBox = rayBoxInfo.x;
                    float dstInsideBox = rayBoxInfo.y;

                    // ����ʵ�ʲ�������
                    float marchStart = max(0, dstToBox);
                    float marchEnd = min(sceneDist, dstToBox + dstInsideBox);
                    float marchDist = max(0, marchEnd - marchStart);

                    if (marchDist > 0)
                    {
                        float3 entryPoint = rayOrigin + rayDir * marchStart;
                        float4 cloudData = rayMarchClouds(entryPoint, rayDir, marchDist);

                        // ��ȡ����Դ��ɫ��Ӧ��
                        float3 finalColor = _LightColor * _MainLightColor.rgb * _MainLightColor.a;
                        finalColor = lerp(finalColor, _colA, _colorOffset1);
                        finalColor = lerp(finalColor, _colB, _colorOffset2);

                        float3 cloudColor = cloudData.rgb * finalColor;
                        float cloudAlpha = cloudData.a;

                        // �뱳�����
                        float3 asceneColor = tex2D(_MainTex, i.uv).rgb;
                        return float4(asceneColor * (1 - cloudAlpha) + cloudColor, 1);
                    }
                    
                    return sceneColor;
                }
                ENDCG
            }
        }
}