using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NetWatch.Models;

public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3, Critical = 4 }

public record LogEntry(int LineNum, string Raw, string Trimmed, LogLevel? Level, bool IsStack, string? Explanation, string[]? Fixes);

public record Issue(LogLevel Level, List<int> Lines, string Message, string? Explanation, string[]? Fixes);

public record AnalysisResult(List<LogEntry> Parsed, List<Issue> Issues, LogStats Stats);

public record LogStats(int Critical, int Error, int Warning, int Info, int Total);

public static class LogParser
{
    private static readonly Dictionary<LogLevel, int> LevelOrder = new()
    {
        [LogLevel.Debug] = 0, [LogLevel.Info] = 1, [LogLevel.Warning] = 2,
        [LogLevel.Error] = 3, [LogLevel.Critical] = 4
    };

    private static readonly Dictionary<string, Dictionary<LogLevel, string>> LevelNames = new()
    {
        ["ru"] = new()
        {
            [LogLevel.Critical] = "Критический", [LogLevel.Error] = "Ошибка",
            [LogLevel.Warning] = "Предупреждение", [LogLevel.Info] = "Информация",
            [LogLevel.Debug] = "Отладка"
        },
        ["en"] = new()
        {
            [LogLevel.Critical] = "Critical", [LogLevel.Error] = "Error",
            [LogLevel.Warning] = "Warning", [LogLevel.Info] = "Info",
            [LogLevel.Debug] = "Debug"
        }
    };

