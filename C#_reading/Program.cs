using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AeroGravimetryProcessor
{
    // ==================== МОДЕЛЬ ДАННЫХ ====================
    
    /// <summary>
    /// Навигационные данные с одной точки измерения
    /// </summary>
    public class NavigationPoint
    {
        public double Time { get; set; }
        public double Latitude { get; set; }   // градусы
        public double Longitude { get; set; }  // градусы
        public double Height { get; set; }     // метры
        public double Ve { get; set; }         // восточная скорость, м/с
        public double Vn { get; set; }         // северная скорость, м/с
        public double Vup { get; set; }        // вертикальная скорость, м/с
        public double Fup { get; set; }        // показания акселерометра, м/с²
    }
    
    /// <summary>
    /// Результат расчёта для одной точки
    /// </summary>
    public class CalculationResult
    {
        public double Time { get; set; }
        public double AnomalyFromHeight { get; set; }
        public double AnomalyFromVelocity { get; set; }
        public double AnomalyFromGravimeter { get; set; }
        public double AccelerationHeight { get; set; }
        public double AccelerationVelocity { get; set; }
        public double EtvosCorrection { get; set; }
        public double NormalGravity { get; set; }
    }
    
    // ==================== КОНСТАНТЫ ЭЛЛИПСОИДА ====================
    
    public static class EarthConstants
    {
        public const double A = 6378137.0;              // большая полуось, м
        public const double E2 = 0.00669437999013;      // квадрат эксцентриситета
        public const double Omega = 7.292115e-5;        // угловая скорость, рад/с
        public const double Ge = 9.7803253359;          // g на экваторе, м/с²
        public const double Beta1 = 0.00193185265241;   // коэффициент
        public const double FreeAirCorrection = 3.086e-5; // поправка, (м/с²)/м
    }
    
    // ==================== ФАЙЛОВЫЙ ВВОД/ВЫВОД ====================
    
    public class DataReader
    {
        /// <summary>
        /// Чтение навигационного файла Phase_L1.VEL
        /// </summary>
        public List<NavigationPoint> ReadNavigation(string filePath)
        {
            var points = new List<NavigationPoint>();
            
            using var reader = new StreamReader(filePath);
            string line;
            int lineNumber = 0;
            
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (lineNumber <= 2) continue; // пропускаем заголовки
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8) continue;
                
                points.Add(new NavigationPoint
                {
                    Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Latitude = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    Longitude = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    Height = double.Parse(parts[3], CultureInfo.InvariantCulture),
                    Ve = double.Parse(parts[5], CultureInfo.InvariantCulture),
                    Vn = double.Parse(parts[6], CultureInfo.InvariantCulture),
                    Vup = double.Parse(parts[7], CultureInfo.InvariantCulture)
                });
            }
            
            Console.WriteLine($"Считано навигационных точек: {points.Count}");
            return points;
        }
        
        /// <summary>
        /// Чтение файла гравиметра Data_Gravimeter.dat
        /// </summary>
        public List<double> ReadGravimeter(string filePath)
        {
            var fupValues = new List<double>();
            
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
                    if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double fup))
                    {
                        fupValues.Add(fup);
                    }
                }
            }
            
            Console.WriteLine($"Считано точек гравиметра: {fupValues.Count}");
            return fupValues;
        }
        
        /// <summary>
        /// Объединение навигационных данных и показаний гравиметра
        /// </summary>
        public List<NavigationPoint> MergeData(List<NavigationPoint> navPoints, List<double> fupValues)
        {
            int count = Math.Min(navPoints.Count, fupValues.Count);
            var result = new List<NavigationPoint>(count);
            
            for (int i = 0; i < count; i++)
            {
                var point = navPoints[i];
                point.Fup = fupValues[i];
                result.Add(point);
            }
            
            Console.WriteLine($"Объединено точек для расчёта: {count}");
            return result;
        }
    }
    
    public class DataWriter
    {
        /// <summary>
        /// Сохранение результатов в файл
        /// </summary>
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
            
            Console.WriteLine($"Сохранён файл: {filePath}");
        }
        
        /// <summary>
        /// Сохранение промежуточных данных (ускорения)
        /// </summary>
        public void SaveAccelerations(string filePath, List<CalculationResult> results)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("Time\tacc_height\tacc_velocity\tacc_gravimeter");
            
            foreach (var r in results)
            {
                writer.WriteLine($"{r.Time.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AccelerationHeight.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AccelerationVelocity.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{r.AnomalyFromGravimeter.ToString(CultureInfo.InvariantCulture)}");
            }
            
            Console.WriteLine($"Сохранён файл: {filePath}");
        }
    }
    
    // ==================== ВЫЧИСЛИТЕЛЬ ====================
    
    public class AnomalyCalculator
    {
        /// <summary>
        /// Расчёт ускорений тремя способами (конечные разности)
        /// </summary>
        private void CalculateAccelerations(List<NavigationPoint> points, 
                                            out double[] accHeight, 
                                            out double[] accVelocity)
        {
            int n = points.Count;
            accHeight = new double[n];
            accVelocity = new double[n];
            
            // Заполняем ускорение из гравиметра (просто копируем)
            // accGravimeter[i] = points[i].Fup - будет использоваться напрямую
            
            for (int i = 1; i < n - 1; i++)
            {
                double dt1 = points[i].Time - points[i - 1].Time;
                double dt2 = points[i + 1].Time - points[i].Time;
                double dtCenter = points[i + 1].Time - points[i - 1].Time;
                
                // Ускорение из высоты (вторая производная)
                double v1 = (points[i].Height - points[i - 1].Height) / dt1;
                double v2 = (points[i + 1].Height - points[i].Height) / dt2;
                accHeight[i] = (v2 - v1) / ((dt1 + dt2) / 2.0);
                
                // Ускорение из вертикальной скорости (первая производная)
                accVelocity[i] = (points[i + 1].Vup - points[i - 1].Vup) / dtCenter;
            }
            
            // Экстраполяция граничных значений
            if (n > 2)
            {
                accHeight[0] = accHeight[1];
                accHeight[n - 1] = accHeight[n - 2];
                accVelocity[0] = accVelocity[1];
                accVelocity[n - 1] = accVelocity[n - 2];
            }
        }
        
        /// <summary>
        /// Расчёт нормальной силы тяжести g0
        /// </summary>
        private double CalculateNormalGravity(double latitudeDeg, double height)
        {
            double phiRad = latitudeDeg * Math.PI / 180.0;
            double sin2 = Math.Sin(phiRad) * Math.Sin(phiRad);
            
            double gNorm = EarthConstants.Ge * (1 + EarthConstants.Beta1 * sin2) 
                         / Math.Sqrt(1 - EarthConstants.E2 * sin2);
            
            return gNorm - EarthConstants.FreeAirCorrection * height;
        }
        
        /// <summary>
        /// Расчёт поправки Этвеша
        /// </summary>
        private double CalculateEtvosCorrection(NavigationPoint point)
        {
            double phiRad = point.Latitude * Math.PI / 180.0;
            double sin2 = Math.Sin(phiRad) * Math.Sin(phiRad);
            double cosPhi = Math.Cos(phiRad);
            
            // Радиусы кривизны
            double rn = EarthConstants.A * (1 - EarthConstants.E2) 
                      / Math.Pow(1 - EarthConstants.E2 * sin2, 1.5);
            double re = EarthConstants.A / Math.Sqrt(1 - EarthConstants.E2 * sin2);
            
            return 2 * EarthConstants.Omega * point.Ve * cosPhi
                   + (point.Ve * point.Ve) / (re + point.Height)
                   + (point.Vn * point.Vn) / (rn + point.Height);
        }
        
        /// <summary>
        /// Расчёт аномалий для всех точек
        /// </summary>
        public List<CalculationResult> Calculate(List<NavigationPoint> points)
        {
            int n = points.Count;
            
            // Расчёт ускорений
            CalculateAccelerations(points, out double[] accHeight, out double[] accVelocity);
            
            var results = new List<CalculationResult>(n);
            
            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                
                double g0 = CalculateNormalGravity(p.Latitude, p.Height);
                double etvos = CalculateEtvosCorrection(p);
                
                // Формула: δg = g_etv + f_UP - g0 - a
                double anomalyHeight = etvos + p.Fup - g0 - accHeight[i];
                double anomalyVelocity = etvos + p.Fup - g0 - accVelocity[i];
                double anomalyGravimeter = etvos + p.Fup - g0 - p.Fup; // a = f_up, поэтому сокращается
                
                results.Add(new CalculationResult
                {
                    Time = p.Time,
                    AnomalyFromHeight = anomalyHeight,
                    AnomalyFromVelocity = anomalyVelocity,
                    AnomalyFromGravimeter = anomalyGravimeter,
                    AccelerationHeight = accHeight[i],
                    AccelerationVelocity = accVelocity[i],
                    EtvosCorrection = etvos,
                    NormalGravity = g0
                });
            }
            
            return results;
        }
    }
    
    // ==================== ОТЧЁТ ====================
    
    public class StatisticsReporter
    {
        public void PrintStatistics(List<CalculationResult> results)
        {
            Console.WriteLine("\n========== СТАТИСТИКА АНОМАЛИЙ ==========");
            
            var anomalies = results.Select(r => r.AnomalyFromGravimeter).ToList();
            double avg = anomalies.Average();
            double min = anomalies.Min();
            double max = anomalies.Max();
            double std = Math.Sqrt(anomalies.Sum(x => Math.Pow(x - avg, 2)) / anomalies.Count);
            
            Console.WriteLine($"Метод из акселерометров:");
            Console.WriteLine($"  Среднее: {avg:E6} м/с²");
            Console.WriteLine($"  Мин/Макс: {min:E6} / {max:E6} м/с²");
            Console.WriteLine($"  СКО: {std:E6} м/с²");
            
            Console.WriteLine("\nПервые 5 значений (все три метода):");
            Console.WriteLine($"{"Time",12} {"Δg_height",14} {"Δg_velocity",14} {"Δg_gravimeter",14}");
            
            for (int i = 0; i < Math.Min(5, results.Count); i++)
            {
                var r = results[i];
                Console.WriteLine($"{r.Time,12:F3} {r.AnomalyFromHeight,14:F6} {r.AnomalyFromVelocity,14:F6} {r.AnomalyFromGravimeter,14:F6}");
            }
        }
    }
    
    // ==================== ГЛАВНАЯ ПРОГРАММА ====================
    
    class Program
    {
        static void Main()
        {
            try
            {
                // Инициализация компонентов
                var reader = new DataReader();
                var calculator = new AnomalyCalculator();
                var writer = new DataWriter();
                var reporter = new StatisticsReporter();
                
                // Чтение данных
                var navPoints = reader.ReadNavigation("Phase_L1.VEL");
                var fupValues = reader.ReadGravimeter("Data_Gravimeter.dat");
                
                // Объединение данных
                var mergedData = reader.MergeData(navPoints, fupValues);
                
                // Расчёт аномалий
                var results = calculator.Calculate(mergedData);
                
                // Сохранение результатов
                writer.SaveResults("anomalies.txt", results);
                writer.SaveAccelerations("accelerations.txt", results);
                
                // Вывод статистики
                reporter.PrintStatistics(results);
                
                Console.WriteLine("\n========== РАСЧЁТ ЗАВЕРШЁН ==========");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Ошибка: файл не найден - {ex.FileName}");
                Console.WriteLine("Убедитесь, что Phase_L1.VEL и Data_Gravimeter.dat находятся в папке с программой");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            
            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}