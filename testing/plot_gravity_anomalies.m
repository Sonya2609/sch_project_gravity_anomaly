function plot_gravity_anomalies(navFile, gravFile, anomFile, accelFile)
% Построение графиков и анализ аномалий силы тяжести
% navFile   : путь к файлу навигации (например, Phase_L1_VEL_interp.dat)
% gravFile  : путь к файлу гравиметра (например, Data_Gravimeter_interp.dat)
% anomFile  : путь к файлу аномалий (anomalies.txt)
% accelFile : путь к файлу ускорений (accelerations.txt)

    close all; clc; % Команда 'clear' удалена, так как это функция

    fprintf('========================================\n');
    fprintf('   АНАЛИЗ АНОМАЛИЙ СИЛЫ ТЯЖЕСТИ\n');
    fprintf('========================================\n\n');

    fprintf('1. ЗАГРУЗКА ДАННЫХ...\n');

    % --- Загрузка аномалий ---
    fprintf('  Загрузка аномалий из: %s\n', anomFile);
    try
        data = dlmread(anomFile, '\t', 1, 0);
        time = data(:, 1);
        anomaly_height = data(:, 2);
        anomaly_velocity = data(:, 3);
        anomaly_gravimeter = data(:, 4);
        fprintf('  ✓ Файл аномалий загружен (%d точек)\n', length(time));
    catch ME
        error('   Ошибка загрузки файла аномалий: %s\n', ME.message);
    end

    % --- Загрузка навигации ---
    fprintf('  Загрузка навигации из: %s\n', navFile);
    try
        nav_data = dlmread(navFile, '', 2, 0);
        TimeGPS = nav_data(:, 1);
        Lat_rad = nav_data(:, 2);
        Lon_rad = nav_data(:, 3);
        Hei_gps = nav_data(:, 4);
        Lat_gps = rad2deg(Lat_rad);
        Lon_gps = rad2deg(Lon_rad);
        fprintf('  ✓ Файл навигации загружен (%d точек)\n', length(Lat_gps));
        fprintf('  Широта: %.4f - %.4f deg\n', min(Lat_gps), max(Lat_gps));
        fprintf('  Долгота: %.4f - %.4f deg\n', min(Lon_gps), max(Lon_gps));
    catch ME
        fprintf('  ⚠ Ошибка загрузки навигации: %s\n', ME.message);
        fprintf('  Используются фиктивные данные для демонстрации графиков.\n');
        n = length(time);
        Lat_gps = linspace(69.35, 69.55, n)';
        Lon_gps = linspace(114, 120, n)';
        Hei_gps = 1000 * ones(n, 1);
    end

    % --- Загрузка ускорений (для полноты картины, хотя графики строятся по аномалиям) ---
    fprintf('  Загрузка ускорений из: %s\n', accelFile);
    try
        accel_data = dlmread(accelFile, '\t', 1, 0);
        fprintf('  ✓ Файл ускорений загружен (%d точек)\n', length(accel_data(:,1)));
    catch ME
        fprintf('  ⚠ Ошибка загрузки файла ускорений: %s\n', ME.message);
    end

    % Приведение массивов к одинаковой длине
    n = min([length(time), length(anomaly_height), length(Lat_gps)]);
    time = time(1:n);
    Lat_gps = Lat_gps(1:n);
    Lon_gps = Lon_gps(1:n);
    Hei_gps = Hei_gps(1:n);
    anomaly_height = anomaly_height(1:n);
    anomaly_velocity = anomaly_velocity(1:n);
    anomaly_gravimeter = anomaly_gravimeter(1:n);

    fprintf('  Точек для анализа: %d\n\n', n);

    fprintf('2. СТАТИСТИКА\n');
    fprintf('========================================\n');

    scale_mgal = 1e5;

    fprintf('\n--- МЕТОД 1 (высота) ---\n');
    fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
           mean(anomaly_height)*scale_mgal, std(anomaly_height)*scale_mgal);
    fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
           min(anomaly_height)*scale_mgal, max(anomaly_height)*scale_mgal);

    fprintf('\n--- МЕТОД 2 (скорость) ---\n');
    fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
           mean(anomaly_velocity)*scale_mgal, std(anomaly_velocity)*scale_mgal);
    fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
           min(anomaly_velocity)*scale_mgal, max(anomaly_velocity)*scale_mgal);

    fprintf('\n--- МЕТОД 3 (гравиметр) ---\n');
    fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
           mean(anomaly_gravimeter)*scale_mgal, std(anomaly_gravimeter)*scale_mgal);
    fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
           min(anomaly_gravimeter)*scale_mgal, max(anomaly_gravimeter)*scale_mgal);

    diff_12 = anomaly_height - anomaly_velocity;
    diff_13 = anomaly_height - anomaly_gravimeter;
    diff_23 = anomaly_velocity - anomaly_gravimeter;

    fprintf('\n--- РАЗНОСТИ ---\n');
    fprintf('  Метод 1-2: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_12)*scale_mgal, std(diff_12)*scale_mgal);
    fprintf('  Метод 1-3: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_13)*scale_mgal, std(diff_13)*scale_mgal);
    fprintf('  Метод 2-3: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_23)*scale_mgal, std(diff_23)*scale_mgal);

    fprintf('\n3. ПОСТРОЕНИЕ ГРАФИКОВ...\n');

    figure(1); clf;
    subplot(3, 1, 1);
    plot(time, anomaly_height * scale_mgal, 'r-', 'LineWidth', 1.5);
    ylabel('\Delta g, мГал'); title('Метод 1: из высоты');
    grid on;

    subplot(3, 1, 2);
    plot(time, anomaly_velocity * scale_mgal, 'g-', 'LineWidth', 1.5);
    ylabel('\Delta g, мГал'); title('Метод 2: из скорости');
    grid on;

    subplot(3, 1, 3);
    plot(time, anomaly_gravimeter * scale_mgal, 'b-', 'LineWidth', 1.5);
    xlabel('Время, с'); ylabel('\Delta g, мГал'); title('Метод 3: из гравиметра');
    grid on;
    print('profiles.png', '-dpng', '-r150');
    fprintf('  ✓ Сохранено: profiles.png\n');

    figure(2); clf;
    plot(Lon_gps, Lat_gps, 'k-', 'LineWidth', 1);
    title('Траектория полёта');
    xlabel('Lon (deg)'); ylabel('Lat (deg)');
    grid on; axis equal;
    print('trajectory.png', '-dpng', '-r150');
    fprintf('  ✓ Сохранено: trajectory.png\n');

    fprintf('\n4. 2D КАРТЫ (аномалии)...\n');

    figure(3); clf;
    subplot(1, 3, 1);
    scatter(Lon_gps, Lat_gps, 10, anomaly_height * scale_mgal, 'filled');
    title('Метод 1: из высоты');
    xlabel('Longitude (deg)'); ylabel('Latitude (deg)');
    colorbar; colormap(jet);
    grid on; axis equal;

    subplot(1, 3, 2);
    scatter(Lon_gps, Lat_gps, 10, anomaly_velocity * scale_mgal, 'filled');
    title('Метод 2: из скорости');
    xlabel('Longitude (deg)');
    colorbar; colormap(jet);
    grid on; axis equal;

    subplot(1, 3, 3);
    scatter(Lon_gps, Lat_gps, 10, anomaly_gravimeter * scale_mgal, 'filled');
    title('Метод 3: из гравиметра');
    xlabel('Longitude (deg)');
    colorbar; colormap(jet);
    grid on; axis equal;
    print('2d_gals.png', '-dpng', '-r200');
    fprintf('  ✓ Сохранено: 2d_gals.png\n');

    figure(4); clf;
    scatter(Lon_gps, Lat_gps, 8, anomaly_gravimeter * scale_mgal, 'filled');
    title('Аномалии силы тяжести (Метод 3)');
    xlabel('Longitude (deg)'); ylabel('Latitude (deg)');
    colorbar; colormap(jet);
    grid on; axis equal;
    print('2d_detail.png', '-dpng', '-r200');
    fprintf('  ✓ Сохранено: 2d_detail.png\n');

    fprintf('\n========================================\n');
    fprintf('         АНАЛИЗ ЗАВЕРШЁН\n');
    fprintf('========================================\n');

end % Конец функции
