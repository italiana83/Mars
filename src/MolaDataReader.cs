using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    public class MolaDataReader
    {
        public Dictionary<string, string> ReadLblFile(string lblFilePath)
        {
            var parameters = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(lblFilePath))
            {
                if (line.Contains("="))
                {
                    var parts = line.Split('=');
                    parameters[parts[0].Trim()] = parts[1].Trim().Trim('"');
                }
            }
            return parameters;
        }

        //public List<Vector4> ReadImgFile(string imgFilePath, Dictionary<string, string> parameters, int step)
        //{
        //    // Получение параметров из словаря
        //    int width = int.Parse(parameters["LINE_SAMPLES"]);
        //    int height = int.Parse(parameters["LINES"]);
        //    string sampleType = parameters["SAMPLE_TYPE"];
        //    int bytesPerSample = int.Parse(parameters["SAMPLE_BITS"]) / 8;

        //    bool isBigEndian = sampleType == "MSB_INTEGER";

        //    // Список вершин
        //    List<Vector4> vertices = new List<Vector4>();

        //    using (var fileStream = new FileStream(imgFilePath, FileMode.Open, FileAccess.Read))
        //    using (var binaryReader = new BinaryReader(fileStream))
        //    {
        //        for (int y = 0; y < height; y += step)
        //        {
        //            for (int x = 0; x < width; x += step)
        //            {
        //                // Чтение данных о высоте
        //                long offset = (y * width + x) * bytesPerSample;
        //                fileStream.Seek(offset, SeekOrigin.Begin);

        //                byte[] bytes = binaryReader.ReadBytes(bytesPerSample);
        //                if (isBigEndian)
        //                {
        //                    Array.Reverse(bytes);
        //                }
        //                short heightValue = BitConverter.ToInt16(bytes, 0);

        //                // Создание вершины: (X, высота Y, Z, атрибут высоты)
        //                vertices.Add(new Vector4(
        //                    x * 0.1f,           // X координата
        //                    heightValue / 1000.0f, // Y (высота)
        //                    y * 0.1f,           // Z координата
        //                    heightValue / 1000.0f  // Атрибут высоты
        //                ));
        //            }
        //        }
        //    }

        //    return vertices;
        //}

        public MapData ReadImgFile(string imgFilePath, Dictionary<string, string> parameters, int step)
        {
            MapData data = new MapData();
            data.Step = step;

            int width = int.Parse(parameters["LINE_SAMPLES"]);
            int height = int.Parse(parameters["LINES"]);
            string sampleType = parameters["SAMPLE_TYPE"];
            int bytesPerSample = int.Parse(parameters["SAMPLE_BITS"]) / 8;
            //float resolution = float.Parse(parameters["MAP_RESOLUTION"]);

            bool isBigEndian = sampleType == "MSB_INTEGER";

            data.Rows = height / step;
            data.Cols = width / step;

            using (var fileStream = new FileStream(imgFilePath, FileMode.Open, FileAccess.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                for (int y = 0; y < height; y += step)
                {
                    for (int x = 0; x < width; x += step)
                    {
                        // Чтение данных о высоте
                        long offset = (y * width + x) * bytesPerSample;
                        fileStream.Seek(offset, SeekOrigin.Begin);

                        byte[] bytes = binaryReader.ReadBytes(bytesPerSample);
                        if (isBigEndian)
                        {
                            Array.Reverse(bytes);
                        }
                        short heightValue = BitConverter.ToInt16(bytes, 0);

                        // Создание вершины: (X, высота Y, Z, атрибут высоты)
                        data.Vertices.Add(new Vector4(
                            x * 0.1f,           // X координата
                            heightValue / 1000.0f, // Y (высота)
                            y * 0.1f,           // Z координата
                            heightValue / 1000.0f  // Атрибут высоты
                        ));
                    }
                }
            }

            return data;
        }

    }
}
