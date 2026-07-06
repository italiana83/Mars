using OpenTK.Mathematics;
using System.Globalization;
using System.Diagnostics;

namespace Mars;

/// <summary>
/// Чтение MOLA PDS-файлов: метаданные из .lbl и heightmap из бинарного .img.
/// </summary>
public class MolaDataReader
{
    /// <summary>Парсит PDS label (.lbl): пары ключ=значение в словарь.</summary>
    public Dictionary<string, string> ReadLblFile(string lblFilePath)
    {
        var parameters = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(lblFilePath))
        {
            if (line.Contains('='))
            {
                var parts = line.Split('=');
                parameters[parts[0].Trim()] = parts[1].Trim().Trim('"');
            }
        }
        return parameters;
    }

    /// <summary>Извлекает числовое значение из PDS-поля вида «-44.0 &lt;DEGREE&gt;».</summary>
    public static float ParsePdsDegree(string value)
    {
        int idx = value.IndexOf('<');
        string num = idx >= 0 ? value[..idx] : value;
        return float.Parse(num.Trim(), CultureInfo.InvariantCulture);
    }

    /// <summary>Читает географические границы тайла из IMAGE_MAP_PROJECTION секции .lbl.</summary>
    public Meg128TileBounds ReadTileBounds(string lblFilePath)
    {
        var p = ReadLblFile(lblFilePath);
        return new Meg128TileBounds(
            Path.GetFileNameWithoutExtension(lblFilePath),
            ParsePdsDegree(p["MINIMUM_LATITUDE"]),
            ParsePdsDegree(p["MAXIMUM_LATITUDE"]),
            ParsePdsDegree(p["WESTERNMOST_LONGITUDE"]),
            ParsePdsDegree(p["EASTERNMOST_LONGITUDE"]));
    }

    /// <summary>Сканирует каталог meg128 и возвращает границы топографических тайлов (megt*.lbl).</summary>
    public static IReadOnlyList<Meg128TileBounds> LoadMeg128Catalog(string directory)
    {
        var reader = new MolaDataReader();
        var tiles = new List<Meg128TileBounds>();

        foreach (var lbl in Directory.EnumerateFiles(directory, "megt*.lbl").OrderBy(Path.GetFileName))
        {
            try
            {
                tiles.Add(reader.ReadTileBounds(lbl));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Skip {lbl}: {ex.Message}");
            }
        }

        return tiles;
    }

    /// <summary>
    /// Читает .img с учётом параметров из lbl: размер, endianness, шаг прореживания step.
    /// Заполняет вершины сетки (X,Z — координаты, Y/W — высота в км).
    /// </summary>
    public MapData ReadImgFile(string imgFilePath, Dictionary<string, string> parameters, int step)
    {
        MapData data = new() { Step = step };

        int width = int.Parse(parameters["LINE_SAMPLES"]);
        int height = int.Parse(parameters["LINES"]);
        string sampleType = parameters["SAMPLE_TYPE"];
        int bytesPerSample = int.Parse(parameters["SAMPLE_BITS"]) / 8;

        bool isBigEndian = sampleType == "MSB_INTEGER";

        data.Rows = height / step;
        data.Cols = width / step;

        using var fileStream = new FileStream(imgFilePath, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        for (int y = 0; y < height; y += step)
        {
            for (int x = 0; x < width; x += step)
            {
                long offset = (y * width + x) * bytesPerSample;
                fileStream.Seek(offset, SeekOrigin.Begin);

                byte[] bytes = binaryReader.ReadBytes(bytesPerSample);
                if (isBigEndian)
                    Array.Reverse(bytes);

                short heightValue = BitConverter.ToInt16(bytes, 0);

                data.Vertices.Add(new Vector4(
                    x * 0.1f,
                    heightValue / 1000.0f,
                    y * 0.1f,
                    heightValue / 1000.0f));
            }
        }

        return data;
    }

    /// <summary>Загружает топографический тайл MEG128: .lbl + соответствующий .img.</summary>
    public MapData LoadTopographyTile(string meg128Directory, string tileBaseName, int step)
    {
        var lblPath = Path.Combine(meg128Directory, tileBaseName + ".lbl");
        var imgPath = Path.Combine(meg128Directory, tileBaseName + ".img");

        if (!File.Exists(imgPath))
            throw new FileNotFoundException("Topography IMG not found.", imgPath);

        var parameters = ReadLblFile(lblPath);
        return ReadImgFile(imgPath, parameters, step);
    }
}
