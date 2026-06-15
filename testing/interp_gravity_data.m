function interp_gravity_data(input_vel, input_grav, out_vel_interp, out_grav_interp)
    if nargin < 4
        % значения по умолчанию (для совместимости)
        input_vel = 'Phase_L1.VEL';
        input_grav = 'Data_Gravimeter.dat';
        out_vel_interp = 'Phase_L1_VEL_interp.dat';
        out_grav_interp = 'Data_Gravimeter_interp.dat';
    end

    fprintf('Чтение %s ...\n', input_vel);
    fid = fopen(input_vel, 'r');
    if fid == -1, error('Не удалось открыть %s', input_vel); end
    fgetl(fid); fgetl(fid); fgetl(fid);
    vel_data = textscan(fid, '%f %f %f %f %f %f %f %f %f %f %f');
    fclose(fid);
    time_vel = vel_data{1};
    lat = vel_data{2}; lon = vel_data{3}; hei = vel_data{4};
    rmspos = vel_data{5}; v_e = vel_data{6}; v_n = vel_data{7};
    v_up = vel_data{8}; rmsvel = vel_data{9}; svs = vel_data{10};

    fprintf('Чтение %s ...\n', input_grav);
    grav_data = load(input_grav);
    time_grav = grav_data(:,1);
    fx3 = grav_data(:,2);

    % общая сетка
    t_start = max(min(time_vel), min(time_grav));
    t_end   = min(max(time_vel), max(time_grav));
    dt = 0.1;
    time_common = t_start:dt:t_end;

    % интерполяция
    lat_interp = interp1(time_vel, lat, time_common, 'linear');
    lon_interp = interp1(time_vel, lon, time_common, 'linear');
    hei_interp = interp1(time_vel, hei, time_common, 'linear');
    rmspos_interp = interp1(time_vel, rmspos, time_common, 'linear');
    v_e_interp = interp1(time_vel, v_e, time_common, 'linear');
    v_n_interp = interp1(time_vel, v_n, time_common, 'linear');
    v_up_interp = interp1(time_vel, v_up, time_common, 'linear');
    rmsvel_interp = interp1(time_vel, rmsvel, time_common, 'linear');
    svs_interp = interp1(time_vel, svs, time_common, 'linear');
    fx3_interp = interp1(time_grav, fx3, time_common, 'linear');

    % сохранение
    fid = fopen(out_vel_interp, 'w');
    fprintf(fid, 'Time\tLat\tLon\tHei\tRmsPos\tV_E\tV_N\tV_UP\tRmsVel\tSVs\n');
    for i = 1:length(time_common)
        fprintf(fid, '%.3f\t%.8f\t%.8f\t%.3f\t%.3f\t%.4f\t%.4f\t%.4f\t%.4f\t%.0f\n', ...
                time_common(i), lat_interp(i), lon_interp(i), hei_interp(i), ...
                rmspos_interp(i), v_e_interp(i), v_n_interp(i), v_up_interp(i), ...
                rmsvel_interp(i), svs_interp(i));
    end
    fclose(fid);

    fid = fopen(out_grav_interp, 'w');
    fprintf(fid, 'Time[s]\tFx3[m/s2]\n');
    for i = 1:length(time_common)
        fprintf(fid, '%.1f\t%.8f\n', time_common(i), fx3_interp(i));
    end
    fclose(fid);

    fprintf('Готово: %s, %s\n', out_vel_interp, out_grav_interp);
end
