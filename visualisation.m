clear; close all; clc;
fprintf('========================================\n');
fprintf('   АНАЛИЗ АНОМАЛИЙ СИЛЫ ТЯЖЕСТИ\n');
fprintf('========================================\n\n');

fprintf('1. ЗАГРУЗКА ДАННЫХ...\n');

try
    data = dlmread('anomalies.txt', '\t', 1, 0);
    time = data(:, 1);
    anomaly_height = data(:, 2);
    anomaly_velocity = data(:, 3);
    anomaly_gravimeter = data(:, 4);
    fprintf('  ✓ anomalies.txt загружен (%d точек)\n', length(time));
catch ME
    error('   Ошибка: %s\n', ME.message);
end

try
    nav_data = dlmread('Phase_L1.VEL', '', 2, 0);
    TimeGPS = nav_data(:, 1);
    Lat_rad = nav_data(:, 2);
    Lon_rad = nav_data(:, 3);
    Hei_gps = nav_data(:, 4);

    if size(nav_data, 2) >= 8
        V_E = nav_data(:, 5);
        V_N = nav_data(:, 6);
        V_UP = nav_data(:, 7);
        horizontal_speed = sqrt(V_E.^2 + V_N.^2);
    else
        fprintf('  ⚠ Скорости не найдены, определяю движение по изменению координат\n');
        horizontal_speed = [0; sqrt(diff(Lat_rad).^2 + diff(Lon_rad).^2) * 6371000]; % приблизительно
        horizontal_speed(1) = 0;
    end

    Lat_gps = rad2deg(Lat_rad);
    Lon_gps = rad2deg(Lon_rad);

    fprintf('  ✓ Phase_L1.VEL загружен (%d точек)\n', length(Lat_gps));
    fprintf('  Широта: %.4f - %.4f deg\n', min(Lat_gps), max(Lat_gps));
    fprintf('  Долгота: %.4f - %.4f deg\n', min(Lon_gps), max(Lon_gps));

catch ME
    fprintf('  ⚠ Ошибка загрузки Phase_L1.VEL: %s\n', ME.message);
    n = length(time);
    Lat_gps = linspace(69.35, 69.55, n)';
    Lon_gps = linspace(114, 120, n)';
    Hei_gps = 1000 * ones(n, 1);
    horizontal_speed = 50 * ones(n, 1);
end

fprintf('\n2. ОПРЕДЕЛЕНИЕ МОМЕНТОВ ДВИЖЕНИЯ (исключение стоянок)...\n');

speed_threshold = 5.0;

moving_mask = horizontal_speed > speed_threshold;

moving_mask_smoothed = movmean(double(moving_mask), 101) > 0.5;

moving_indices = find(moving_mask_smoothed);
if isempty(moving_indices)
    fprintf('  ⚠ Не удалось определить движение, использую все данные\n');
    start_idx = 1;
    end_idx = length(time);
else
    start_idx = moving_indices(1);
    end_idx = moving_indices(end);
    fprintf('  Начало движения: индекс %d (время %.1f с)\n', start_idx, time(start_idx));
    fprintf('  Конец движения:  индекс %d (время %.1f с)\n', end_idx, time(end_idx));

    start_idx = max(1, start_idx - 100);
    end_idx = min(length(time), end_idx + 100);
    fprintf('  С учётом запаса: %d - %d (%.1f - %.1f с)\n', start_idx, end_idx, time(start_idx), time(end_idx));
end

time = time(start_idx:end_idx);
Lat_gps = Lat_gps(start_idx:end_idx);
Lon_gps = Lon_gps(start_idx:end_idx);
Hei_gps = Hei_gps(start_idx:end_idx);
anomaly_height = anomaly_height(start_idx:end_idx);
anomaly_velocity = anomaly_velocity(start_idx:end_idx);
anomaly_gravimeter = anomaly_gravimeter(start_idx:end_idx);
horizontal_speed = horizontal_speed(start_idx:end_idx);

fprintf('  После исключения стоянок осталось: %d точек\n', length(time));

