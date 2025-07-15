using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class EditorCreateTexture3DTest : Editor
{
    [MenuItem("GameTools/CreateTexture3D")]

    
    
    static void CreateTexture3D()
    {
        int[] perm = new int[256 * 2];

        float scale = 0.06f;
        
        perm = GeneratePermutationTable();
        // Configure the texture
        int size = 64;
        TextureFormat format = TextureFormat.RGBA32;
        TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        // Create the texture and apply the configuration
        Texture3D texture = new Texture3D(size, size, size, format, false);
        texture.wrapMode = wrapMode;

        // Create a 3-dimensional array to store color data
        Color[] colors = new Color[size * size * size];

        // Populate the array so that the x, y, and z values of the texture will map to red, blue, and green colors
        float inverseResolution = 1.0f / (size - 1.0f);
        for (int z = 0; z < size; z++)
        {
            int zOffset = z * size * size;
            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    //float noise = PerlinNoise3D(x, y, z,scale,perm);
                    float noise = WorleyNoise3D.Generate(x, y, z, scale)*0.5f + WorleyNoise3D.Generate(x, y, z, 0.03f)*0.3f+ WorleyNoise3D.Generate(x, y, z, 0.01f)*0.2f;
                    colors[x + yOffset + zOffset] = new Color(noise,
                        noise, noise, 1.0f);
                }
            }
        }

        // Copy the color values to the texture
        texture.SetPixels(colors);

        // Apply the changes to the texture and upload the updated texture to the GPU
        texture.Apply();

        // Save the texture to your Unity Project
        AssetDatabase.CreateAsset(texture, "Assets/Texture3DApply/WorleyNoise3DTexture_Mix.asset");
    }


    public static int[] GeneratePermutationTable(int size = 256)
    {
        int[] perm = new int[size];

        // 初始化顺序序列
        for (int i = 0; i < size; i++)
        {
            perm[i] = i;
        }

        // Fisher-Yates洗牌
        System.Random rng = new System.Random();
        for (int i = size - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            // 交换元素
            (perm[i], perm[j]) = (perm[j], perm[i]);
        }

        // 重复一次以简化边界检查（Perlin标准做法）
        int[] doubledPerm = new int[size * 2];
        for (int i = 0; i < size * 2; i++)
        {
            doubledPerm[i] = perm[i % size];
        }

        return doubledPerm;
    }


    static float PerlinNoise3D(float x, float y, float z, float scale, int[] perm)
    {
        x *= scale; y *= scale; z *= scale;

        int xi = (int)Mathf.Floor(x) & 255;
        int yi = (int)Mathf.Floor(y) & 255;
        int zi = (int)Mathf.Floor(z) & 255;

        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);

        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        // permutation数组实现哈希梯度索引
        int A = perm[xi] + yi, AA = perm[A] + zi, AB = perm[A + 1] + zi;
        int B = perm[xi + 1] + yi, BA = perm[B] + zi, BB = perm[B + 1] + zi;


        // 三线性插值
        return Lerp(
            Lerp(
                Lerp(Grad(perm[AA], x, y, z),
                    Grad(perm[BA], x - 1, y, z), u),
                Lerp(Grad(perm[AB], x, y - 1, z),
                    Grad(perm[BB], x - 1, y - 1, z), u), v),
            Lerp(
                Lerp(Grad(perm[AA + 1], x, y, z - 1),
                    Grad(perm[BA + 1], x - 1, y, z - 1), u),
                Lerp(Grad(perm[AB + 1], x, y - 1, z - 1),
                    Grad(perm[BB + 1], x - 1, y - 1, z - 1), u), v),
            w);
    }

    // 辅助函数
    static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    static float Lerp(float a, float b, float t) => a + t * (b - a);
    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y,
              v = h < 4 ? y : (h == 12 || h == 14) ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    
}


public static class WorleyNoise3D
{
    // 生成3D Worley噪声
    public static float Generate(
        float x, float y, float z,
        float scale = 1.0f,
        int pointCount = 5,
        bool invert = false,
        float[] featureWeights = null // 可选：多层级权重
    )
    {
        x *= scale;
        y *= scale;
        z *= scale;

        // 确定当前单元格
        int cellX = Mathf.FloorToInt(x);
        int cellY = Mathf.FloorToInt(y);
        int cellZ = Mathf.FloorToInt(z);

        float minDist = float.MaxValue;

        // 检查当前及相邻单元格（3x3x3区域）
        for (int offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                {
                    int neighborCellX = cellX + offsetX;
                    int neighborCellY = cellY + offsetY;
                    int neighborCellZ = cellZ + offsetZ;

                    // 为每个单元格生成固定数量的特征点
                    for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                    {
                        // 获取确定性的随机特征点位置
                        Vector3 featurePoint = GetFeaturePoint(
                            neighborCellX, neighborCellY, neighborCellZ, pointIndex
                        );

                        // 计算到特征点的距离
                        float dist = Vector3.Distance(
                            new Vector3(x, y, z),
                            featurePoint
                        );

                        // 保留最小距离（F1 Worley噪声）
                        minDist = Mathf.Min(minDist, dist);
                    }
                }
            }
        }

        // 归一化并可选反转结果
        float noise = Mathf.Clamp01(minDist * 2.0f); // 乘以2使值域更接近[0,1]
        return invert ? 1 - noise : noise;
    }

    // 获取单元格内的确定性随机特征点
    private static Vector3 GetFeaturePoint(int cellX, int cellY, int cellZ, int pointIndex)
    {
        // 使用哈希函数生成固定随机值
        float randX = Hash(cellX, cellY, cellZ, pointIndex * 3);
        float randY = Hash(cellX, cellY, cellZ, pointIndex * 3 + 1);
        float randZ = Hash(cellX, cellY, cellZ, pointIndex * 3 + 2);

        return new Vector3(
            cellX + randX,
            cellY + randY,
            cellZ + randZ
        );
    }

    // 确定性哈希函数（返回[0,1]范围）
    private static float Hash(int x, int y, int z, int seed)
    {
        // 使用大质数混合
        uint hash = (uint)(x * 374761393 + y * 668265263 + z * 2246822519 + seed * 3266489917);
        hash = (hash ^ (hash >> 15)) * 2246822519;
        hash = (hash ^ (hash >> 13)) * 3266489917;
        return (hash & 0xFFFF) / 65535.0f; // 转换为[0,1]
    }

    // 分形Worley噪声（多层级混合）
    public static float Fractal(
        float x, float y, float z,
        float scale = 1.0f,
        int octaves = 3,
        float persistence = 0.5f,
        float lacunarity = 2.0f
    )
    {
        float total = 0;
        float amplitude = 1;
        float frequency = scale;

        for (int i = 0; i < octaves; i++)
        {
            total += Generate(x * frequency, y * frequency, z * frequency, 1.0f) * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Mathf.Clamp01(total);
    }
}