using System.Drawing;
using OpenTK.Mathematics;

namespace Mars;

/// <summary>
/// Преобразование широты/долготы в координаты equirectangular-карты Mars
/// в проекции USGS (0° — центр, 180°W слева, 180°E справа, север вверху).
/// </summary>
public static class MarsMapProjection
{
    /// <summary>Широта → Y-пиксель исходного файла (0 = север, height = юг).</summary>
    public static float LatToY(float lat, float imageHeight) =>
        (90f - lat) / 180f * imageHeight;

    /// <summary>Долгота → X-пиксель; 0°E в центре изображения (как USGS topographic map).</summary>
    public static float LonToX(float lon, float imageWidth)
    {
        float x = (lon / 360f - 0.5f) * imageWidth;
        if (x < 0f)
            x += imageWidth;
        return x;
    }

    /// <summary>
    /// Географические границы тайла → экранный прямоугольник на мини-карте.
    /// <paramref name="mirrorVertical"/> должен совпадать с отражением текстуры (vUV.y = 1 - y).
    /// </summary>
    public static RectangleF GeoToScreenRect(
        float latMin,
        float latMax,
        float lonWest,
        float lonEast,
        float imageWidth,
        float imageHeight,
        Vector2 imgPos,
        Vector2 imgSize,
        bool mirrorVertical)
    {
        float pxWest = LonToX(lonWest, imageWidth);
        float pxEast = LonToX(lonEast, imageWidth);
        float yNorth = LatToY(latMax, imageHeight);
        float ySouth = LatToY(latMin, imageHeight);

        float nx;
        float nw;
        if (pxWest < pxEast)
        {
            nx = pxWest / imageWidth;
            nw = (pxEast - pxWest) / imageWidth;
        }
        else
        {
            nx = pxWest / imageWidth;
            nw = (imageWidth - pxWest) / imageWidth;
        }

        float ny;
        float nh;
        if (mirrorVertical)
        {
            ny = 1f - ySouth / imageHeight;
            nh = (ySouth - yNorth) / imageHeight;
        }
        else
        {
            ny = yNorth / imageHeight;
            nh = (ySouth - yNorth) / imageHeight;
        }

        return new RectangleF(
            imgPos.X + nx * imgSize.X,
            imgPos.Y + ny * imgSize.Y,
            nw * imgSize.X,
            nh * imgSize.Y);
    }
}
