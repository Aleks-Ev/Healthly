# Healthly — руководство для команды

Как за 10 минут запустить проект у себя, войти под нужной ролью, найти документацию API и подключить мобильное приложение.

| Документ | Для чего |
|---|---|
| **TEAM_GUIDE.md** (этот файл) | Запуск, тестовые аккаунты, документация, адреса для мобильного приложения |
| [MOBILE_HANDOFF.md](MOBILE_HANDOFF.md) | Подробный справочник API на английском: все эндпоинты с примерами JSON, ошибки, код для Flutter / React Native |
| [PROJECT_GUIDE.md](PROJECT_GUIDE.md) | Карта проекта, как устроены процессы (для защиты диплома), roadmap |
| [README_CONTRIBUTOR.md](README_CONTRIBUTOR.md) | Работа с Git: ветки, коммиты, Pull Request |
| [HealthlyApi/Data/REFERENCE_VALUES.md](HealthlyApi/Data/REFERENCE_VALUES.md) | Справочник норм анализов — инструкция для врачей-консультантов (на английском) |

> ⚠️ **Важно для всей команды: нормы анализов — ЧЕРНОВИК, врач их ещё не проверял.**
> Нормы и критические значения лежат в `HealthlyApi/Data/reference-values.csv` (63 строки, взрослые). Их подготовили
> разработчики по общедоступным источникам (колонка `Source`), но **ни одна строка не подписана врачом** (`ReviewedBy`
> пустой) — API при каждом запуске пишет об этом предупреждение в лог.
> - Не показывайте оценки «норма / отклонение / критично» реальным пациентам, пока медсоветники не проверят файл.
> - Передайте файл и [инструкцию](HealthlyApi/Data/REFERENCE_VALUES.md) медсоветникам; их правки — через PR или файлом.
> - Нормы **не правятся в коде** — только в CSV. Ошибка в файле (минимум больше максимума, пересечения строк)
>   останавливает запуск API с номером строки — так и задумано.

---

## 1. Что установить

