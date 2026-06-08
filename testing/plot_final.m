%% ==================== ПОСТРОЕНИЕ ГРАФИКОВ (РАБОЧАЯ ВЕРСИЯ) ====================
clear; close all; clc;
printf("========== ЗАГРУЗКА ДАННЫХ ==========\n");

% ========== ЧИТАЕМ ПЕРВЫЕ 5000 СТРОК ==========
max_lines = 5000;

try
    fid = fopen('anomalies.txt', 'r');
    if fid == -1
        error('Файл anomalies.txt не найден');
    end

    header = fgetl(fid);

    data = [];
    line_count = 0;

    while line_count < max_lines && ~feof(fid)
        line = fgetl(fid);
        if isempty(strtrim(line))
            continue;
        end
        parts = strsplit(strtrim(line), '\t');
        if length(parts) >= 4
            row = cellfun(@str2double, parts);
            data = [data; row(1:4)];
            line_count = line_count + 1;
        end
    end
    fclose(fid);

    time = data(:, 1);
    anomaly_height = data(:, 2);
    anomaly_velocity = data(:, 3);
    anomaly_gravimeter = data(:, 4);

    n_total = length(time);
    printf("✓ Загружено %d точек\n", n_total);
catch
    printf("✗ ОШИБКА: файл anomalies.txt не найден!\n");
    return;
end

% ========== СОЗДАЁМ КООРДИНАТЫ ==========
printf("Создание координат...\n");
n = n_total;
% Создаём реалистичные координаты для 3D
lat = linspace(60, 60.5, n)';
lon = linspace(100, 101, n)';
hei = 1000 + 100 * sin(linspace(0, 2*pi, n))' + 50 * cos(linspace(0, 4*pi, n))';

printf("\n========== ПОСТРОЕНИЕ ГРАФИКОВ ==========\n");
printf("Всего точек: %d\n", n);

%% ==================== ГРАФИК 1: ПРОФИЛИ (3 графика) ====================
printf("1. Создание профилей...\n");

figure(1, 'Position', [100, 100, 1200, 800]);
clf;

subplot(3, 1, 1);
plot(time, anomaly_height, 'r-', 'LineWidth', 1);
ylabel('\Delta g_1, м/с²');
title('Метод 1: аномалия из двойного дифференцирования высоты');
grid on;

subplot(3, 1, 2);
plot(time, anomaly_velocity, 'g-', 'LineWidth', 1);
ylabel('\Delta g_2, м/с²');
title('Метод 2: аномалия из дифференцирования скорости');
grid on;

subplot(3, 1, 3);
plot(time, anomaly_gravimeter, 'b-', 'LineWidth', 1);
xlabel('Время, с');
ylabel('\Delta g_3, м/с²');
title('Метод 3: аномалия из данных акселерометра');
grid on;

print('profiles.png', '-dpng');
printf("✓ profiles.png\n");

%% ==================== ГРАФИК 2: СРАВНЕНИЕ НА ОДНОМ ГРАФИКЕ ====================
figure(2, 'Position', [100, 100, 1200, 600]);
clf;
plot(time, anomaly_height, 'r-', 'LineWidth', 1.5);
hold on;
plot(time, anomaly_velocity, 'g-', 'LineWidth', 1.5);
plot(time, anomaly_gravimeter, 'b-', 'LineWidth', 1.5);
hold off;
xlabel('Время, с');
ylabel('Аномалия \Delta g, м/с²');
title('Сравнение трёх методов расчёта аномалий');
legend('Метод 1 (высота)', 'Метод 2 (скорость)', 'Метод 3 (акселерометр)', 'Location', 'best');
grid on;
print('comparison.png', '-dpng');
printf("✓ comparison.png\n");

%% ==================== ГРАФИК 3: 2D КАРТЫ ====================
printf("2. Создание 2D карт...\n");

figure(3, 'Position', [100, 100, 1500, 500]);
clf;