fprintf('\n3. СГЛАЖИВАНИЕ (скользящее среднее через filtfilt)...\n');

window_size = 100;
if mod(window_size, 2) == 0, window_size = window_size + 1; end

fprintf('  Окно сглаживания: %d точек (~%.1f%% данных)\n', window_size, window_size/length(time)*100);

kernel = ones(window_size, 1) / window_size;

if exist('filtfilt', 'file') == 2
    anomaly_height_smooth = filtfilt(kernel, 1, anomaly_height);
    anomaly_velocity_smooth = filtfilt(kernel, 1, anomaly_velocity);
    anomaly_gravimeter_smooth = filtfilt(kernel, 1, anomaly_gravimeter);
    fprintf('  Используется filtfilt (нулевой фазовый сдвиг)\n');
else
    anomaly_height_smooth = movmean(anomaly_height, window_size);
    anomaly_velocity_smooth = movmean(anomaly_velocity, window_size);
    anomaly_gravimeter_smooth = movmean(anomaly_gravimeter, window_size);
    fprintf('  Используется movmean\n');
end

fprintf('  ✓ Сглаживание выполнено\n\n');

fprintf('4. ОБРЕЗКА КРАЁВ (удаление переходных процессов)...\n');

trim_edges = max(500, 4 * window_size);
if trim_edges * 2 >= length(time)
    trim_edges = floor(length(time) * 0.05);
end

time_plot = time(trim_edges+1:end-trim_edges);
Lat_plot = Lat_gps(trim_edges+1:end-trim_edges);
Lon_plot = Lon_gps(trim_edges+1:end-trim_edges);
Hei_plot = Hei_gps(trim_edges+1:end-trim_edges);
anom_height_plot = anomaly_height_smooth(trim_edges+1:end-trim_edges);
anom_velocity_plot = anomaly_velocity_smooth(trim_edges+1:end-trim_edges);
anom_grav_plot = anomaly_gravimeter_smooth(trim_edges+1:end-trim_edges);

fprintf('  Обрезано по %d точек с каждого края\n', trim_edges);
fprintf('  Осталось точек: %d\n\n', length(time_plot));

%% ==================== СТАТИСТИКА ====================
fprintf('5. СТАТИСТИКА (после обработки)\n');
fprintf('========================================\n');

scale_mgal = 1e5;

fprintf('\n--- МЕТОД 1 (высота) ---\n');
fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
       mean(anom_height_plot)*scale_mgal, std(anom_height_plot)*scale_mgal);
fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
       min(anom_height_plot)*scale_mgal, max(anom_height_plot)*scale_mgal);

fprintf('\n--- МЕТОД 2 (скорость) ---\n');
fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
       mean(anom_velocity_plot)*scale_mgal, std(anom_velocity_plot)*scale_mgal);
fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
       min(anom_velocity_plot)*scale_mgal, max(anom_velocity_plot)*scale_mgal);

fprintf('\n--- МЕТОД 3 (гравиметр) ---\n');
fprintf('  Среднее = %.4f мГал, СКО = %.4f мГал\n', ...
       mean(anom_grav_plot)*scale_mgal, std(anom_grav_plot)*scale_mgal);
fprintf('  Мин/Макс = %.4f / %.4f мГал\n', ...
       min(anom_grav_plot)*scale_mgal, max(anom_grav_plot)*scale_mgal);

diff_12 = anom_height_plot - anom_velocity_plot;
diff_13 = anom_height_plot - anom_grav_plot;
diff_23 = anom_velocity_plot - anom_grav_plot;