    public static string GetLevelName(LogLevel level, string lang = "en")
    {
        var dict = LevelNames.GetValueOrDefault(lang, LevelNames["en"]);
        return dict.GetValueOrDefault(level, level.ToString());
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Explanations = new()
    {
        ["ru"] = new()
        {
            ["CRITICAL"] = "Системная критическая ошибка — требуется немедленное вмешательство",
            ["FATAL"] = "Фатальная ошибка — приложение не может продолжить работу",
            ["PANIC"] = "Паника ядра — система в неработоспособном состоянии",
            ["EMERGENCY"] = "Аварийная ситуация — система полностью неработоспособна",
            ["OOM"] = "Нехватка оперативной памяти — процесс убит ядром",
            ["OUT OF MEMORY"] = "Нехватка оперативной памяти — невозможно выделить память",
            ["SEGFAULT"] = "Ошибка сегментации — обращение к недопустимой области памяти",
            ["SEGMENTATION FAULT"] = "Ошибка сегментации — обращение к недопустимой области памяти",
            ["KERNEL PANIC"] = "Паника ядра ОС — система остановлена",
            ["CORE DUMP"] = "Аварийный дамп памяти — приложение завершилось аварийно",
            ["BLUE SCREEN"] = "Синий экран смерти (BSOD) — критический сбой Windows",
            ["ERROR"] = "Ошибка в работе компонента",
            ["EXCEPTION"] = "Исключение — непредвиденное поведение программы",
            ["FAILED"] = "Операция завершилась неудачей",
            ["FAILURE"] = "Сбой — операция не выполнена",
            ["TIMEOUT"] = "Превышено время ожидания — сервер не ответил вовремя",
            ["REFUSED"] = "В соединении отказано — целевой сервер отклонил запрос",
            ["DENIED"] = "Доступ запрещён — недостаточно прав",
            ["CONNECTION RESET"] = "Соединение сброшено удалённой стороной",
            ["CONNECTION REFUSED"] = "Отказ в соединении — целевой сервер недоступен",
            ["CONNECTION TIMEOUT"] = "Таймаут соединения — сервер не отвечает",
            ["ECONNREFUSED"] = "Сетевая ошибка: удалённый хост отклонил соединение",
            ["ECONNRESET"] = "Сетевая ошибка: соединение сброшено удалённой стороной",
            ["ETIMEDOUT"] = "Сетевая ошибка: превышено время ожидания",
            ["ENOTFOUND"] = "Сетевая ошибка: DNS-имя не найдено",
            ["STACK TRACE"] = "Трассировка стека — цепочка вызовов, приведшая к ошибке",
            ["TRACEBACK"] = "Трассировка стека Python — цепочка вызовов",
            ["NULL POINTER"] = "Обращение по нулевому указателю — объект не инициализирован",
            ["500"] = "HTTP 500 — внутренняя ошибка сервера",
            ["502"] = "HTTP 502 — шлюз получил неверный ответ от вышестоящего сервера",
            ["503"] = "HTTP 503 — сервис временно недоступен",
            ["401"] = "HTTP 401 — требуется авторизация",
            ["403"] = "HTTP 403 — доступ запрещён",
            ["WARNING"] = "Предупреждение — потенциальная проблема",
            ["DEPRECATED"] = "Устаревший API — следует обновить код",
            ["SLOW QUERY"] = "Медленный запрос к базе данных — возможна оптимизация",
            ["RETRY"] = "Повторная попытка — предыдущая операция не удалась",
            ["FALLBACK"] = "Резервный режим — основной метод недоступен",
            ["429"] = "HTTP 429 — слишком много запросов (rate limit)",
            ["DISK SPACE LOW"] = "Мало места на диске — возможно переполнение",
            ["HIGH CPU"] = "Высокая загрузка процессора",
            ["HIGH MEMORY"] = "Высокое потребление памяти",
            ["HIGH LOAD"] = "Высокая нагрузка на систему",
            ["INFO"] = "Информационное сообщение — система работает в штатном режиме",
            ["INFORMATION"] = "Информационное сообщение — система работает в штатном режиме",
            ["NOTICE"] = "Уведомление — событие заслуживает внимания, но не является проблемой",
            ["STARTED"] = "Сервис запущен — компонент начал работу",
            ["STARTING"] = "Сервис запускается — идёт инициализация компонента",
            ["STOPPED"] = "Сервис остановлен — компонент завершил работу",
            ["STOPPING"] = "Сервис останавливается — компонент завершает работу",
            ["CONNECTED"] = "Соединение установлено — подключение к серверу успешно",
            ["CONNECTING"] = "Установка соединения — идёт подключение к серверу",
            ["LISTENING"] = "Сервис слушает порт — ожидает входящих подключений",
            ["REQUEST"] = "Входящий запрос — сервер получил обращение от клиента",
            ["RESPONSE"] = "Ответ отправлен — сервер обработал запрос клиента",
            ["200"] = "HTTP 200 OK — запрос выполнен успешно",
            ["201"] = "HTTP 201 Created — ресурс успешно создан",
        },
        ["en"] = new()
        {
            ["CRITICAL"] = "System critical error — immediate intervention required",
            ["FATAL"] = "Fatal error — application cannot continue",
            ["PANIC"] = "Kernel panic — system is in an unusable state",
            ["EMERGENCY"] = "Emergency — system is completely unavailable",
            ["OOM"] = "Out of memory — process killed by kernel",
            ["OUT OF MEMORY"] = "Out of memory — unable to allocate memory",
            ["SEGFAULT"] = "Segmentation fault — accessing invalid memory region",
            ["SEGMENTATION FAULT"] = "Segmentation fault — accessing invalid memory region",
            ["KERNEL PANIC"] = "Kernel panic — OS halted",
            ["CORE DUMP"] = "Core dump — application crashed and dumped memory",
            ["BLUE SCREEN"] = "Blue Screen of Death (BSOD) — critical Windows crash",
            ["ERROR"] = "Component error",
            ["EXCEPTION"] = "Exception — unexpected program behavior",
            ["FAILED"] = "Operation failed",
            ["FAILURE"] = "Failure — operation not completed",
            ["TIMEOUT"] = "Timeout — server did not respond in time",
            ["REFUSED"] = "Connection refused — target server rejected request",
            ["DENIED"] = "Access denied — insufficient permissions",
            ["CONNECTION RESET"] = "Connection reset by remote peer",
            ["CONNECTION REFUSED"] = "Connection refused — target server unavailable",
            ["CONNECTION TIMEOUT"] = "Connection timeout — server not responding",
            ["ECONNREFUSED"] = "Network error: remote host refused connection",
            ["ECONNRESET"] = "Network error: connection reset by peer",
            ["ETIMEDOUT"] = "Network error: connection timed out",
            ["ENOTFOUND"] = "Network error: DNS name not found",
            ["STACK TRACE"] = "Stack trace — call chain leading to error",
            ["TRACEBACK"] = "Python traceback — call chain",
            ["NULL POINTER"] = "Null pointer access — object not initialized",
            ["500"] = "HTTP 500 — internal server error",
            ["502"] = "HTTP 502 — bad gateway from upstream server",
            ["503"] = "HTTP 503 — service temporarily unavailable",
            ["401"] = "HTTP 401 — authorization required",
            ["403"] = "HTTP 403 — access forbidden",
            ["WARNING"] = "Warning — potential issue",
            ["DEPRECATED"] = "Deprecated API — code should be updated",
            ["SLOW QUERY"] = "Slow database query — optimization possible",
            ["RETRY"] = "Retry attempt — previous operation failed",
            ["FALLBACK"] = "Fallback mode — primary method unavailable",
            ["429"] = "HTTP 429 — too many requests (rate limit)",
            ["DISK SPACE LOW"] = "Low disk space — possible overflow",
            ["HIGH CPU"] = "High CPU usage",
            ["HIGH MEMORY"] = "High memory consumption",
            ["HIGH LOAD"] = "High system load",
            ["INFO"] = "Informational message — system is operating normally",
            ["INFORMATION"] = "Informational message — system is operating normally",
            ["NOTICE"] = "Notice — event worth attention but not a problem",
            ["STARTED"] = "Service started — component is now running",
            ["STARTING"] = "Service starting — component is initializing",
            ["STOPPED"] = "Service stopped — component has shut down",
            ["STOPPING"] = "Service stopping — component is shutting down",
            ["CONNECTED"] = "Connection established — successfully connected to server",
            ["CONNECTING"] = "Connecting — establishing connection to server",
            ["LISTENING"] = "Listening on port — waiting for incoming connections",
            ["REQUEST"] = "Incoming request — server received a client call",
            ["RESPONSE"] = "Response sent — server processed the client request",
            ["200"] = "HTTP 200 OK — request completed successfully",
            ["201"] = "HTTP 201 Created — resource created successfully",
        }
    };

    private static readonly Dictionary<string, Dictionary<string, string[]>> Fixes = new()
    {
        ["ru"] = new()
        {
            ["OOM"] = ["Увеличь лимит памяти (Docker: --memory, K8s: resources.limits)", "Проверь утечки памяти в приложении (heap dump, профилирование)", "Оптимизируй кэширование и буферизацию"],
            ["OUT OF MEMORY"] = ["Увеличь объём оперативной памяти или лимит контейнера", "Используй ленивую загрузку данных (постранично)", "Проверь циклы с накоплением данных в памяти"],
            ["SEGFAULT"] = ["Проверь инициализацию указателей перед использованием", "Обнови зависимости — возможен баг в библиотеке", "Включи AddressSanitizer для поиска ошибки: -fsanitize=address"],
            ["SEGMENTATION FAULT"] = ["Проверь выход за границы массивов и строк", "Убедись что объекты не удалены до обращения к ним", "Собери с отладочными символами (-g) и запусти через gdb"],
            ["KERNEL PANIC"] = ["Проверь аппаратное обеспечение (RAM, диск)", "Обнови ядро ОС и драйверы", "Проанализируй дамп ядра: crash / vmcore"],
            ["CORE DUMP"] = ["Проанализируй дамп: gdb <binary> <core>", "Проверь последние изменения в коде", "Убедись что бинарник собран с -g и без -O0"],
            ["BLUE SCREEN"] = ["Обнови драйверы (особенно GPU и сетевые)", "Проверь RAM утилитой memtest86", "Отключи разгон (overclocking) и проверь температуру"],
            ["CRITICAL"] = ["Немедленно проверь статус сервера и сервисов", "Временно переключи на резервный узел", "Собери дампы и логи для анализа"],
            ["FATAL"] = ["Перезапусти сервис с мониторингом", "Проверь конфигурацию и последние деплои", "Откатись на предыдущую рабочую версию"],
            ["PANIC"] = ["Перезагрузи сервер и проверь здоровье системы", "Проверь целостность файловой системы", "Свяжись с администратором"],
            ["EMERGENCY"] = ["Активируй аварийный план восстановления", "Переключи трафик на резервный ЦОД", "Уведоми команду эксплуатации"],
            ["ERROR"] = ["Проверь логи на контекст ошибки", "Убедись что все зависимости доступны", "Перезапусти проблемный сервис"],
            ["EXCEPTION"] = ["Прочитай сообщение исключения — оно содержит причину", "Проверь трассировку стека — какой код вызвал ошибку", "Добавь обработку исключения (try/catch)"],
            ["FAILED"] = ["Проверь предусловия операции (права, ресурсы, сеть)", "Попробуй повторить операцию вручную", "Проверь что целевой сервис работает"],
            ["FAILURE"] = ["Проверь логи до этой ошибки для первопричины", "Убедись что конфигурация корректна", "Перезапусти сервис и проверь зависимости"],
            ["TIMEOUT"] = ["Увеличь таймаут в конфигурации", "Проверь сеть и доступность сервера", "Оптимизируй медленный запрос/операцию"],
            ["REFUSED"] = ["Убедись что целевой сервис запущен: systemctl status", "Проверь firewall и порты", "Проверь что сервис слушает на ожидаемом порте: ss -tlnp"],
            ["DENIED"] = ["Проверь права пользователя и группы", "Убедись что ACL и политики доступа корректны", "Проверь SELinux/AppArmor логи: audit.log"],
            ["CONNECTION RESET"] = ["Проверь что удалённый сервис не перезапускался", "Увеличь keepalive на соединение", "Проверь балансировщик нагрузки — возможен drop"],
            ["CONNECTION REFUSED"] = ["Проверь что сервис запущен: netstat/ss", "Проверь firewall и security groups", "Проверь что сервис привязан к правильному IP/port"],
            ["CONNECTION TIMEOUT"] = ["Проверь сеть: ping, traceroute к целевому хосту", "Увеличь timeout в конфигурации клиента", "Проверь что нет SYN-flood или перегрузки"],
            ["ECONNREFUSED"] = ["Проверь запущен ли целевой сервис", "Проверь порт и IP адрес подключения", "Проверь firewall: iptables -L или ufw status"],
            ["ECONNRESET"] = ["Сервер закрыл соединение — проверь его логи", "Проверь proxy/балансировщик — возможен timeout", "Попробуй повторный запрос с экспоненциальной задержкой"],
            ["ETIMEDOUT"] = ["Проверь DNS-резолвинг: nslookup/dig", "Проверь маршрутизацию: traceroute", "Увеличь timeout и добавь retry логику"],
            ["ENOTFOUND"] = ["Проверь DNS запись: nslookup <hostname>", "Проверь /etc/hosts и resolv.conf", "Проверь что домен существует и не истёк"],
            ["STACK TRACE"] = ["Найди строку в своём коде по файлу:номер_строки", "Проверь аргументы переданные в функцию", "Добавь валидацию входных данных"],
            ["TRACEBACK"] = ["Прочитай traceback снизу вверх — последняя строка = причина", "Проверь типы данных в проблемной строке", "Добавь try/except с информативным сообщением"],
            ["NULL POINTER"] = ["Добавь проверку на null/None перед доступом", "Проверь порядок инициализации объектов", "Используй Optional/Maybe паттерн"],
            ["500"] = ["Проверь логи сервера (nginx/apache/app)", "Проверь последние изменения в коде и конфиге", "Временно включи debug режим для детальной ошибки"],
            ["502"] = ["Проверь что upstream сервис работает", "Проверь конфигурацию прокси (nginx/HAProxy)", "Перезапусти upstream и балансировщик"],
            ["503"] = ["Проверь healthcheck сервиса", "Возможно перегрузка — добавь ресурсы", "Проверь что нет активного деплоя/перезапуска"],
            ["401"] = ["Проверь токен/credentials — возможно истёк", "Обнови API ключ или пароль", "Проверь OAuth flow и redirect URI"],
            ["403"] = ["Проверь права пользователя и роль", "Убедись что CSRF токен передан корректно", "Проверь IP whitelist и ACL"],
            ["DEPRECATED"] = ["Запланируй обновление кода на новый API", "Проверь документацию — deadline устаревания", "Добавь предупреждение в мониторинг"],
            ["SLOW QUERY"] = ["Добавь индекс по полям фильтрации", "Используй EXPLAIN ANALYZE для анализа плана запроса", "Ограничь выборку LIMIT и добавь пагинацию"],
            ["RETRY"] = ["Проверь что повторная попытка успешна", "Увеличь задержку между retry (exponential backoff)", "Проверь причину первоначальной неудачи"],
            ["FALLBACK"] = ["Разберись почему основной метод недоступен", "Восстанови основной путь как можно скорее", "Добавь мониторинг fallback-событий"],
            ["429"] = ["Уменьши частоту запросов (rate limiting)", "Добавь задержку между запросами", "Проверь что нет зацикленного скрипта"],
            ["DISK SPACE LOW"] = ["Удали старые логи: find /var/log -mtime +30 -delete", "Очисти кэши и временные файлы", "Увеличь объём диска или настрой ротацию логов"],
            ["HIGH CPU"] = ["Найди процесс: top/htop, ps aux --sort=-%cpu", "Проверь бесконечные циклы и тяжёлые запросы", "Настрой auto-scaling или добавь ресурсы"],
            ["HIGH MEMORY"] = ["Найди процесс: ps aux --sort=-%mem", "Проверь утечки памяти в приложении", "Перезапусти процесс-потребитель"],
            ["HIGH LOAD"] = ["Проверь CPU, I/O, память отдельно", "Найди bottleneck: iostat, vmstat, mpstat", "Рассмотри горизонтальное масштабирование"],
            ["INFO"] = ["Это нормальная работа — действий не требуется", "Убедись что информационные сообщения не маскируют ошибки", "Настрой уровень логирования если слишком много шума"],
            ["STARTED"] = ["Сервис запущен корректно — проверь что все модули загрузились", "Убедись что healthcheck проходит", "Проверь что сервис слушает на нужном порту"],
            ["STOPPED"] = ["Проверь причину остановки — запланированная или авария?", "Проверь логи перед остановкой на наличие ошибок", "Настрой graceful shutdown если нужно"],
            ["CONNECTED"] = ["Соединение установлено — проверь что протокол корректен", "Убедись что TLS-сертификат валиден", "Проверь что подключение к нужному хосту"],
            ["LISTENING"] = ["Проверь что порт правильный: ss -tlnp", "Убедись что бинд на нужный интерфейс", "Проверь что firewall пропускает подключения"],
            ["REQUEST"] = ["Проверь что запрос валидный", "Убедись что rate limiting настроен", "Проверь логирование запросов для аудита"],
            ["200"] = ["Запрос успешен — действий не требуется", "Убедись что ответ содержит ожидаемые данные", "Проверь время ответа — может быть медленным"],
            ["201"] = ["Ресурс создан — проверь что данные корректны", "Убедись что созданы все зависимые ресурсы", "Проверь что ID/URL нового ресурса возвращён"],
        },
        ["en"] = new()
        {
            ["OOM"] = ["Increase memory limit (Docker: --memory, K8s: resources.limits)", "Check for memory leaks (heap dump, profiling)", "Optimize caching and buffering"],
            ["OUT OF MEMORY"] = ["Increase RAM or container memory limit", "Use lazy/paginated data loading", "Check loops that accumulate data in memory"],
            ["SEGFAULT"] = ["Check pointer initialization before use", "Update dependencies — possible library bug", "Enable AddressSanitizer: -fsanitize=address"],
            ["SEGMENTATION FAULT"] = ["Check array/string bounds", "Ensure objects aren't freed before access", "Build with debug symbols (-g) and run under gdb"],
            ["KERNEL PANIC"] = ["Check hardware (RAM, disk)", "Update OS kernel and drivers", "Analyze kernel dump: crash / vmcore"],
            ["CORE DUMP"] = ["Analyze dump: gdb <binary> <core>", "Check recent code changes", "Ensure binary built with -g and not stripped"],
            ["BLUE SCREEN"] = ["Update drivers (especially GPU and network)", "Test RAM with memtest86", "Disable overclocking and check temps"],
            ["CRITICAL"] = ["Immediately check server and service status", "Switch to backup node temporarily", "Collect dumps and logs for analysis"],
            ["FATAL"] = ["Restart service with monitoring enabled", "Check config and recent deployments", "Rollback to last known working version"],
            ["PANIC"] = ["Reboot server and check system health", "Check filesystem integrity", "Contact system administrator"],
            ["EMERGENCY"] = ["Activate disaster recovery plan", "Switch traffic to backup datacenter", "Notify operations team"],
            ["ERROR"] = ["Check logs for error context", "Ensure all dependencies are available", "Restart the affected service"],
            ["EXCEPTION"] = ["Read the exception message — it contains the cause", "Check the stack trace — which code triggered it", "Add exception handling (try/catch)"],
            ["FAILED"] = ["Check operation preconditions (permissions, resources, network)", "Try the operation manually", "Verify target service is running"],
            ["FAILURE"] = ["Check logs before this error for root cause", "Verify configuration is correct", "Restart service and check dependencies"],
            ["TIMEOUT"] = ["Increase timeout in configuration", "Check network and server availability", "Optimize the slow request/operation"],
            ["REFUSED"] = ["Verify target service is running: systemctl status", "Check firewall rules and ports", "Ensure service listens on expected port: ss -tlnp"],
            ["DENIED"] = ["Check user/group permissions", "Verify ACL and access policies", "Check SELinux/AppArmor logs: audit.log"],
            ["CONNECTION RESET"] = ["Check if remote service restarted", "Increase connection keepalive", "Check load balancer — possible connection drop"],
            ["CONNECTION REFUSED"] = ["Verify service is running: netstat/ss", "Check firewall and security groups", "Ensure service binds to correct IP/port"],
            ["CONNECTION TIMEOUT"] = ["Check network: ping, traceroute to target host", "Increase timeout in client config", "Check for SYN-flood or network congestion"],
            ["ECONNREFUSED"] = ["Check if target service is running", "Verify port and IP address", "Check firewall: iptables -L or ufw status"],
            ["ECONNRESET"] = ["Server closed connection — check its logs", "Check proxy/load balancer for timeout", "Retry with exponential backoff"],
            ["ETIMEDOUT"] = ["Check DNS resolution: nslookup/dig", "Check routing: traceroute", "Increase timeout and add retry logic"],
            ["ENOTFOUND"] = ["Check DNS record: nslookup <hostname>", "Check /etc/hosts and resolv.conf", "Verify domain exists and hasn't expired"],
            ["STACK TRACE"] = ["Find the line in your code by file:line_number", "Check arguments passed to the function", "Add input validation"],
            ["TRACEBACK"] = ["Read traceback bottom-up — last line = root cause", "Check data types at the problem line", "Add try/except with informative message"],
            ["NULL POINTER"] = ["Add null/None check before access", "Check object initialization order", "Use Optional/Maybe pattern"],
            ["500"] = ["Check server logs (nginx/apache/app)", "Review recent code and config changes", "Temporarily enable debug mode for details"],
            ["502"] = ["Verify upstream service is running", "Check proxy config (nginx/HAProxy)", "Restart upstream and load balancer"],
            ["503"] = ["Check service healthcheck", "Possible overload — add resources", "Ensure no active deployment/restart"],
            ["401"] = ["Check token/credentials — may have expired", "Refresh API key or password", "Verify OAuth flow and redirect URI"],
            ["403"] = ["Check user permissions and role", "Ensure CSRF token is correct", "Check IP whitelist and ACL"],
            ["DEPRECATED"] = ["Plan migration to the new API", "Check docs for deprecation deadline", "Add deprecation warning monitoring"],
            ["SLOW QUERY"] = ["Add index on filter columns", "Use EXPLAIN ANALYZE to review query plan", "Add LIMIT and pagination"],
            ["RETRY"] = ["Check if retry succeeded", "Increase delay between retries (exponential backoff)", "Investigate original failure cause"],
            ["FALLBACK"] = ["Investigate why primary method is unavailable", "Restore primary path ASAP", "Add monitoring for fallback events"],
            ["429"] = ["Reduce request frequency", "Add delay between requests", "Check for looping scripts"],
            ["DISK SPACE LOW"] = ["Delete old logs: find /var/log -mtime +30 -delete", "Clear caches and temp files", "Increase disk size or configure log rotation"],
            ["HIGH CPU"] = ["Find process: top/htop, ps aux --sort=-%cpu", "Check for infinite loops and heavy queries", "Set up auto-scaling or add resources"],
            ["HIGH MEMORY"] = ["Find process: ps aux --sort=-%mem", "Check for memory leaks", "Restart high-memory process"],
            ["HIGH LOAD"] = ["Check CPU, I/O, memory separately", "Find bottleneck: iostat, vmstat, mpstat", "Consider horizontal scaling"],
            ["INFO"] = ["This is normal operation — no action needed", "Make sure info messages aren't masking errors", "Adjust log level if there's too much noise"],
            ["STARTED"] = ["Service started correctly — verify all modules loaded", "Ensure healthcheck passes", "Check service is listening on the right port"],
            ["STOPPED"] = ["Check if stop was planned or unexpected", "Check logs before shutdown for errors", "Configure graceful shutdown if needed"],
            ["CONNECTED"] = ["Connection established — verify protocol is correct", "Ensure TLS certificate is valid", "Check connection is to the right host"],
            ["LISTENING"] = ["Verify the port is correct: ss -tlnp", "Ensure binding on the right interface", "Check firewall allows incoming connections"],
            ["REQUEST"] = ["Verify the request is valid", "Ensure rate limiting is configured", "Check request logging for audit"],
            ["200"] = ["Request succeeded — no action needed", "Ensure response contains expected data", "Check response time — may be slow"],
            ["201"] = ["Resource created — verify data is correct", "Ensure all dependent resources are created", "Check that new resource ID/URL is returned"],
        }
    };

    private static readonly Dictionary<LogLevel, Regex[]> Patterns = new()
    {
        [LogLevel.Critical] =
        [
            new(@"\bCRITICAL\b", RegexOptions.IgnoreCase),
            new(@"\bFATAL\b", RegexOptions.IgnoreCase),
            new(@"\bPANIC\b", RegexOptions.IgnoreCase),
            new(@"\bEMERG(?:ENCY)?\b", RegexOptions.IgnoreCase),
            new(@"\bOOM\b", RegexOptions.IgnoreCase),
            new(@"\bSEGFAULT\b", RegexOptions.IgnoreCase),
            new(@"\bCORE\s*DUMP\b", RegexOptions.IgnoreCase),
            new(@"\bBLUE\s*SCREEN\b", RegexOptions.IgnoreCase),
            new(@"\bKERNEL\s*PANIC\b", RegexOptions.IgnoreCase),
            new(@"\bOUT\s*OF\s*MEMORY\b", RegexOptions.IgnoreCase),
            new(@"\bАВАРИЯ\b", RegexOptions.IgnoreCase),
            new(@"\bКРИТИЧЕСК(?:ИЙ|АЯ|ОЕ|ИЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bФАТАЛЬН(?:АЯ|ЫЙ|ОЕ|ЫЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bПАНИКА\b", RegexOptions.IgnoreCase),
        ],
        [LogLevel.Error] =
        [
            new(@"\bERROR\b", RegexOptions.IgnoreCase),
            new(@"\bERR\b(?!\w)", RegexOptions.IgnoreCase),
            new(@"\bEXCEPTION\b", RegexOptions.IgnoreCase),
            new(@"\bFAILED\b", RegexOptions.IgnoreCase),
            new(@"\bFAILURE\b", RegexOptions.IgnoreCase),
            new(@"\bTIMEOUT\b", RegexOptions.IgnoreCase),
            new(@"\bREFUSED\b", RegexOptions.IgnoreCase),
            new(@"\bDENIED\b", RegexOptions.IgnoreCase),
            new(@"\bUNABLE\s*TO\b", RegexOptions.IgnoreCase),
            new(@"\bCANNOT\b", RegexOptions.IgnoreCase),
            new(@"\bCOULD\s*NOT\b", RegexOptions.IgnoreCase),
            new(@"\bSTACK\s*TRACE\b", RegexOptions.IgnoreCase),
            new(@"\bTRACEBACK\b", RegexOptions.IgnoreCase),
            new(@"\bNULL\s*POINTER\b", RegexOptions.IgnoreCase),
            new(@"\bSEGMENTATION\s*FAULT\b", RegexOptions.IgnoreCase),
            new(@"\bCONNECTION\s*RESET\b", RegexOptions.IgnoreCase),
            new(@"\bCONNECTION\s*REFUSED\b", RegexOptions.IgnoreCase),
            new(@"\bCONNECTION\s*TIMEOUT\b", RegexOptions.IgnoreCase),
            new(@"\bECONNREFUSED\b"),
            new(@"\bECONNRESET\b"),
            new(@"\bETIMEDOUT\b"),
            new(@"\bENOTFOUND\b"),
            new(@"\b500\s+(?:INTERNAL\s+SERVER\s+ERROR)?\b"),
            new(@"\b502\s+(?:BAD\s+GATEWAY)?\b"),
            new(@"\b503\s+(?:SERVICE\s+UNAVAILABLE)?\b"),
            new(@"\b401\s+(?:UNAUTHORIZED)?\b"),
            new(@"\b403\s+(?:FORBIDDEN)?\b"),
            new(@"\bОШИБКА\b", RegexOptions.IgnoreCase),
            new(@"\bОШИБК(?:У|И|АМИ)\b", RegexOptions.IgnoreCase),
            new(@"\bОТКАЗ\b", RegexOptions.IgnoreCase),
            new(@"\bСБОЙ\b", RegexOptions.IgnoreCase),
            new(@"\bНЕШТАТН(?:АЯ|ЫЙ|ОЕ|ЫЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bНЕУДАЧА\b", RegexOptions.IgnoreCase),
            new(@"\bИСКЛЮЧЕНИ(?:Е|Я)\b", RegexOptions.IgnoreCase),
            new(@"\bТАЙМАУТ\b", RegexOptions.IgnoreCase),
            new(@"\bНЕДОСТУПЕН\b", RegexOptions.IgnoreCase),
            new(@"\bДОСТУП\s*ЗАПРЕЩ[ЁЕ]Н\b", RegexOptions.IgnoreCase),
        ],
        [LogLevel.Warning] =
        [
            new(@"\bWARN(?:ING)?\b", RegexOptions.IgnoreCase),
            new(@"\bDEPRECATED\b", RegexOptions.IgnoreCase),
            new(@"\bSLOW\s*QUERY\b", RegexOptions.IgnoreCase),
            new(@"\bRETRY(?:ING)?\b", RegexOptions.IgnoreCase),
            new(@"\bRETRYABLE\b", RegexOptions.IgnoreCase),
            new(@"\bPARTIAL\b", RegexOptions.IgnoreCase),
            new(@"\bFALLBACK\b", RegexOptions.IgnoreCase),
            new(@"\b429\s+(?:TOO\s+MANY\s+REQUESTS)?\b"),
            new(@"\bDISK\s*(?:SPACE|USAGE)\s*(?:LOW|HIGH)\b", RegexOptions.IgnoreCase),
            new(@"\bHIGH\s*(?:CPU|MEMORY|LOAD)\b", RegexOptions.IgnoreCase),
            new(@"\bПРЕДУПРЕЖДЕН(?:ИЕ|ИЯ)\b", RegexOptions.IgnoreCase),
            new(@"\bВНИМАНИЕ\b", RegexOptions.IgnoreCase),
            new(@"\bУСТАРЕЛ(?:О|А|ЫЙ|ЫЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bМЕДЛЕНН(?:ЫЙ|ОЕ|АЯ|ЫЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bПОВТОРН(?:АЯ|ЫЙ|ОЕ|ЫЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bВЫСОК(?:АЯ|ИЙ|ОЕ|ИЕ)\b", RegexOptions.IgnoreCase),
            new(@"\bПЕРЕПОЛНЕН(?:ИЕ|ИЯ)\b", RegexOptions.IgnoreCase),
        ],
        [LogLevel.Info] =
        [
            new(@"\bINFO\b", RegexOptions.IgnoreCase),
            new(@"\bINFORMATION\b", RegexOptions.IgnoreCase),
            new(@"\bNOTICE\b", RegexOptions.IgnoreCase),
            new(@"\bSTART(?:ED|ING)?\b", RegexOptions.IgnoreCase),
            new(@"\bSTOP(?:PED|PING)?\b", RegexOptions.IgnoreCase),
            new(@"\bCONNECT(?:ED|ING)?\b", RegexOptions.IgnoreCase),
            new(@"\bLISTEN(?:ING)?\b", RegexOptions.IgnoreCase),
            new(@"\bREQUEST\b", RegexOptions.IgnoreCase),
            new(@"\bRESPONSE\b", RegexOptions.IgnoreCase),
            new(@"\b200\s+OK\b"),
            new(@"\b201\s+CREATED\b"),
            new(@"\bИНФОРМАЦИЯ\b", RegexOptions.IgnoreCase),
            new(@"\bИНФО\b", RegexOptions.IgnoreCase),
            new(@"\bЗАПУЩЕН(?:О|А|ЫЙ)?\b", RegexOptions.IgnoreCase),
            new(@"\bОСТАНОВЛЕН(?:О|А|ЫЙ)?\b", RegexOptions.IgnoreCase),
            new(@"\bПОДКЛЮЧ[ЁЕ]Н(?:О|А|ЫЙ)?\b", RegexOptions.IgnoreCase),
        ],
    };

    private static readonly Regex[] StackTracePatterns =
    [
        new(@"^\s+at\s+.+\s\(.+\)$"),
        new(@"^\s+at\s+.+$"),
        new(@"^File\s+"".+""\s*,\s+line\s+\d+"),
        new(@"^Traceback\s+\(most\s+recent\s+call\s+last\)"),
        new(@"^Caused\s+by:"),
        new(@"^\s+\.\.\.\s+\d+\s+more"),
        new(@"^---\s+End\s+of\s+inner\s+exception"),
        new(@"^\s+→"),
        new(@"^\s+в\s+.+\.rb:\d+"),
        new(@"^\s+в\s+.+\.py:\d+"),
        new(@"^Трассировк(?:а|и)\s+стека"),
        new(@"^Причин(?:а|ы):"),
    ];

    private static readonly Dictionary<int, LogLevel> SyslogLevels = new()
    {
        [0] = LogLevel.Critical, [1] = LogLevel.Critical, [2] = LogLevel.Critical,
        [3] = LogLevel.Error, [4] = LogLevel.Warning, [5] = LogLevel.Info,
        [6] = LogLevel.Info, [7] = LogLevel.Debug
    };

    private static readonly Dictionary<int, string> SyslogLevelNames = new()
    {
        [0] = "EMERGENCY", [1] = "ALERT", [2] = "CRITICAL", [3] = "ERROR",
        [4] = "WARNING", [5] = "NOTICE", [6] = "INFO", [7] = "DEBUG"
    };

    private static readonly Regex SyslogPattern = new(
        @"^(<(\d{1,3})>)?(\w{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2})\s+(\S+)\s+([^\[:]+?)(?:\[(\d+)\])?:\s*(.*)$",
        RegexOptions.Multiline);

    private static readonly Regex NginxAccessPattern = new(
        @"^(\S+)\s+-\s+(\S+)\s+\[([^\]]+)\]\s+""(\S+)\s+(\S+)\s+(\S+)""\s+(\d{3})\s+(\d+)(?:\s+""([^""]*)""\s+""([^""]*)"")?(?:\s+(\S+))?.*$",
        RegexOptions.Multiline);

    private static readonly Regex WinEventPattern = new(@"<Event[^>]*>([\s\S]*?)</Event>");
    private static readonly Regex WinEventTime = new(@"<TimeCreated\s+SystemTime='([^']*)'");
    private static readonly Regex WinEventProvider = new(@"<Provider\s+Name='([^']*)'");
    private static readonly Regex WinEventId = new(@"<EventID[^>]*>(\d+)");
    private static readonly Regex WinEventLevel = new(@"<Level>(\d+)");
    private static readonly Regex WinEventData = new(@"<Data[^>]*>([\s\S]*?)</Data>");

    private static readonly Regex SyslogDetect = new(@"^(?:<\d{1,3}>)?\w{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\S+", RegexOptions.Multiline);
    private static readonly Regex NginxDetect = new(@"^\S+\s+-\s+-\s+\[.*?\]\s+""(?:GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s", RegexOptions.Multiline);
    private static readonly Regex WinEventDetect = new(@"<Event\b[^>]*>");

    private static LogLevel? DetectLevel(string line)
    {
        foreach (var (level, patterns) in Patterns)
        {
            foreach (var pat in patterns)
            {
                if (pat.IsMatch(line)) return level;
            }
        }
        return null;
    }

    private static bool IsStackTrace(string line)
    {
        return StackTracePatterns.Any(p => p.IsMatch(line));
    }

    private static string? ExplainLine(string line, string lang)
    {
        var dict = Explanations.GetValueOrDefault(lang, Explanations["en"]);
        var upper = line.ToUpperInvariant();
        foreach (var key in dict.Keys)
        {
            if (upper.Contains(key.ToUpperInvariant())) return dict[key];
        }
        return null;
    }

    private static string[]? FixLine(string line, string lang)
    {
        var dict = Fixes.GetValueOrDefault(lang, Fixes["en"]);
        var upper = line.ToUpperInvariant();
        foreach (var key in dict.Keys)
        {
            if (upper.Contains(key.ToUpperInvariant())) return dict[key];
        }
        return null;
    }

    private static LogEntry? ParseLine(string raw, int lineNum, string lang)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        var level = DetectLevel(raw);
        var isStack = IsStackTrace(raw);
        var explanation = level.HasValue ? ExplainLine(trimmed, lang) : null;
        var fixes = level.HasValue ? FixLine(trimmed, lang) : null;
        return new LogEntry(lineNum, raw, trimmed, level, isStack, explanation, fixes);
    }

