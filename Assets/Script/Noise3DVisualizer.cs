using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noise3DVisualizer : MonoBehaviour
{
    public int size = 32; // 3D纹理的边长
    public float scale = 0.1f;
    private Texture3D noiseTexture;

    int[] perm = new int[256*2];
    void Start()
    {
        noiseTexture = Generate3DNoise();
        VisualizeAsSlices();

       perm=GeneratePermutationTable();
    }

    Texture3D Generate3DNoise()
    {
        Color[] colors = new Color[size * size * size];

        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = PerlinNoise3D(x, y, z, scale);
                    colors[z * size * size + y * size + x] = new Color(noise, noise, noise);
                }
            }
        }

        Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    void VisualizeAsSlices()
    {
        // 创建2D切片纹理
        Texture2D sliceTex = new Texture2D(size, size * size);

        // 将3D纹理按Z轴切片平铺
        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color col = noiseTexture.GetPixel(x, y, z);
                    sliceTex.SetPixel(x, y + z * size, col);
                }
            }
        }

        sliceTex.Apply();
        GetComponent<Renderer>().material.mainTexture = sliceTex;
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


    float PerlinNoise3D(float x, float y, float z, float scale)
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

        // 哈希梯度索引（需提前定义permutation数组）
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
