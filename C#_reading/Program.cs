using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AeroGravimetryProcessor
{
    public class NavigationPoint
    {
        public double Time { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Height { get; set; }
        public double Ve { get; set; }
        public double Vn { get; set; }
        public double Vup { get; set; }
        public double Fup { get; set; }
        // ДОБАВЛЕНО: Вертикальное ускорение, измеренное гравиметром
        public double AccelerationGravimeter { get; set; } 
    }

    public class GravimeterData
    {
        public double Fup { get; set; }
        public double AccelerationGravimeter { get; set; }
        public bool HasAccelerationData { get; set; }
    }

    public class CalculationResult
    {
        public double Time { get; set; }
        public double AnomalyFromHeight { get; set; }
        public double AnomalyFromVelocity { get; set; }
        public double AnomalyFromGravimeter { get; set; }
        public double AccelerationHeight { get; set; }
        public double AccelerationVelocity { get; set; }
        public double AccelerationGravimeter { get; set; } // ДОБАВЛЕНО
        public double EtvosCorrection { get; set; }
        public double NormalGravity { get; set; }
    }

    public static class EarthConstants
    {
        public const double A = 6378137.0;
        public const double E2 = 0.00669437999013;
        public const double Omega = 7.292115e-5;
        public const double Ge = 9.7803253359;
        public const double Beta1 = 0.00193185265241;
        public const double FreeAirCorrection = 3.086e-5; // мГал/м (или м/с²/м, проверьте единицы в ваших данных)
    }

    public class DataReader
    {
        public List<NavigationPoint> ReadNavigation(string filePath)
        {
            var points = new List<NavigationPoint>();
            using var reader = new StreamReader(filePath);
            string line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (lineNumber <= 2) continue; // Пропуск заголовка
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8) continue;

                points.Add(new NavigationPoint
                {
                    Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Latitude = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    Longitude = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    Height = double.Parse(parts[3], CultureInfo.InvariantCulture),
                    // parts[4] часто бывает ошибка или пропуск, берем Ve, Vn, Vup по индексам
                    Ve = double.Parse(parts[5], CultureInfo.InvariantCulture),
                    Vn = double.Parse(parts[6], CultureInfo.InvariantCulture),
                    Vup = double.Parse(parts[7], CultureInfo.InvariantCulture)
                });
            }

            Console.WriteLine($"✓ Считано навигационных точек: {points.Count}");
            return points;
        }

        public List<GravimeterData> ReadGravimeter(string filePath)
        {
            var gravDataList = new List<GravimeterData>();
            using var reader = new StreamReader(filePath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.Trim();
                if (!char.IsDigit(trimmed[0]) && trimmed[0] != '-') continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var gData = new GravimeterData();
                    if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double fup))
                    {
                        gData.Fup = fup;
                    }

                    // Пытаемся считать 3-й столбец как ускорение гравиметра
                    if (parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double acc))
                    {
                        gData.AccelerationGravimeter = acc;
                        gData.HasAccelerationData = true;
                    }
                    else
                    {
                        gData.AccelerationGravimeter = 0.0;
                        gData.HasAccelerationData = false;
                    }

                    gravDataList.Add(gData);
                }
            }

            Console.WriteLine($"✓ Считано точек гравиметра: {gravDataList.Count}");
            if (gravDataList.Count > 0 && !gravDataList[0].HasAccelerationData)
            {
                Console.WriteLine("  ⚠ ВНИМАНИЕ: В файле гравиметра не найдены данные об ускорении (3-й столбец).");
                Console.WriteLine("  Для корректного расчета 'Метода 3' необходимо вертикальное ускорение гравиметра.");
            }
            return gravDataList;
        }

        public List<NavigationPoint> MergeData(List<NavigationPoint> navPoints, List<GravimeterData> gravData)
        {
            int count = Math.Min(navPoints.Count, gravData.Count);
            var result = new List<NavigationPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var point = navPoints[i];
                point.Fup = gravData[i].Fup;
                point.AccelerationGravimeter = gravData[i].AccelerationGravimeter;
                result.Add(point);
            }

            Console.WriteLine($"✓ Объединено точек для расчёта: {count}");
            return result;
        }
    }

    public class DataWriter
    {
        public void SaveResults(string filePath, List<CalculationResult> results)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("Time\tanomaly_height\tanomaly_velocity\tanomaly_gravimeter");
            foreach (var r in results)
            {
                writer.WriteLine($"{r.Time.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AnomalyFromHeight.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AnomalyFromVelocity.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AnomalyFromGravimeter.ToString(CultureInfo.InvariantCulture)}");
            }
            Console.WriteLine($"✓ Сохранён файл: {filePath}");
        }

        public void SaveAccelerations(string filePath, List<CalculationResult> results)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("Time\tacc_height\tacc_velocity\tacc_gravimeter");
            foreach (var r in results)
            {
                writer.WriteLine($"{r.Time.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AccelerationHeight.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AccelerationVelocity.ToString(CultureInfo.InvariantCulture)}\t" +
                                 // ИСПРАВЛЕНО: раньше здесь ошибочно писалось r.AnomalyFromGravimeter
                                 $"{r.AccelerationGravimeter.ToString(CultureInfo.InvariantCulture)}"); 
            }
            Console.WriteLine($"✓ Сохранён файл: {filePath}");
        }
    }

    public class AnomalyCalculator
    {
        private void CalculateAccelerations(List<NavigationPoint> points, out double[] accHeight, out double[] accVelocity)
        {
            int n = points.Count;
            accHeight = new double[n];
            accVelocity = new double[n];
            
            for (int i = 1; i < n - 1; i++)
            {
                double dt1 = points[i].Time - points[i - 1].Time;
                double dt2 = points[i + 1].Time - points[i].Time;
                double dtCenter = points[i + 1].Time - points[i - 1].Time;

                double v1 = (points[i].Height - points[i - 1].Height) / dt1;
                double v2 = (points[i + 1].Height - points[i].Height) / dt2;
                accHeight[i] = (v2 - v1) / ((dt1 + dt2) / 2.0);

                accVelocity[i] = (points[i + 1].Vup - points[i - 1].Vup) / dtCenter;
            }

            if (n > 2)
            {
                accHeight[0] = accHeight[1];
                accHeight[n - 1] = accHeight[n - 2];
                accVelocity[0] = accVelocity[1];
                accVelocity[n - 1] = accVelocity[n - 2];
            }
        }

        private double CalculateNormalGravity(double latitudeDeg, double height)
        {
            double phiRad = latitudeDeg * Math.PI / 180.0;
            double sin2 = Math.Sin(phiRad) * Math.Sin(phiRad);
            double gNorm = EarthConstants.Ge * (1 + EarthConstants.Beta1 * sin2) / Math.Sqrt(1 - EarthConstants.E2 * sin2);
            return gNorm - EarthConstants.FreeAirCorrection * height;
        }

        private double CalculateEtvosCorrection(NavigationPoint point)
        {
            double phiRad = point.Latitude * Math.PI / 180.0;
            double sin2 = Math.Sin(phiRad) * Math.Sin(phiRad);
            double cosPhi = Math.Cos(phiRad);

            double rn = EarthConstants.A * (1 - EarthConstants.E2) / Math.Pow(1 - EarthConstants.E2 * sin2, 1.5);
            double re = EarthConstants.A / Math.Sqrt(1 - EarthConstants.E2 * sin2);
            
            return 2 * EarthConstants.Omega * point.Ve * cosPhi + 
                   (point.Ve * point.Ve) / (re + point.Height) + 
                   (point.Vn * point.Vn) / (rn + point.Height);
        }

        public List<CalculationResult> Calculate(List<NavigationPoint> points)
        {
            int n = points.Count;
            CalculateAccelerations(points, out double[] accHeight, out double[] accVelocity);
            var results = new List<CalculationResult>(n);
            
            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                double g0 = CalculateNormalGravity(p.Latitude, p.Height);
                double etvos = CalculateEtvosCorrection(p);

                double anomalyHeight = etvos + p.Fup - g0 - accHeight[i];
                double anomalyVelocity = etvos + p.Fup - g0 - accVelocity[i];
                
                // ИСПРАВЛЕНО: вычитаем ускорение гравиметра, а не p.Fup (что давало etvos - g0)
                double anomalyGravimeter = etvos + p.Fup - g0 - p.AccelerationGravimeter;

                results.Add(new CalculationResult
                {
                    Time = p.Time,
                    AnomalyFromHeight = anomalyHeight,
                    AnomalyFromVelocity = anomalyVelocity,
                    AnomalyFromGravimeter = anomalyGravimeter,
                    AccelerationHeight = accHeight[i],
                    AccelerationVelocity = accVelocity[i],
                    AccelerationGravimeter = p.AccelerationGravimeter,
                    EtvosCorrection = etvos,
                    NormalGravity = g0
                });
            }

            return results;
        }
    }

    public class StatisticsReporter
    {
        private void PrintMethodStats(string methodName, List<double> values)
        {
            double avg = values.Average();
            double min = values.Min();
            double max = values.Max();
            double std = Math.Sqrt(values.Sum(x => Math.Pow(x - avg, 2)) / values.Count);

            Console.WriteLine($"\n--- {methodName} ---");
            Console.WriteLine($"  Среднее = {avg:F4} м/с², СКО = {std:F4} м/с²");
            Console.WriteLine($"  Мин/Макс = {min:F4} / {max:F4} м/с²");
        }

        public void PrintStatistics(List<CalculationResult> results)
        {
            Console.WriteLine("\n========== СТАТИСТИКА АНОМАЛИЙ ==========");
            
            PrintMethodStats("МЕТОД 1 (из высоты)", results.Select(r => r.AnomalyFromHeight).ToList());
            PrintMethodStats("МЕТОД 2 (из скорости)", results.Select(r => r.AnomalyFromVelocity).ToList());
            PrintMethodStats("МЕТОД 3 (из гравиметра)", results.Select(r => r.AnomalyFromGravimeter).ToList());

            Console.WriteLine("\nПервые 5 значений (все три метода):");
            Console.WriteLine($"{"Time",12} {"Δg_height",14} {"Δg_velocity",14} {"Δg_gravimeter",14}");

            for (int i = 0; i < Math.Min(5, results.Count); i++)
            {
                var r = results[i];
                Console.WriteLine($"{r.Time,12:F3} {r.AnomalyFromHeight,14:F6} {r.AnomalyFromVelocity,14:F6} {r.AnomalyFromGravimeter,14:F6}");
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Если аргументы переданы из MATLAB, используем их. Иначе - имена по умолчанию.
            string navFilePath = args.Length > 0 ? args[0] : "Phase_L1.VEL";
            string gravFilePath = args.Length > 1 ? args[1] : "Data_Gravimeter.dat";

            try
            {
                Console.WriteLine("Загрузка данных...");
                Console.WriteLine($" - Навигация: {navFilePath}");
                Console.WriteLine($" - Гравиметр: {gravFilePath}");

                var reader = new DataReader();
                var calculator = new AnomalyCalculator();
                var writer = new DataWriter();
                var reporter = new StatisticsReporter();

                var navPoints = reader.ReadNavigation(navFilePath);
                var gravData = reader.ReadGravimeter(gravFilePath);

                var mergedData = reader.MergeData(navPoints, gravData);

                Console.WriteLine("Выполнение расчётов...");
                var results = calculator.Calculate(mergedData);

                // Результаты сохраняем в текущую рабочую папку (где лежит .exe)
                writer.SaveResults("anomalies.txt", results);
                writer.SaveAccelerations("accelerations.txt", results);

                reporter.PrintStatistics(results);

                Console.WriteLine("\n========== РАСЧЁТ ЗАВЕРШЁН ==========");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"\n❌ Ошибка: файл не найден - {ex.FileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Критическая ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            // Console.ReadKey(); // Закомментировано, чтобы программа не висела при вызове из MATLAB
        }
    }
}