using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseQuad : MonoBehaviour
{
    public int width = 64;
    public int height = 64;

    public float scale = 5;

    public float offsetX = 100f;
    public float offsetY = 100f;


    private void Start()
    {
        
    }

    private void Update()
    {
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = GenerateTexture();
    }


    private Texture2D GenerateTexture()
    {
        Texture2D texture = new Texture2D(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                //float value = 1-GenerateWorleyNoise(x, y, scale);
                float value = GeneratePerlinNoise(x, y, scale)-0.2f;
                texture.SetPixel(x, y, new Color(value, value, value));
            }
        }
        texture.Apply();

        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/WeatherTexturePerlin.png", bytes);

        Debug.Log("噪声图已保存到: " + Application.dataPath + "/WeatherTexturePerlin.png");

        return texture;
    }

    float GernateValueNoise(float x, float y, float scale)
    {
        //获取噪声图上，该像素所采样的那个点所在位于的1*1的整数坐标正方形的四个顶点
        float x0 = Mathf.Floor((float)x * scale);
        float x1 = x0 + 1;
        float y0 = Mathf.Floor((float)y * scale);
        float y1 = y0 + 1;

        // 对正方形四个顶点计算哈希伪随机值
        float r00 = Hash(x0, y0);
        float r10 = Hash(x1, y0);
        float r01 = Hash(x0, y1);
        float r11 = Hash(x1, y1);

        // 计算该像素在噪声图所采样的那个点，在所在整数坐标四边形内的插值比例
        float tx = (float)x * scale - x0;
        float ty = (float)y * scale - y0;

        // 进行双线性插值
        float v0 = Mathf.Lerp(r00, r10, tx);
        float v1 = Mathf.Lerp(r01, r11, tx);
        return Mathf.Lerp(v0, v1, ty);
    }

    float GeneratePerlinNoise(float x, float y, float scale)
    {
        //计算晶格边界值
        float x0 = Mathf.Floor((float)x * scale);
        float x1 = x0 + 1;
        float y0 = Mathf.Floor((float)y * scale);
        float y1 = y0 + 1;

        //生成晶格每个顶点对应的随机梯度向量
        Vector2 r00 = GetGradient(x0, y0);
        Vector2 r01= GetGradient(x0, y1);
        Vector2 r10= GetGradient(x1, y0);
         Vector2 r11= GetGradient(x1, y1);

        //计算晶格到目标采样点的距离向量
        Vector2 dist00 = new Vector2(x * scale - x0, y * scale - y0);
        Vector2 dist01= new Vector2(x * scale - x0, y * scale - y1);
        Vector2 dist10= new Vector2(x * scale - x1, y * scale - y0);
        Vector2 dist11 = new Vector2(x * scale - x1, y * scale - y1);

        //计算每个顶点的距离向量和梯度向量的点积，作为对最终采样点着色的影响值
        float product00 = Vector2.Dot(dist00, r00);
        float product01 = Vector2.Dot(dist01, r01);
        float product10 = Vector2.Dot(dist10, r10);
        float product11 = Vector2.Dot(dist11, r11);

        float tx = (float)x * scale - x0;
        float ty = (float)y * scale - y0;
        float u = Smooth(tx);
        float v = Smooth(ty);

        //双线性插值
        float lerpx0 = Mathf.Lerp(product00, product10, u);//x=x0方向上的插值
        float lerpx1 = Mathf.Lerp(product01, product11, u );//x=x1方向上的插值

        float lerp=Mathf.Lerp(lerpx0, lerpx1, v);//将上面两个插值结果在y方向上再做一次插值

        return lerp*0.5f+0.5f;



        //float xcoord = (float)x / width * scale +offsetX;
        // float ycoord = (float)y / height * scale + offsetY;

        // return Mathf.PerlinNoise(xcoord, ycoord);
    }


    float Hash(float x, float y)
    {
        return Mathf.Abs((Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1);
    }
    float Hash(float n)
    {
        // 使用大质数进行混淆
        return Mathf.Abs((Mathf.Sin(n) * 43758.5453123f) % 1);
    }

     private static float Fade(float t) 
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static Vector2 Hash22(Vector2 p)
    {
        p = new Vector2(
            p.x * 127.1f + p.y * 311.7f,
            p.x * 269.5f + p.y * 183.3f
        );
        return new Vector2(
            Mathf.Sin(p.x) * 43758.5453f % 1f,
            Mathf.Sin(p.y) * 43758.5453f % 1f
        );
    }

    Vector2 GetGradient(float x, float y)
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

