using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

class NavigationData
{
    public double Time;   // время
    public double Lat;    // широта B (градусы)
    public double Lon;    // долгота L (градусы)
    public double Hei;    // высота h (м)
    public double VE;     // восточная скорость V_E (м/с)
    public double VN;     // северная скорость V_N (м/с)
    public double VUp;    // вертикальная скорость V_UP (м/с)
}

class Program
{
    static void Main()
    {
        string phasePath = "Phase_L1.VEL";
        string gravPath = "Data_Gravimeter.dat";

        // ==================== 1. ЧТЕНИЕ НАВИГАЦИОННЫХ ДАННЫХ ====================
        List<NavigationData> navData = new List<NavigationData>();

        using (StreamReader reader = new StreamReader(phasePath))
        {
            string line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (lineNumber <= 2) continue; // пропускаем заголовки

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 9) continue;

                navData.Add(new NavigationData
                {
                    Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                    Lat = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    Lon = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    Hei = double.Parse(parts[3], CultureInfo.InvariantCulture),
                    VE = double.Parse(parts[5], CultureInfo.InvariantCulture),
                    VN = double.Parse(parts[6], CultureInfo.InvariantCulture),
                    VUp = double.Parse(parts[7], CultureInfo.InvariantCulture)
                });
            }
        }
        Console.WriteLine($"Считано навигационных точек: {navData.Count}");

        // ==================== 2. ЧТЕНИЕ ДАННЫХ ГРАВИМЕТРА ====================
        List<double> gravTime = new List<double>();
        List<double> fUp = new List<double>();

        using (StreamReader reader = new StreamReader(gravPath))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string trimmed = line.Trim();
                if (!char.IsDigit(trimmed[0]) && trimmed[0] != '-') continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double timeVal) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double fupVal))
                    {
                        gravTime.Add(timeVal);
                        fUp.Add(fupVal);
                    }
                }
            }
        }
        Console.WriteLine($"Считано точек гравиметра: {fUp.Count}");

        // Синхронизация размеров (берём минимум)
        int N = Math.Min(navData.Count, fUp.Count);
        Console.WriteLine($"Количество точек для расчёта: {N}");

        // Константы эллипсоида Земли (WGS84)
        const double a = 6378137.0;           // большая полуось (м)
        const double e2 = 0.00669437999013;   // квадрат эксцентриситета
        const double omega = 7.292115e-5;     // угловая скорость вращения Земли (рад/с)
        
        // Константы для нормальной силы тяжести
        const double g_e = 9.7803253359;      // нормальная сила тяжести на экваторе (м/с²)
        const double beta1 = 0.00193185265241; // коэффициент
        const double free_air_corr = 3.086e-5; // поправка свободного воздуха (м/с²)/м

        // ==================== 3. РАСЧЁТ УСКОРЕНИЙ ТРЕМЯ СПОСОБАМИ ====================
        double[] acc_height = new double[N];
        double[] acc_velocity = new double[N];
        double[] acc_gravimeter = new double[N];
        double[] timeArr = new double[N];
        double[] latArr = new double[N];
        double[] lonArr = new double[N];
        double[] heiArr = new double[N];
        double[] veArr = new double[N];
        double[] vnArr = new double[N];
        double[] vupArr = new double[N];
        
        // Копируем данные в массивы для удобства
        for (int i = 0; i < N; i++)
        {
            timeArr[i] = navData[i].Time;
            latArr[i] = navData[i].Lat;
            lonArr[i] = navData[i].Lon;
            heiArr[i] = navData[i].Hei;
            veArr[i] = navData[i].VE;
            vnArr[i] = navData[i].VN;
            vupArr[i] = navData[i].VUp;
            acc_gravimeter[i] = fUp[i];
        }
        
        // Расчёт ускорений для внутренних точек (i = 1..N-2)
        for (int i = 1; i < N - 1; i++)
        {
            double dt1 = timeArr[i] - timeArr[i - 1];
            double dt2 = timeArr[i + 1] - timeArr[i];
            double dt_center = timeArr[i + 1] - timeArr[i - 1];
            
            // 3.1 Ускорение из высоты (вторая производная)
            double v1_height = (heiArr[i] - heiArr[i - 1]) / dt1;
            double v2_height = (heiArr[i + 1] - heiArr[i]) / dt2;
            acc_height[i] = (v2_height - v1_height) / ((dt1 + dt2) / 2.0);
            
            // 3.2 Ускорение из вертикальной скорости (первая производная)
            acc_velocity[i] = (vupArr[i + 1] - vupArr[i - 1]) / dt_center;
        }
        
        // Для границ (0 и N-1) экстраполируем значения
        if (N > 2)
        {
            acc_height[0] = acc_height[1];
            acc_height[N - 1] = acc_height[N - 2];
            acc_velocity[0] = acc_velocity[1];
            acc_velocity[N - 1] = acc_velocity[N - 2];
        }
        
        // ==================== 4. РАСЧЁТ АНОМАЛИЙ ====================
        // Формула: δg = g_etv + f_UP - g0 - a
        
        List<double> anomaly_height = new List<double>();
        List<double> anomaly_velocity = new List<double>();
        List<double> anomaly_gravimeter = new List<double>();
        List<double> timeValid = new List<double>();
        
        for (int idx = 0; idx < N; idx++)
        {
            // Переводим широту в радианы
            double B_rad = latArr[idx] * Math.PI / 180.0;
            double sinB = Math.Sin(B_rad);
            double sin2 = sinB * sinB;
            double cosB = Math.Cos(B_rad);
            
            // 4.1 Радиусы кривизны эллипсоида
            double RN = a * (1 - e2) / Math.Pow(1 - e2 * sin2, 1.5);  // меридиональный радиус
            double RE = a / Math.Sqrt(1 - e2 * sin2);                  // радиус в prime vertical
            
            // 4.2 Поправка Этвеша (g_etv)
            double g_etv = 2 * omega * veArr[idx] * cosB 
                         + (veArr[idx] * veArr[idx]) / (RE + heiArr[idx]) 
                         + (vnArr[idx] * vnArr[idx]) / (RN + heiArr[idx]);
            
            // 4.3 Нормальная сила тяжести g0
            double g_norm = g_e * (1 + beta1 * sin2) / Math.Sqrt(1 - e2 * sin2);
            double g0 = g_norm - free_air_corr * heiArr[idx];
            
            // 4.4 Аномалия по формуле: δg = g_etv + f_UP - g0 - a
            double anomaly_h = g_etv + fUp[idx] - g0 - acc_height[idx];
            double anomaly_v = g_etv + fUp[idx] - g0 - acc_velocity[idx];
            double anomaly_g = g_etv + fUp[idx] - g0 - acc_gravimeter[idx];
            
            anomaly_height.Add(anomaly_h);
            anomaly_velocity.Add(anomaly_v);
            anomaly_gravimeter.Add(anomaly_g);
            timeValid.Add(timeArr[idx]);
        }
        
        // ==================== 5. СОХРАНЕНИЕ ПРОМЕЖУТОЧНОГО ФАЙЛА (УСКОРЕНИЯ) ====================
        string accFilePath = "accelerations.txt";
        using (StreamWriter writer = new StreamWriter(accFilePath))
        {
            writer.WriteLine("Time\tacc_height\tacc_velocity\tacc_gravimeter");
            for (int idx = 0; idx < N; idx++)
            {
                writer.WriteLine($"{timeArr[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{acc_height[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{acc_velocity[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{acc_gravimeter[idx].ToString(CultureInfo.InvariantCulture)}");
            }
        }
        Console.WriteLine($"Сохранён промежуточный файл: {accFilePath}");
        
        // ==================== 6. СОХРАНЕНИЕ ИТОГОВОГО ФАЙЛА С АНОМАЛИЯМИ ====================
        string anomalyFilePath = "anomalies.txt";
        using (StreamWriter writer = new StreamWriter(anomalyFilePath))
        {
            writer.WriteLine("Time\tanomaly_height\tanomaly_velocity\tanomaly_gravimeter");
            for (int idx = 0; idx < N; idx++)
            {
                writer.WriteLine($"{timeValid[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{anomaly_height[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{anomaly_velocity[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{anomaly_gravimeter[idx].ToString(CultureInfo.InvariantCulture)}");
            }
        }
        Console.WriteLine($"Сохранён итоговый файл с аномалиями: {anomalyFilePath}");
        
        // ==================== 7. ДОПОЛНИТЕЛЬНО: ПОПРАВКА ЭТВЕША И g0 ====================
        string etvFilePath = "etv_and_g0.txt";
        using (StreamWriter writer = new StreamWriter(etvFilePath))
        {
            writer.WriteLine("Time\tg_etv\tg_norm\tg0");
            for (int idx = 0; idx < N; idx++)
            {
                double B_rad = latArr[idx] * Math.PI / 180.0;
                double sin2 = Math.Pow(Math.Sin(B_rad), 2);
                double cosB = Math.Cos(B_rad);
                
                double g_norm = g_e * (1 + beta1 * sin2) / Math.Sqrt(1 - e2 * sin2);
                double g0_val = g_norm - free_air_corr * heiArr[idx];
                
                double RN = a * (1 - e2) / Math.Pow(1 - e2 * sin2, 1.5);
                double RE = a / Math.Sqrt(1 - e2 * sin2);
                double g_etv_val = 2 * omega * veArr[idx] * cosB 
                                 + (veArr[idx] * veArr[idx]) / (RE + heiArr[idx]) 
                                 + (vnArr[idx] * vnArr[idx]) / (RN + heiArr[idx]);
                
                writer.WriteLine($"{timeArr[idx].ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{g_etv_val.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{g_norm.ToString(CultureInfo.InvariantCulture)}\t" +
                                 $"{g0_val.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        Console.WriteLine($"Сохранён файл с поправкой Этвеша и g0: {etvFilePath}");
        
        
        // ==================== 8. ВЫВОД СТАТИСТИКИ ====================
        Console.WriteLine("\n========== РАСЧЁТ ЗАВЕРШЁН ==========");
        Console.WriteLine($"Всего обработано точек: {N}");
        
        // Статистика по аномалиям (метод из акселерометров)
        double avg_anomaly = anomaly_gravimeter.Average();
        double min_anomaly = anomaly_gravimeter.Min();
        double max_anomaly = anomaly_gravimeter.Max();
        double std_anomaly = Math.Sqrt(anomaly_gravimeter.Select(x => Math.Pow(x - avg_anomaly, 2)).Average());
        
        Console.WriteLine($"\nСтатистика аномалий (метод из акселерометров):");
        Console.WriteLine($"  Среднее: {avg_anomaly:E6} м/с²");
        Console.WriteLine($"  Мин/Макс: {min_anomaly:E6} / {max_anomaly:E6} м/с²");
        Console.WriteLine($"  СКО: {std_anomaly:E6} м/с²");
        
        Console.WriteLine("\nПервые 5 значений аномалий (все три метода):");
        Console.WriteLine($"{"Time",12} {"Δg_height",14} {"Δg_velocity",14} {"Δg_gravimeter",14}");
        for (int idx = 0; idx < Math.Min(5, N); idx++)
        {
            Console.WriteLine($"{timeValid[idx]:F3}   {anomaly_height[idx]:F6}   {anomaly_velocity[idx]:F6}   {anomaly_gravimeter[idx]:F6}");
        }
        
        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }
}
