function MainPipeline()
    clc;
    disp('=========================================================');
    disp('   ЕДИНАЯ СИСТЕМА ОБРАБОТКИ ГРАВИМЕТРИЧЕСКИХ ДАННЫХ');
    disp('=========================================================');

    % 1. Запоминаем папку, где лежат наши .m скрипты и .exe файл
    scriptDir = pwd;

    % ГАРАНТИЯ: Добавляем эту папку в путь MATLAB/Octave.
    % Теперь функции interp_gravity_data и plot_gravity_anomalies будут
    % находиться всегда, даже когда мы делаем cd в папку пользователя.
    if ~ismember(scriptDir, path)
        addpath(scriptDir);
    end

    % Имена файлов по умолчанию для окна выбора
    defaultNav = 'Phase_L1.VEL';
    defaultGrav = 'Data_Gravimeter.dat';

    % =========================================================================
    % ШАГ 1: Выбор входных файлов пользователем
    % =========================================================================
    disp('>>> Шаг 1: Выберите исходные файлы');
    disp('(В полях "Имя файла" уже введены значения по умолчанию)');

    [navFile, navPath] = uigetfile({'*.vel;*.txt;*.dat', 'Файлы навигации (*.vel, *.txt, *.dat)'}, ...
        'Выберите файл навигации', defaultNav);
    if isequal(navFile, 0), disp('Отмена пользователем.'); return; end

    [gravFile, gravPath] = uigetfile({'*.dat;*.txt', 'Файлы гравиметра (*.dat, *.txt)'}, ...
        'Выберите файл гравиметра', defaultGrav);
    if isequal(gravFile, 0), disp('Отмена пользователем.'); return; end

    if ~strcmp(navPath, gravPath)
        errordlg('Для корректной работы оба файла должны находиться в одной папке!', 'Ошибка');
        return;
    end
    workDir = navPath;

    % =========================================================================
    % ШАГ 2: Запуск интерполяции
    % =========================================================================
    disp('>>> Шаг 2: Запуск интерполяции данных...');
    try
        % Переходим в папку с данными, чтобы скрипт создал новые файлы именно там
        cd(workDir);

        % Вызываем функцию интерполяции (теперь она точно найдется благодаря addpath)
        interp_gravity_data();

        % Имена файлов, которые создает ваш скрипт интерполяции
        interpNavFile = 'Phase_L1_VEL_interp.dat';
        interpGravFile = 'Data_Gravimeter_interp.dat';

        fullInterpNav = fullfile(workDir, interpNavFile);
        fullInterpGrav = fullfile(workDir, interpGravFile);

        if ~exist(fullInterpNav, 'file') || ~exist(fullInterpGrav, 'file')
            error('Файлы %s и/или %s не найдены после интерполяции. Проверьте скрипт interp_gravity_data.m', interpNavFile, interpGravFile);
        end
        disp('✓ Интерполяция завершена. Созданы интерполированные файлы.');

    catch ME
        cd(scriptDir); % Возвращаемся в безопасную папку при ошибке
        error('❌ Ошибка при выполнении interp_gravity_data.m: %s', ME.message);
    end

    % =========================================================================
    % ШАГ 3: Запуск C# программы
    % =========================================================================
    disp('>>> Шаг 3: Запуск расчета аномалий (C#)...');

    % Мы уже находимся в workDir, поэтому C# программа сохранит
    % anomalies.txt и accelerations.txt прямо в папку с данными пользователя.

    exePath = fullfile(scriptDir, 'C#_reading.exe');
    if ~exist(exePath, 'file')
        cd(scriptDir);
        error('❌ Файл C#_reading.exe не найден в папке %s. Скомпилируйте Program.cs!', scriptDir);
    end

    % Формируем команду. Передаем полные пути к интерполированным файлам как аргументы.
    % Кавычки вокруг путей обязательны для корректной обработки кириллицы и пробелов в путях!
    command = sprintf('"%s" "%s" "%s"', exePath, fullInterpNav, fullInterpGrav);

    [status, cmdout] = system(command);

    if status ~= 0
        cd(scriptDir);
        error('❌ Ошибка выполнения C# программы:\n%s', cmdout);
    end
    disp('✓ Расчет аномалий завершен. Созданы anomalies.txt и accelerations.txt');

    % =========================================================================
    % ШАГ 4: Запуск построения графиков
    % =========================================================================
    disp('>>> Шаг 4: Построение графиков и сохранение результатов...');
    try
        % Формируем полные абсолютные пути ко всем 4 файлам (они сейчас лежат в workDir)
        finalNav = fullfile(workDir, interpNavFile);
        finalGrav = fullfile(workDir, interpGravFile);
        finalAnom = fullfile(workDir, 'anomalies.txt');
        finalAccel = fullfile(workDir, 'accelerations.txt');

        % Вызываем ваш скрипт построения графиков, передавая ему все 4 файла
        plot_gravity_anomalies(finalNav, finalGrav, finalAnom, finalAccel);

        disp('✓ Графики построены и итоговые файлы сохранены.');
    catch ME
        cd(scriptDir);
        error('❌ Ошибка при выполнении plot_gravity_anomalies.m: %s', ME.message);
    end

    % Возвращаемся в исходную папку со скриптами
    cd(scriptDir);

    disp('=========================================================');
    disp('   ОБРАБОТКА УСПЕШНО ЗАВЕРШЕНА!');
    disp('=========================================================');
end
