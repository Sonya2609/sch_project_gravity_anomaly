clc; close all;
Day          = '2024-07-14'; % flight data
D            = importdata('Phase_L1.VEL'); % Time[s] Lat[rad] Lon[rad] Hei[m] RmsPos V_E V_N V_UP RmsVel SVs Type
gnss         = D.data;
clear D

%------------- Data reading & Figures --------------------------------------
i0_gps=1;
in_gps=max(size(gnss));

TimeGPS     = gnss(i0_gps:in_gps,1);
Lat_gps     = rad2deg(gnss(i0_gps:in_gps,2));  % deg
Lon_gps     = rad2deg(gnss(i0_gps:in_gps,3));  % deg
Hei_gps     = gnss(i0_gps:in_gps,4);           % m
Ve_gps      = gnss(i0_gps:in_gps,6);           % m/s
Vn_gps      = gnss(i0_gps:in_gps,7);           % m/s
Vup_gps     = gnss(i0_gps:in_gps,8);           % m/s
RmsPos      = gnss(i0_gps:in_gps,5);           % m
RmsVel      = gnss(i0_gps:in_gps,9);           % m/s
SVs         = gnss(i0_gps:in_gps,10);
Type_sln    = gnss(i0_gps:in_gps,11);          

figure('Name','Sr2Nav - Traj'); clf;
plot(Lon_gps,Lat_gps)
h = title(['Trajectory from GPS. Flight ', num2str(Day)]);
h1 = xlabel('Lon (deg)');
h2 = ylabel('Lat (deg)');
grid on

figure('Name','Sr2Nav - Vel'); clf;
plot(TimeGPS,[Ve_gps,Vn_gps,Vup_gps])
title(['Velocity from GPS. Flight ', num2str(Day)])
ylabel('(m/s)')
xlabel('Time (s)');
legend('V_E','V_N','V_{Up}')
grid on
