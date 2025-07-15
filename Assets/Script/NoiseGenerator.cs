using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseGenerator : MonoBehaviour
{
    public enum NoiseType { White, Value, Perlin, Worley }

    public int width = 64;
    public int height = 64;
    public NoiseType noiseType = NoiseType.Worley;
    public float scale = 1.0f;
    public int worleyPointCount = 3;

    void Start()
    {
        // 创建新纹理
        Texture2D texture = new Texture2D(width, height);
        

        // 填充纹理数据
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 这里将添加噪声生成逻辑
                float value=0;
                switch (noiseType)
                {
                    case NoiseType.White:
                        value = Random.value;
                        break;
                    case NoiseType.Value:
                        value = GenerateValueNoise(x, y, scale);
                        break;
                    case NoiseType.Perlin:
                        value = GeneratePerlinNoise(x, y, scale);
                        break;
                    case NoiseType.Worley:
                        value = GenerateWorleyNoise(x, y, scale, worleyPointCount);
                        break;
                }
                
                texture.SetPixel(x, y, new Color(value, value, value));
            }
        }

        texture.Apply(); // 应用所有SetPixel调用

        // 将纹理保存为PNG文件
        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/NoiseTexture.png", bytes);

        Debug.Log("噪声图已保存到: " + Application.dataPath + "/WeatherTexturePerlin.png");
    }

    // 这里插入上面定义的所有噪声生成函数...


    //值噪声
    float GenerateValueNoise(int x, int y, float scale)
    {
        int x0 = Mathf.FloorToInt(x * scale);
        int x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(y * scale);
        int y1 = y0 + 1;

        // 网格点上的随机值（使用伪随机函数保持一致性）
        float r00 = Hash(x0, y0);
        float r10 = Hash(x1, y0);
        float r01 = Hash(x0, y1);
        float r11 = Hash(x1, y1);

        // 插值
        float tx = x * scale - x0;
        float ty = y * scale - y0;

        // 双线性插值
        float v0 = Mathf.Lerp(r00, r10, tx);
        float v1 = Mathf.Lerp(r01, r11, tx);
        return Mathf.Lerp(v0, v1, ty);
    }

    // 简单的伪随机哈希函数
    float Hash(int x, int y)
    {
        return Mathf.Abs((Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1);
    }
    float Hash(float n)
    {
        // 使用大质数进行混淆
        return  Mathf.Abs((Mathf.Sin(n) * 43758.5453123f) % 1);
    }


    //柏林噪声
    float GeneratePerlinNoise(float x, float y, float scale)
    {
        // 网格点上的梯度
        int x0 = Mathf.FloorToInt(x * scale);
        int x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(y * scale);
        int y1 = y0 + 1;

        // 计算相对网格点的位置
        float tx = (float)x * scale - x0;
        float ty = (float)y * scale - y0;

        // 网格点上的梯度向量
        Vector2 g00 = GetGradient(x0, y0);
        Vector2 g10 = GetGradient(x1, y0);
        Vector2 g01 = GetGradient(x0, y1);
        Vector2 g11 = GetGradient(x1, y1);

        // 计算点积
        float v00 = Dot(g00, tx, ty);
        float v10 = Dot(g10, tx - 1, ty);
        float v01 = Dot(g01, tx, ty - 1);
        float v11 = Dot(g11, tx - 1, ty - 1);

        // 平滑插值
        float u = Smooth(tx);
        float v = Smooth(ty);

        // 双线性插值
        float a = Mathf.Lerp(v00, v10, u);
        float b = Mathf.Lerp(v01, v11, u);

       

        return Mathf.Lerp(a, b, v) * 0.5f + 0.5f; // 归一化到[0,1]
    }

    Vector2 GetGradient(int x, int y)
    {
        // 使用哈希函数生成一致的随机梯度
        float random = Hash(x + y * 57) * Mathf.PI * 2;
        return new Vector2(Mathf.Cos(random), Mathf.Sin(random));
    }

    float Dot(Vector2 grad, float x, float y)
    {
        return grad.x * x + grad.y * y;
    }

    float Smooth(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10); // 5次多项式平滑
    }


    //细胞噪声
    float GenerateWorleyNoise(float x, float y, float scale, int pointCount = 3)
    {
        // 确定当前单元格
        int cellX = Mathf.FloorToInt(x * scale);
        int cellY = Mathf.FloorToInt(y * scale);

        float minDist = float.MaxValue;

        // 检查当前单元格和相邻单元格
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                // 获取单元格的特征点
                for (int p = 0; p < pointCount; p++)
                {
                    Vector2 point = GetFeaturePoint(cellX + i, cellY + j, p);
                    point.x += (cellX + i);
                    point.y += (cellY + j);

                    // 计算距离
                    float dist = Vector2.Distance(new Vector2(x * scale, y * scale), point);
                    if (dist < minDist) minDist = dist;
                }
            }
        }

        return Mathf.Clamp01(minDist * 0.5f); // 调整范围
    }

    Vector2 GetFeaturePoint(int cellX, int cellY, int index)
    {
        // 使用哈希函数生成一致的特征点
        float x = Hash(cellX + cellY * 57 + index * 101);
        float y = Hash(cellX * 101 + cellY * 57 + index * 131);
        return new Vector2(x, y);
    }



}