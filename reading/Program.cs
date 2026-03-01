using System;
using System.Collections.Generic;  //для List<>
using System.Globalization;  //для правильного чтения чисел с точкой
using System.IO;  //для работы с файлами
using System.Linq;

class NavigationData
{
    public double Time;
    public double Lat;
    public double Lon;
    public double Hei;
    public double VUp;
}

class Program
{
    static void Main()
    {
        string phasePath = "Phase_L1.VEL";
        string gravPath = "Data_Gravimeter.dat";

        List<NavigationData> navData = new List<NavigationData>();
        List<double> gravData = new List<double>();

        // 1. ЧТЕНИЕ Phase_L1

        using (StreamReader reader = new StreamReader(phasePath))
        {
            string line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (lineNumber <= 2)
                    continue;

                string[] parts = line.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 8)
                    continue;

                navData.Add(new NavigationData
                {
                    Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Lat = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    Lon = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    Hei = double.Parse(parts[3], CultureInfo.InvariantCulture),
                    VUp = double.Parse(parts[7], CultureInfo.InvariantCulture)
                });
            }
        }


        // 2. ЧТЕНИЕ Data_Gravimeter

        using (StreamReader reader = new StreamReader(gravPath))
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();
                if (!char.IsDigit(trimmed[0]) && trimmed[0] != '-')
                    continue;

                string[] parts = line.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (string part in parts)
                {
                    if (double.TryParse(part,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double value))
                    {
                        gravData.Add(value);
                    }
                }
            }
        }

        NavigationData[] nav = navData.ToArray();
        double[] grav = gravData.ToArray();

        int N = Math.Min(nav.Length, grav.Length);

        double[] anomaly = new double[N];


        // 3. ВЫЧИСЛЕНИЕ УСКОРЕНИЯ (вторая производная высоты)

        double[] acc = new double[N];

        for (int i = 1; i < N - 1; i++)
        {
            double dt1 = nav[i].Time - nav[i - 1].Time;
            double dt2 = nav[i + 1].Time - nav[i].Time;

            double v1 = (nav[i].Hei - nav[i - 1].Hei) / dt1;
            double v2 = (nav[i + 1].Hei - nav[i].Hei) / dt2;

            acc[i] = (v2 - v1) / ((dt1 + dt2) / 2.0);
        }

        // 4. РАСЧЁТ АНОМАЛИИ

        for (int i = 1; i < N - 1; i++)
        {
            double B = nav[i].Lat;
            double h = nav[i].Hei;

            double sinB = Math.Sin(B);
            double sin2 = sinB * sinB;

            double g0 =
                9.7803253359 *
                (1 + 0.00193185265241 * sin2) /
                Math.Sqrt(1 - 0.00669437999013 * sin2)
                - 3.086e-6 * h;

            anomaly[i] = grav[i] - g0 + acc[i];
        }

        Console.WriteLine("Расчёт аномалии завершён.");
        Console.WriteLine($"Количество точек: {N}");

        // пример вывода первых 5 значений
        for (int i = 1; i < 6; i++)
            Console.WriteLine($"Δg[{i}] = {anomaly[i]}");
    }
}






// using System;
// using System.IO;
// using System.Globalization;
// using System.Collections.Generic;

// class Program
// {
//     static void Main()
//     {
//         string phasePath = "Phase_L1.VEL";
//         string gravPath = "Data_Gravimeter.dat";

//         List<double[]> phaseData = new List<double[]>();
//         List<double[]> gravData = new List<double[]>();

//         // ===== ЧТЕНИЕ СПУТНИКОВЫХ ДАННЫХ =====
//         using (StreamReader reader = new StreamReader(phasePath))
//         {
//             string line;

//             while ((line = reader.ReadLine()) != null)
//             {
//                 string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

//                 double[] numbers = new double[parts.Length];

//                 for (int i = 0; i < parts.Length; i++)
//                 {
//                     numbers[i] = double.Parse(parts[i], CultureInfo.InvariantCulture);
//                 }

//                 phaseData.Add(numbers);
//             }
//         }

//         // ===== ЧТЕНИЕ ДАННЫХ ГРАВИМЕТРА =====
//         using (StreamReader reader = new StreamReader(gravPath))
//         {
//             string line;

//             while ((line = reader.ReadLine()) != null)
//             {
//                 string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

//                 double[] numbers = new double[parts.Length];

//                 for (int i = 0; i < parts.Length; i++)
//                 {
//                     numbers[i] = double.Parse(parts[i], CultureInfo.InvariantCulture);
//                 }

//                 gravData.Add(numbers);
//             }
//         }

//         // ===== ПРИМЕР РАСЧЁТА АНОМАЛИИ =====
//         int count = Math.Min(phaseData.Count, gravData.Count);

//         for (int i = 0; i < count; i++)
//         {
//             double satelliteValue = phaseData[i][0];
//             double gravimeterValue = gravData[i][0];

//             double anomaly = gravimeterValue - satelliteValue;

//             Console.WriteLine($"Аномалия в точке {i}: {anomaly}");
//         }
//     }
// }