fprintf('\n--- РАЗНОСТИ ---\n');
fprintf('  Метод 1-2: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_12)*scale_mgal, std(diff_12)*scale_mgal);
fprintf('  Метод 1-3: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_13)*scale_mgal, std(diff_13)*scale_mgal);
fprintf('  Метод 2-3: Среднее = %.4f, СКО = %.4f мГал\n', mean(diff_23)*scale_mgal, std(diff_23)*scale_mgal);

fprintf('\n6. ПОСТРОЕНИЕ ГРАФИКОВ...\n');

figure(1); clf;
subplot(3, 1, 1);
plot(time_plot, anom_height_plot * scale_mgal, 'r-', 'LineWidth', 1.5);
ylabel('\Delta g, мГал'); title('Метод 1: из высоты');
grid on;

subplot(3, 1, 2);
plot(time_plot, anom_velocity_plot * scale_mgal, 'g-', 'LineWidth', 1.5);
ylabel('\Delta g, мГал'); title('Метод 2: из скорости');
grid on;

subplot(3, 1, 3);
plot(time_plot, anom_grav_plot * scale_mgal, 'b-', 'LineWidth', 1.5);
xlabel('Время, с'); ylabel('\Delta g, мГал'); title('Метод 3: из гравиметра');
grid on;

print('profiles.png', '-dpng', '-r150');
fprintf('  ✓ profiles.png\n');

figure(2); clf;
plot(Lon_plot, Lat_plot, 'k-', 'LineWidth', 1);
title('Траектория полёта');
xlabel('Lon (deg)'); ylabel('Lat (deg)');
grid on; axis equal;
print('trajectory.png', '-dpng', '-r150');
fprintf('  ✓ trajectory.png\n');

fprintf('\n7. 2D КАРТЫ...\n');

figure(3); clf;

subplot(1, 3, 1);
scatter(Lon_plot, Lat_plot, 10, anom_height_plot * scale_mgal, 'filled');
title('Метод 1: из высоты');
xlabel('Longitude (deg)'); ylabel('Latitude (deg)');
colorbar; colormap(jet);
grid on; axis equal;

subplot(1, 3, 2);
scatter(Lon_plot, Lat_plot, 10, anom_velocity_plot * scale_mgal, 'filled');
title('Метод 2: из скорости');
xlabel('Longitude (deg)');
colorbar; colormap(jet);
grid on; axis equal;

subplot(1, 3, 3);
scatter(Lon_plot, Lat_plot, 10, anom_grav_plot * scale_mgal, 'filled');
title('Метод 3: из гравиметра');
xlabel('Longitude (deg)');
colorbar; colormap(jet);
grid on; axis equal;

print('2d_gals.png', '-dpng', '-r200');
fprintf('  ✓ 2d_gals.png\n');

figure(4); clf;
scatter(Lon_plot, Lat_plot, 8, anom_grav_plot * scale_mgal, 'filled');
title('Аномалии силы тяжести (Метод 3)');
xlabel('Longitude (deg)'); ylabel('Latitude (deg)');
colorbar; colormap(jet);
grid on; axis equal;
print('2d_detail.png', '-dpng', '-r200');
fprintf('  ✓ 2d_detail.png\n');

figure(5); clf;
subplot(2, 1, 1);
plot(time, horizontal_speed, 'b-', 'LineWidth', 1);
ylabel('Скорость, м/с');
title('Горизонтальная скорость во времени');
grid on;
line([time(1), time(end)], [speed_threshold, speed_threshold], 'Color', 'r', 'LineStyle', '--', 'DisplayName', 'Порог');
legend('Скорость', 'Порог 5 м/с', 'Location', 'best');

subplot(2, 1, 2);
plot(time, Hei_gps, 'm-', 'LineWidth', 1);
xlabel('Время, с');
ylabel('Высота, м');
title('Высота полёта');
grid on;
print('speed_altitude.png', '-dpng', '-r150');
fprintf('  ✓ speed_altitude.png\n');

fprintf('\n========================================\n');
fprintf('         АНАЛИЗ ЗАВЕРШЁН\n');
fprintf('========================================\n');
fprintf('\nСОЗДАННЫЕ ФАЙЛЫ:\n');
fprintf('  1. profiles.png\n');
fprintf('  2. trajectory.png\n');
fprintf('  3. 2d_gals.png\n');
fprintf('  4. 2d_detail.png\n');
fprintf('  5. speed_altitude.png\n');
fprintf('\n========================================\n');