    private static string PreprocessSyslog(string content)
    {
        return SyslogPattern.Replace(content, m =>
        {
            var priNumStr = m.Groups[2].Value;
            var timestamp = m.Groups[3].Value;
            var host = m.Groups[4].Value;
            var proc = m.Groups[5].Value;
            var pid = m.Groups[6].Value;
            var msg = m.Groups[7].Value;
            var priNum = int.TryParse(priNumStr, out var n) ? n : -1;
            var facility = priNum >= 0 ? priNum / 8 : -1;
            var severity = priNum >= 0 ? priNum % 8 : -1;
            var lvlName = severity is >= 0 and <= 7 ? SyslogLevelNames[severity] : "";
            var pidPart = string.IsNullOrEmpty(pid) ? "" : $"/{pid}";
            return $"{timestamp} {host} {lvlName} [{proc}{pidPart}][facility={facility}]: {msg}";
        });
    }

    private static string PreprocessNginxAccess(string content)
    {
        return NginxAccessPattern.Replace(content, m =>
        {
            var ip = m.Groups[1].Value;
            var ts = m.Groups[3].Value;
            var method = m.Groups[4].Value;
            var url = m.Groups[5].Value;
            var status = m.Groups[7].Value;
            var size = m.Groups[8].Value;
            var rt = m.Groups[11].Value;
            var s = int.TryParse(status, out var code) ? code : 0;
            var lvl = s >= 500 ? "ERROR" : s >= 300 ? "WARNING" : "INFO";
            var rtPart = string.IsNullOrEmpty(rt) ? "" : $" {rt}ms";
            return $"{ts} {lvl} [nginx][{method} {url}] {status} {size}B{rtPart} ip={ip}";
        });
    }