| Что | Зачем | Проверка |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Самый простой запуск — всё одной командой (способ А) | `docker --version` |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Запуск бэкенда без Docker (способ Б) | `dotnet --version` → `10.x` |
| [PostgreSQL](https://www.postgresql.org/download/) + pgAdmin | База данных для способа Б | pgAdmin открывается |
| [Node.js 24](https://nodejs.org/) | Веб-фронтенд без Docker | `node --version` → `v24.x` |
| EF Core CLI | Миграции базы (способ Б) | `dotnet tool install --global dotnet-ef` |

Мобильной команде для работы с API достаточно **Docker Desktop** (способ А).

---

## 2. Запуск бэкенда

### Способ А — Docker (рекомендуется)

Из корня репозитория:

```bash
docker compose up -d --build
```

Через 1–2 минуты работают:

| Сервис | Адрес |
|---|---|
| Сайт (Next.js) | http://localhost:3000 |
| API (.NET 10) | http://localhost:5097 |
| Документация API (Scalar) | http://localhost:5097/scalar |
| PostgreSQL | `localhost:5433`, база `healthly_db`, пользователь `healthly`, пароль `healthly_dev_password` |

При первом запуске API сам создаёт таблицы (миграции) и тестовые аккаунты (раздел 3).

| Команда | Что делает |
|---|---|
| `docker compose logs -f api` | Логи API (ошибки, запросы) |
| `docker compose up -d --build` | Пересобрать после `git pull` |
| `docker compose down` | Остановить; база и загруженные файлы **сохраняются** (тома `healthly_postgres-data`, `healthly_uploads`) |
| `docker compose down -v` | Остановить и **удалить** базу и файлы — чистый старт |

Пароли и ключи для Docker можно поменять в файле `.env` (скопируйте `.env.example` → `.env`). Без `.env` работают
значения по умолчанию — для локальной разработки этого достаточно.

> Порты 3000 и 5097 те же, что у способа Б: одновременно Docker и `dotnet run` / `npm run dev` не запустятся.
> PostgreSQL в Docker — на порту **5433**, чтобы не мешать локальному PostgreSQL на 5432.

### Способ Б — локально (.NET + свой PostgreSQL)

**1. Создайте пустую базу** `healthly_db` в pgAdmin (или `createdb healthly_db`).

**2. Пропишите секреты** — они хранятся вне репозитория, в `dotnet user-secrets` (папка `HealthlyApi`):

```bash
cd HealthlyApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=healthly_db;Username=postgres;Password=<ваш пароль от PostgreSQL>"
dotnet user-secrets set "Jwt:Key" "<любая случайная строка, минимум 32 символа>"
```

Проверить: `dotnet user-secrets list`. Без `Jwt:Key` API не запустится и напишет, какую команду выполнить.

Необязательно — ключи ИИ для `POST /api/documents/{id}/analyze` (без них работает заглушка `isMock: true`).
Оба бесплатные: Gemini — https://aistudio.google.com/apikey, Groq — https://console.groq.com/keys.

```bash
dotnet user-secrets set "Ai:Gemini:ApiKey" "ВСТАВЬТЕ_КЛЮЧ_GEMINI"
dotnet user-secrets set "Ai:Groq:ApiKey" "ВСТАВЬТЕ_КЛЮЧ_GROQ"
```

> ⚠️ Вставляйте ключ **как есть** — без угловых скобок `< >` и лишних кавычек внутри. Ключ Groq начинается с `gsk_`.
> Если анализ отвечает `503`, причина — в логе API: `Invalid API Key` / `API_KEY_INVALID` — неверный ключ,
> `model ... does not exist` / `no longer available` — модель выведена из обращения, поменяйте `Ai:Gemini:Model` /
> `Ai:Groq:Model` в `appsettings.json` (список моделей — в консоли Google AI Studio / Groq).

В Docker — те же ключи в `.env`: `GEMINI_API_KEY=...`, `GROQ_API_KEY=...`.

Необязательно — **Tesseract** для анализа фото и сканов (в Docker уже установлен; без него фото получают `503`,
PDF с текстом работают). На Windows, в PowerShell:

```powershell
winget install UB-Mannheim.TesseractOCR
# Русский и турецкий языки — в папку пользователя (в Program Files нужны права администратора)
$dir = "$env:LOCALAPPDATA\tessdata"; New-Item -ItemType Directory -Force $dir
foreach ($l in 'eng','rus','tur','osd') { Invoke-WebRequest "https://github.com/tesseract-ocr/tessdata/raw/main/$l.traineddata" -OutFile "$dir\$l.traineddata" }
cd HealthlyApi
dotnet user-secrets set "Ocr:TesseractPath" "C:\Program Files\Tesseract-OCR\tesseract.exe"
dotnet user-secrets set "Ocr:TessdataPath" "$dir"
```

Проверка: `& "C:\Program Files\Tesseract-OCR\tesseract.exe" --tessdata-dir "$env:LOCALAPPDATA\tessdata" --list-langs`
— в списке должны быть `eng`, `rus`, `tur`. Тесты OCR (`dotnet test`) находят Tesseract сами; если его нет — пропускаются.

**3. Создайте таблицы:**

```bash
dotnet ef database update
```

**4. Запустите API:**

```bash
dotnet run                          # только для этого компьютера: http://localhost:5097
dotnet run --launch-profile lan     # доступен и с телефона/эмулятора в той же Wi-Fi (раздел 5)
```

Готово, когда в консоли появилось `Now listening on: http://...:5097`.

**5. Сайт (по желанию):**

```bash
cd healthly-web
npm install        # один раз
npm run dev        # http://localhost:3000
```

**Тесты:** `dotnet test` в корне репозитория (проект `HealthlyApi.Tests`, xUnit).

**Миграции после `git pull`.** Если кто-то изменил модели, выполните `dotnet ef database update` (в Docker — не нужно,
применяется само). Новую миграцию создаёт тот, кто меняет модель: `dotnet ef migrations add <Название>`.

---

## 3. Тестовые роли и аккаунты

### Роли

| Роль | Кто это | Что может |
|---|---|---|
| **Patient** | Пациент. Все, кто регистрируется на сайте или в приложении | Видит и редактирует **только свои** данные: профиль, документы, показатели, отчёт, графики. Меняет свой пароль |
| **Doctor** | Врач | Видит **всех** пациентов и их медкарты, вносит и исправляет показатели, меняет статус документов (модерация). Пользователей не создаёт и не удаляет |
| **Admin** | Администратор | Всё, что врач, плюс: создать/изменить/удалить пользователя, назначить роль, задать пароль, удалить любой документ, управлять лабораториями (`/admin/laboratories`) |
| **LabStaff** | Сотрудник лаборатории-партнёра | Работает на `/lab`: находит пациента по Healthly ID, загружает бланк с компьютера лаборатории, анализирует и подтверждает. Открывает **только документы своей лаборатории** — не медкарту, не отчёт, не графики пациента |

Сделать пользователя врачом, админом или сотрудником лаборатории может только Admin (страница `/admin/users` → Edit → Role;
для LabStaff — ещё выбрать лабораторию).

### Аккаунты

Создаются автоматически при запуске API в режиме **Development** (и в Docker, и через `dotnet run`):

| Роль | Email | Пароль | Что внутри |
|---|---|---|---|
| Patient | `patient@healthly.com` | `Patient123!` | Elif Kaya, Стамбул. 6 анализов за последние 6 месяцев (глюкоза, холестерин, гемоглобин — выходят из отклонений к норме) + 1 документ «на проверке» |
| Doctor | `doctor@healthly.com` | `Doctor123!` | Dr. Mehmet Yilmaz, Анкара. Своя история анализов, всё в норме |
| Admin | `admin@healthly.com` | `Admin123!` | Healthly Admin, Измир. Своя история анализов, холестерин растёт |
| LabStaff | `lab@healthly.com` | `Lab123!` | Deniz Lab — сотрудник демо-лаборатории «Healthly Partner Lab — Istanbul». После входа — рабочее место `/lab`: найти пациента по Healthly ID (у `patient@` он в профиле), загрузить бланк, проанализировать и подтвердить |

- Аккаунт создаётся, **только если такого email ещё нет**; пароль существующего аккаунта не перезаписывается.
  Например, у владельца проекта `admin@healthly.com` был создан раньше — там другой пароль.
- Если вы сменили пароль демо-аккаунта и забыли его — войдите под админом и задайте новый
  (`/admin/users` → Edit → New password) или начните с чистой базы (`docker compose down -v`).
- Даты анализов отсчитываются от первого запуска. Чтобы снова получить «последние 6 месяцев» (или вернуть
  испорченные демо-данные): один раз запустите API с флагом `DemoData:ResetHistory`:
  `DemoData__ResetHistory=true dotnet run` (bash) или `DEMO_RESET_HISTORY=true` в `.env` для Docker.
- Своих пациентов можно регистрировать сколько угодно: http://localhost:3000/register или `POST /api/auth/register`.
- **На публичном демо-сервере https://healthilyclub.com этих аккаунтов нет** (Production — их пароли записаны в
  репозитории). Там есть только админ владельца проекта; врача и сотрудника лаборатории он создаёт на странице Users,
  пациенты регистрируются сами. Нужен аккаунт врача/лаборатории для проверки — попросите владельца (раздел 7).

---

## 4. Документация API

### Scalar — интерактивная документация

**http://localhost:5097/scalar** (только в режиме Development — локально и в Docker по умолчанию).

Там все эндпоинты, сгруппированные по разделам (Auth, Users, Documents, Test Results, Reports): описание, поля
запроса и ответа, все коды ошибок и кнопка **Test Request**, чтобы вызвать метод прямо из браузера.

**Как вызвать защищённый метод в Scalar:**

1. Раздел **Auth → Log in** → Test Request → в теле `{ "email": "patient@healthly.com", "password": "Patient123!" }` → Send.
2. Скопируйте из ответа значение `token` (без кавычек).
3. Справа в блоке **Authentication** вставьте его в поле **Bearer Token** (слово `Bearer` писать не нужно).
4. Теперь любой метод вызывается от имени этого пользователя. Токен живёт 15 минут — потом повторите шаг 1.

### Другие источники

| Где | Что |
|---|---|
| http://localhost:5097/openapi/v1.json | Спецификация OpenAPI 3.1 — для генерации клиента (`openapi-generator` для Dart, `openapi-typescript` для TS) |
| `HealthlyApi/HealthlyApi.http` | Готовые запросы для VS Code (REST Client) / Rider / Visual Studio |
| [MOBILE_HANDOFF.md](MOBILE_HANDOFF.md) | Справочник для мобильной разработки: все эндпоинты с примерами JSON, обработка ошибок, код для Flutter / React Native |
| `healthly-web/types/api.ts` | Готовые TypeScript-типы всех запросов и ответов |

### Главное об API в двух абзацах

**Авторизация.** `POST /api/auth/login` возвращает `token` (access-токен, живёт 15 минут) и `refreshToken` (30 дней).
`token` отправляется в каждом запросе заголовком `Authorization: Bearer <token>`. Когда запрос вернул `401` — вызовите
`POST /api/auth/refresh { "refreshToken": "..." }`, сохраните **оба** новых токена и повторите запрос. Выход —
`POST /api/auth/logout`, смена своего пароля — `PUT /api/auth/me/password`. Веб-сайт хранит refresh-токен
в httpOnly-cookie (заголовок `X-Refresh-Token-Transport: cookie`); мобильному приложению это не нужно — оно получает
токен в JSON и хранит его в защищённом хранилище (`flutter_secure_storage` / `expo-secure-store`).

**Ошибки.** Все ошибки — JSON в формате ProblemDetails: показывайте пользователю поле `detail`; у ошибок валидации (`400`)
вместо него — `errors` с сообщениями по полям. Поле `traceId` прикладывайте к баг-репортам — по нему ошибка находится
в логах сервера.

---

## 5. Базовые URL для мобильного приложения

Все эндпоинты начинаются с `/api`, базовый адрес — `http://<хост>:5097`.

| Где запущено приложение | Базовый URL | Условия |
|---|---|---|
| **Android-эмулятор** | `http://10.0.2.2:5097` | `10.0.2.2` — это `localhost` компьютера, на котором запущен эмулятор |
| **iOS-симулятор** | `http://localhost:5097` | Симулятор использует сеть Mac |
| **Телефон в той же Wi-Fi** | `http://<IP компьютера>:5097`, например `http://192.168.1.152:5097` | API запущен через Docker или `dotnet run --launch-profile lan`; открыт порт в брандмауэре (ниже) |
| **Телефон в любой сети — демо-сервер** | `https://healthilyclub.com` | Компьютер владельца проекта включён и запущен с `--profile tunnel` (раздел 7). Настраивать ничего не нужно — это HTTPS |
| **Телефон в любой сети — свой API** | `https://<id>.ngrok-free.app` | На компьютере с API: `ngrok http 5097`. Адрес меняется при каждом перезапуске ngrok (бесплатный тариф) |

Храните базовый URL в одной настройке (`--dart-define=API_URL=...` во Flutter, `.env` / `app.config.ts` в React Native),
чтобы переключаться между вариантами без правки кода.

**IP компьютера:** Windows — `ipconfig` → адаптер Wi-Fi → «IPv4-адрес»; macOS — `ipconfig getifaddr en0`.
Берите адрес вида `192.168.x.x` / `10.x.x.x`, а не адреса VPN.

**Брандмауэр Windows** (один раз, PowerShell **от имени администратора**), иначе телефон не достучится:

```powershell
New-NetFirewallRule -DisplayName "Healthly API 5097" -Direction Inbound -Protocol TCP -LocalPort 5097 -Action Allow -Profile Private
```

Сеть Wi-Fi в Windows должна быть «Частной».

**HTTP без шифрования в debug-сборках.** Локальные адреса — `http://`, а Android и iOS по умолчанию его блокируют
(адрес ngrok — `https://`, с ним настраивать ничего не нужно):

- Android: в `android/app/src/debug/AndroidManifest.xml` у `<application>` — `android:usesCleartextTraffic="true"`.
- iOS: в `Info.plist` (debug) — `NSAppTransportSecurity` → `NSAllowsLocalNetworking = YES`, и `NSLocalNetworkUsageDescription`
  для доступа к локальной сети.
- Expo Go: работает без настроек.

**Проверка связи** — откройте в браузере телефона `http://<IP компьютера>:5097/openapi/v1.json`: должен показаться JSON.
Если нет — проблема в сети/брандмауэре, а не в приложении.

---

## 6. Частые проблемы

| Симптом | Решение |
|---|---|
| `dotnet run`: «Jwt:Key is missing» | Не задан секрет — шаг 2 способа Б |
| `dotnet run`: ошибка подключения к базе | Проверьте строку подключения (`dotnet user-secrets list`), что PostgreSQL запущен и база создана |
| «relation "Users" does not exist» | Не применены миграции: `dotnet ef database update` |
| `docker compose up`: «port is already allocated» | Порт 3000 / 5097 / 5433 занят — остановите `dotnet run` / `npm run dev` или другой контейнер |
| Не входит под `admin@healthly.com` / `Admin123!` | Аккаунт был создан раньше с другим паролем (раздел 3) — на чистой базе пароль `Admin123!` |
| Телефон не подключается | `--launch-profile lan` или Docker, правило брандмауэра, одна Wi-Fi сеть, IP компьютера (не `localhost`), проверка через браузер телефона |
| Android: «CLEARTEXT communication not permitted» | `usesCleartextTraffic` в debug-манифесте или ngrok |
| `401` на каждом запросе | Заголовок ровно `Authorization: Bearer <token>`; токен истёк — обновите через `/api/auth/refresh` |
| `401` от `/api/auth/refresh` | Refresh-токен уже обменян, отозван (выход, смена пароля) или истёк — нужно войти заново |
| `403` | У роли нет прав или это данные другого пользователя (раздел 3, таблица ролей) |
| Нет `/scalar` | API запущен не в Development (`ASPNETCORE_ENVIRONMENT=Production` или `API_ENVIRONMENT` в `.env`) |
| `429 Too Many Requests` | Сработал лимит (вход/регистрация — 20 в минуту с одного IP, ИИ-анализ — 20 в час на пользователя, поиск по Healthly ID — 30 в минуту); подождите `Retry-After` секунд |
| `api` в состоянии `Restarting`, в логах `password authentication failed for user "healthly"` | Том `healthly_postgres-data` остался от старого запуска с другим паролем (пароль PostgreSQL задаётся только при создании базы). Если там только тестовые данные: `docker compose --profile tunnel down`, `docker volume rm healthly_postgres-data healthly_uploads`, `docker compose --profile tunnel up -d` — база создастся заново (**данные Docker-версии удаляются**; локальная `healthly_db` и `HealthlyApi/uploads` не затрагиваются) |
| `dotnet run` / `npm run dev`: порт занят | Работает Docker-стек демо-сервера — `docker compose --profile tunnel down` (данные сохранятся), после разработки запустите снова |
| `healthilyclub.com` не открывается | Компьютер выключен/спит, не запущен Docker или туннель (раздел 7); в Cloudflare → Tunnels статус должен быть `HEALTHY` |

---

## 7. Публичный демо-сервер (healthilyclub.com)

Сайт и API работают **на домашнем компьютере владельца проекта** и доступны из любой сети по
**https://healthilyclub.com** — для показа куратору/группе и для разработки мобильного приложения на реальном телефоне.

### Как это устроено

```
Браузер / телефон ──HTTPS──> Cloudflare ──туннель──> cloudflared (контейнер tunnel)
                                                        └─> web:3000 (Next.js) ──/api/*──> api:8080 (.NET) ──> db
```

- **Cloudflare Tunnel**: контейнер `cloudflared` сам открывает *исходящее* соединение к Cloudflare. Проброс портов на
  роутере, «белый» IP и сертификат не нужны — HTTPS выдаёт Cloudflare.
- **Один адрес для сайта и API**: сервер Next.js проксирует `/api/*` в контейнер API (`API_PROXY_TARGET` в
  `next.config.ts`), браузер ходит на `/api` того же домена. Поэтому нет CORS, и cookie с refresh-токеном
  (`SameSite=Strict`, `Secure`) работает. Мобильное приложение: базовый URL `https://healthilyclub.com`.
- **Production**: нет `/scalar` и демо-аккаунтов с известными паролями; отдельная база в Docker (не ваша локальная
  `healthly_db`). Первый админ — из `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` в `.env`, остальных (врач, сотрудник
  лаборатории) он создаёт на странице Users; пациенты регистрируются сами.
- **Лимиты** (`Services/RateLimiting.cs`): вход и регистрация — 20 в минуту с одного IP (подбор паролей), ИИ-анализ —
  20 в час на пользователя (квота Gemini/Groq), поиск по Healthly ID — 30 в минуту (перебор кодов). Превышение → `429`.

### Настройка (один раз, делает владелец проекта)

1. **Cloudflare** → <https://one.dash.cloudflare.com> → **Networks → Tunnels → Create a tunnel** → тип
   **Cloudflared** → имя `healthly-home` → на шаге установки выберите **Docker** и нажмите значок копирования у
   команды `docker run … --token eyJ…`. **Саму команду не запускайте** — туннель запускает наш `docker compose`
   (контейнер `tunnel`). Нужен только **токен** — длинная строка после `--token ` (на экране скрыта звёздочками, но
   копируется целиком). Это секрет: не отправляйте его в чат и не коммитьте.
   Надпись «Waiting for your Tunnel to connect…» — нормально, она сменится на «Connected» после шага 3.
2. Вставьте токен в `.env` в корне проекта — только токен, без `docker run` и `--token`:
   `CLOUDFLARE_TUNNEL_TOKEN=eyJ…`.
   Остальное в `.env` уже заполнено для демо (`API_ENVIRONMENT=Production`, `WEB_ORIGIN=https://healthilyclub.com`,
   `PUBLIC_API_URL=/api`, `API_PROXY_TARGET=http://api:8080`, случайные пароль БД и `JWT_KEY`, ключи ИИ, админ).
3. Остановите `dotnet run` и `npm run dev` (Docker займёт порты 3000 и 5097) и запустите:
   ```powershell
   docker compose --profile tunnel up -d --build
   ```
4. В Cloudflare в туннеле → **Add a route** → **Published application** (остальные варианты — Private hostname,
   Private CIDR, Workers VPC — для закрытых корпоративных сетей, не нужны). Поля:

   | Поле | Значение |
   |---|---|
   | Subdomain | пусто (серое `www` — подсказка, не значение; «Full hostname» должен быть `healthilyclub.com`) |
   | Domain | `healthilyclub.com` |
   | Path | пусто |
   | Service URL | **`http://web:3000`** — именно `http`, не `https` (HTTPS для посетителей даёт Cloudflare), и `web`, не `localhost` (для контейнера туннеля `localhost` — он сам, а `web` — имя контейнера сайта) |

   **Add route**. Статус туннеля → **HEALTHY**, через минуту открывайте https://healthilyclub.com.
   Для `www.healthilyclub.com` — ещё один такой же маршрут с Subdomain `www` (необязательно, сейчас не добавлен).
5. Войдите админом (`SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` из `.env`) → `/admin/laboratories` → добавить
   лабораторию → `/admin/users` → создать врача и сотрудника лаборатории.

Проверка, что всё запущено: `docker compose --profile tunnel ps` — `db` (healthy), `api`, `web`, `tunnel` в состоянии
`Up`. Строка `Restarting` у `api` — смотрите `docker compose logs api` и таблицу проблем ниже.

### Каждый день

| Действие | Команда |
|---|---|
| Запустить | `docker compose --profile tunnel up -d` |
| Обновить после `git pull` | `docker compose --profile tunnel up -d --build` |
| Логи API / туннеля | `docker compose logs -f api` / `docker compose logs -f tunnel` |
| Остановить | `docker compose --profile tunnel down` (данные сохраняются) |

Контейнеры перезапускаются сами (`restart: unless-stopped`) после перезагрузки компьютера, если Docker Desktop
стартует вместе с Windows (Settings → General → *Start Docker Desktop when you sign in*).

### Ограничения

- Сайт работает, **пока компьютер включён и не спит** (Параметры → Система → Питание → «Переводить в спящий режим» —
  «Никогда» на время показов).
- Скорость — исходящий канал домашнего интернета; одна загрузка + анализ ≈ 10–20 с.
- Cloudflare обрывает запрос через **100 с** — если ИИ-анализ дольше (оба провайдера тормозят), будет ошибка 524,
  просто повторите.
- **Только тестовые данные.** Хранение пока не зашифровано (PROJECT_GUIDE, «Отложено») — настоящие анализы
  пациентов сюда не загружать.
- Нормы — черновик (см. предупреждение в начале файла), на показе так и говорить.
