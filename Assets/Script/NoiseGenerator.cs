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
        // ����������
        Texture2D texture = new Texture2D(width, height);
        

        // �����������
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // ���ｫ������������߼�
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

        texture.Apply(); // Ӧ������SetPixel����

        // ��������ΪPNG�ļ�
        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/NoiseTexture.png", bytes);

        Debug.Log("����ͼ�ѱ��浽: " + Application.dataPath + "/WeatherTexturePerlin.png");
    }

    // ����������涨��������������ɺ���...


    //ֵ����
    float GenerateValueNoise(int x, int y, float scale)
    {
        int x0 = Mathf.FloorToInt(x * scale);
        int x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(y * scale);
        int y1 = y0 + 1;

        // ������ϵ����ֵ��ʹ��α�����������һ���ԣ�
        float r00 = Hash(x0, y0);
        float r10 = Hash(x1, y0);
        float r01 = Hash(x0, y1);
        float r11 = Hash(x1, y1);

        // ��ֵ
        float tx = x * scale - x0;
        float ty = y * scale - y0;

        // ˫���Բ�ֵ
        float v0 = Mathf.Lerp(r00, r10, tx);
        float v1 = Mathf.Lerp(r01, r11, tx);
        return Mathf.Lerp(v0, v1, ty);
    }

    // �򵥵�α�����ϣ����
    float Hash(int x, int y)
    {
        return Mathf.Abs((Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1);
    }
    float Hash(float n)
    {
        // ʹ�ô��������л���
        return  Mathf.Abs((Mathf.Sin(n) * 43758.5453123f) % 1);
    }


    //��������
    float GeneratePerlinNoise(float x, float y, float scale)
    {
        // ������ϵ��ݶ�
        int x0 = Mathf.FloorToInt(x * scale);
        int x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(y * scale);
        int y1 = y0 + 1;

        // �������������λ��
        float tx = (float)x * scale - x0;
        float ty = (float)y * scale - y0;

        // ������ϵ��ݶ�����
        Vector2 g00 = GetGradient(x0, y0);
        Vector2 g10 = GetGradient(x1, y0);
        Vector2 g01 = GetGradient(x0, y1);
        Vector2 g11 = GetGradient(x1, y1);

        // ������
        float v00 = Dot(g00, tx, ty);
        float v10 = Dot(g10, tx - 1, ty);
        float v01 = Dot(g01, tx, ty - 1);
        float v11 = Dot(g11, tx - 1, ty - 1);

        // ƽ����ֵ
        float u = Smooth(tx);
        float v = Smooth(ty);

        // ˫���Բ�ֵ
        float a = Mathf.Lerp(v00, v10, u);
        float b = Mathf.Lerp(v01, v11, u);

       

        return Mathf.Lerp(a, b, v) * 0.5f + 0.5f; // ��һ����[0,1]
    }

    Vector2 GetGradient(int x, int y)
    {
        // ʹ�ù�ϣ��������һ�µ�����ݶ�
        float random = Hash(x + y * 57) * Mathf.PI * 2;
        return new Vector2(Mathf.Cos(random), Mathf.Sin(random));
    }

    float Dot(Vector2 grad, float x, float y)
    {
        return grad.x * x + grad.y * y;
    }

    float Smooth(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10); // 5�ζ���ʽƽ��
    }


    //ϸ������
    float GenerateWorleyNoise(float x, float y, float scale, int pointCount = 3)
    {
        // ȷ����ǰ��Ԫ��
        int cellX = Mathf.FloorToInt(x * scale);
        int cellY = Mathf.FloorToInt(y * scale);

        float minDist = float.MaxValue;

        // ��鵱ǰ��Ԫ������ڵ�Ԫ��
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                // ��ȡ��Ԫ���������
                for (int p = 0; p < pointCount; p++)
                {
                    Vector2 point = GetFeaturePoint(cellX + i, cellY + j, p);
                    point.x += (cellX + i);
                    point.y += (cellY + j);

                    // �������
                    float dist = Vector2.Distance(new Vector2(x * scale, y * scale), point);
                    if (dist < minDist) minDist = dist;
                }
            }
        }

        return Mathf.Clamp01(minDist * 0.5f); // ������Χ
    }

    Vector2 GetFeaturePoint(int cellX, int cellY, int index)
    {
        // ʹ�ù�ϣ��������һ�µ�������
        float x = Hash(cellX + cellY * 57 + index * 101);
        float y = Hash(cellX * 101 + cellY * 57 + index * 131);
        return new Vector2(x, y);
    }



}