    private static string PreprocessWinEventXml(string content)
    {
        var results = new List<string>();
        var lvlMap = new Dictionary<string, string>
        {
            ["1"] = "CRITICAL", ["2"] = "ERROR", ["3"] = "WARNING", ["4"] = "INFO", ["5"] = "INFO"
        };
        var matches = WinEventPattern.Matches(content);
        foreach (Match ev in matches)
        {
            var body = ev.Groups[1].Value;
            var timeCreated = WinEventTime.Match(body).Groups[1].Value;
            var provider = WinEventProvider.Match(body).Groups[1].Value;
            var eventId = WinEventId.Match(body).Groups[1].Value;
            var lvlNum = WinEventLevel.Match(body).Groups[1].Value;
            var msg = WinEventData.Match(body).Groups[1].Value.Trim();
            var lvl = lvlMap.GetValueOrDefault(lvlNum, "INFO");
            results.Add($"{timeCreated} {lvl} [{provider}][EventID={eventId}]: {(string.IsNullOrEmpty(msg) ? lvl : msg)}");
        }
        return results.Count > 0 ? string.Join("\n", results) : content;
    }

    private static string Preprocess(string content)
    {
        if (WinEventDetect.IsMatch(content)) return PreprocessWinEventXml(content);
        if (NginxDetect.IsMatch(content)) return PreprocessNginxAccess(content);
        if (SyslogDetect.IsMatch(content)) return PreprocessSyslog(content);
        return content;
    }