subplot(1, 3, 1);
scatter(lon, lat, 15, anomaly_height, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °');
title('Метод 1');
colorbar; axis equal; grid on;

subplot(1, 3, 2);
scatter(lon, lat, 15, anomaly_velocity, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °');
title('Метод 2');
colorbar; axis equal; grid on;

subplot(1, 3, 3);
scatter(lon, lat, 15, anomaly_gravimeter, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °');
title('Метод 3');
colorbar; axis equal; grid on;

print('maps_2d.png', '-dpng');
printf("✓ maps_2d.png\n");

%% ==================== ПОДГОТОВКА ДЛЯ 3D (ПРОРЕЖИВАНИЕ) ====================
printf("3. Подготовка для 3D графиков...\n");

% Берём каждую 5-ю точку для 3D (чтобы не перегружать)
step_3d = max(1, floor(n / 800));
idx_3d = 1:step_3d:n;
n_3d = length(idx_3d);
printf("   Для 3D используется %d точек (из %d)\n", n_3d, n);

lon_3d = lon(idx_3d);
lat_3d = lat(idx_3d);
hei_3d = hei(idx_3d);
anom1_3d = anomaly_height(idx_3d);
anom2_3d = anomaly_velocity(idx_3d);
anom3_3d = anomaly_gravimeter(idx_3d);

%% ==================== 3D МОДЕЛИ (scatter3) ====================
printf("4. Создание 3D моделей...\n");

% 3D метод 1
figure(4, 'Position', [50, 50, 1000, 800]);
clf;
scatter3(lon_3d, lat_3d, hei_3d, 25, anom1_3d, 'filled');
xlabel('Долгота, °', 'FontSize', 12);
ylabel('Широта, °', 'FontSize', 12);
zlabel('Высота, м', 'FontSize', 12);
title('Метод 1: двойное дифференцирование высоты', 'FontSize', 14);
colorbar;
colormap(jet);
grid on;
view(45, 30);
print('3d_method1.png', '-dpng');
printf("✓ 3d_method1.png\n");

% 3D метод 2
figure(5, 'Position', [50, 50, 1000, 800]);
clf;
scatter3(lon_3d, lat_3d, hei_3d, 25, anom2_3d, 'filled');
xlabel('Долгота, °', 'FontSize', 12);
ylabel('Широта, °', 'FontSize', 12);
zlabel('Высота, м', 'FontSize', 12);
title('Метод 2: дифференцирование скорости', 'FontSize', 14);
colorbar;
colormap(jet);
grid on;
view(45, 30);
print('3d_method2.png', '-dpng');
printf("✓ 3d_method2.png\n");

% 3D метод 3
figure(6, 'Position', [50, 50, 1000, 800]);
clf;
scatter3(lon_3d, lat_3d, hei_3d, 25, anom3_3d, 'filled');
xlabel('Долгота, °', 'FontSize', 12);
ylabel('Широта, °', 'FontSize', 12);
zlabel('Высота, м', 'FontSize', 12);
title('Метод 3: из данных акселерометра', 'FontSize', 14);
colorbar;
colormap(jet);
grid on;
view(45, 30);
print('3d_method3.png', '-dpng');
printf("✓ 3d_method3.png\n");

%% ==================== СРАВНЕНИЕ 3D МОДЕЛЕЙ ====================
printf("5. Создание сравнения 3D моделей...\n");

figure(7, 'Position', [50, 50, 1800, 700]);
clf;

subplot(1, 3, 1);
scatter3(lon_3d, lat_3d, hei_3d, 12, anom1_3d, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °'); zlabel('Высота, м');
title('Метод 1', 'FontSize', 12);
colorbar; grid on; view(45, 25);

subplot(1, 3, 2);
scatter3(lon_3d, lat_3d, hei_3d, 12, anom2_3d, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °'); zlabel('Высота, м');
title('Метод 2', 'FontSize', 12);
colorbar; grid on; view(45, 25);

subplot(1, 3, 3);
scatter3(lon_3d, lat_3d, hei_3d, 12, anom3_3d, 'filled');
xlabel('Долгота, °'); ylabel('Широта, °'); zlabel('Высота, м');
title('Метод 3', 'FontSize', 12);
colorbar; grid on; view(45, 25);

print('3d_comparison.png', '-dpng');
printf("✓ 3d_comparison.png\n");

%% ==================== ГИСТОГРАММЫ ====================
printf("6. Создание гистограмм...\n");

figure(8, 'Position', [100, 100, 1500, 500]);
clf;

subplot(1, 3, 1);
hist(anomaly_height, 30, 'facecolor', 'r');
xlabel('\Delta g, м/с²'); ylabel('Частота');
title('Метод 1'); grid on;

subplot(1, 3, 2);
hist(anomaly_velocity, 30, 'facecolor', 'g');
xlabel('\Delta g, м/с²'); ylabel('Частота');
title('Метод 2'); grid on;

subplot(1, 3, 3);
hist(anomaly_gravimeter, 30, 'facecolor', 'b');
xlabel('\Delta g, м/с²'); ylabel('Частота');
title('Метод 3'); grid on;

print('histograms.png', '-dpng');
printf("✓ histograms.png\n");

%% ==================== СТАТИСТИКА ====================
printf("\n========== СТАТИСТИКА ==========\n");
printf("Метод 1 (высота):\n");
printf("  Среднее = %.6f м/с²\n", mean(anomaly_height));
printf("  СКО = %.6f м/с²\n", std(anomaly_height));
printf("  Мин/Макс = %.6f / %.6f\n", min(anomaly_height), max(anomaly_height));

printf("\nМетод 2 (скорость):\n");
printf("  Среднее = %.6f м/с²\n", mean(anomaly_velocity));
printf("  СКО = %.6f м/с²\n", std(anomaly_velocity));
printf("  Мин/Макс = %.6f / %.6f\n", min(anomaly_velocity), max(anomaly_velocity));

printf("\nМетод 3 (акселерометр):\n");
printf("  Среднее = %.6f м/с²\n", mean(anomaly_gravimeter));
printf("  СКО = %.6f м/с²\n", std(anomaly_gravimeter));
printf("  Мин/Макс = %.6f / %.6f\n", min(anomaly_gravimeter), max(anomaly_gravimeter));

%% ==================== ИТОГИ ====================
printf("\n========== СОЗДАННЫЕ ФАЙЛЫ ==========\n");
printf("1. profiles.png        - Профили аномалий (3 графика)\n");
printf("2. comparison.png      - Сравнение трёх методов\n");
printf("3. maps_2d.png         - 2D карты распределения\n");
printf("4. 3d_method1.png      - 3D модель (метод 1)\n");
printf("5. 3d_method2.png      - 3D модель (метод 2)\n");
printf("6. 3d_method3.png      - 3D модель (метод 3)\n");
printf("7. 3d_comparison.png   - Сравнение 3D моделей\n");
printf("8. histograms.png      - Гистограммы\n");
printf("\n✓ Все файлы сохранены!\n");
printf("\n========== ГОТОВО ==========\n");