    public static AnalysisResult Parse(string content, string lang = "en")
    {
        var preprocessed = Preprocess(content);
        var lines = preprocessed.Split('\n');
        var parsed = new List<LogEntry>();
        var issues = new List<Issue>();
        var stats = new LogStats(0, 0, 0, 0, lines.Length);
        Issue? currentIssue = null;
        var currentClosed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var entry = ParseLine(lines[i], i + 1, lang);
            if (entry == null) continue;
            parsed.Add(entry);

            if (entry.Level.HasValue && LevelOrder.ContainsKey(entry.Level.Value))
            {
                switch (entry.Level.Value)
                {
                    case LogLevel.Critical: stats = stats with { Critical = stats.Critical + 1 }; break;
                    case LogLevel.Error: stats = stats with { Error = stats.Error + 1 }; break;
                    case LogLevel.Warning: stats = stats with { Warning = stats.Warning + 1 }; break;
                    case LogLevel.Info: stats = stats with { Info = stats.Info + 1 }; break;
                }

                if (entry.Level.Value >= LogLevel.Warning)
                {
                    if (currentIssue != null && currentIssue.Level == entry.Level.Value && !currentClosed)
                    {
                        currentIssue.Lines.Add(entry.LineNum);
                    }
                    else
                    {
                        currentClosed = false;
                        currentIssue = new Issue(entry.Level.Value, [entry.LineNum],
                            entry.Trimmed.Length > 200 ? entry.Trimmed[..200] : entry.Trimmed,
                            entry.Explanation, entry.Fixes);
                        issues.Add(currentIssue);
                    }
                }
            }

            if (entry.IsStack && currentIssue != null && !currentClosed)
                currentIssue.Lines.Add(entry.LineNum);

            if (!entry.IsStack && !entry.Level.HasValue && currentIssue != null && !currentClosed)
                currentClosed = true;
        }

        issues.Sort((a, b) => LevelOrder[b.Level] - LevelOrder[a.Level]);
        return new AnalysisResult(parsed, issues, stats);
    }
}
