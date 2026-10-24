# Healthly — руководство для команды

Как за 10 минут запустить проект у себя, войти под нужной ролью, найти документацию API и подключить мобильное приложение.

| Документ | Для чего |
|---|---|
| **TEAM_GUIDE.md** (этот файл) | Запуск, тестовые аккаунты, документация, адреса для мобильного приложения |
| [MOBILE_HANDOFF.md](MOBILE_HANDOFF.md) | Подробный справочник API на английском: все эндпоинты с примерами JSON, ошибки, код для Flutter / React Native |
| [PROJECT_GUIDE.md](PROJECT_GUIDE.md) | Карта проекта, как устроены процессы (для защиты диплома), roadmap |
| [README_CONTRIBUTOR.md](README_CONTRIBUTOR.md) | Работа с Git: ветки, коммиты, Pull Request |

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
| **Admin** | Администратор | Всё, что врач, плюс: создать/изменить/удалить пользователя, назначить роль, задать пароль, удалить любой документ |

Сделать пользователя врачом или админом может только Admin (страница `/admin/users` → Edit → Role).

### Аккаунты

Создаются автоматически при запуске API в режиме **Development** (и в Docker, и через `dotnet run`):

| Роль | Email | Пароль | Что внутри |
|---|---|---|---|
| Patient | `patient@healthly.com` | `Patient123!` | Elif Kaya, Стамбул. 6 анализов за последние 6 месяцев (глюкоза, холестерин, гемоглобин — выходят из отклонений к норме) + 1 документ «на проверке» |
| Doctor | `doctor@healthly.com` | `Doctor123!` | Dr. Mehmet Yilmaz, Анкара. Своя история анализов, всё в норме |
| Admin | `admin@healthly.com` | `Admin123!` | Healthly Admin, Измир. Своя история анализов, холестерин растёт |

- Аккаунт создаётся, **только если такого email ещё нет**; пароль существующего аккаунта не перезаписывается.
  Например, у владельца проекта `admin@healthly.com` был создан раньше — там другой пароль.
- Если вы сменили пароль демо-аккаунта и забыли его — войдите под админом и задайте новый
  (`/admin/users` → Edit → New password) или начните с чистой базы (`docker compose down -v`).
- Даты анализов отсчитываются от первого запуска. Чтобы снова получить «последние 6 месяцев» (или вернуть
  испорченные демо-данные): один раз запустите API с флагом `DemoData:ResetHistory`:
  `DemoData__ResetHistory=true dotnet run` (bash) или `DEMO_RESET_HISTORY=true` в `.env` для Docker.
- Своих пациентов можно регистрировать сколько угодно: http://localhost:3000/register или `POST /api/auth/register`.

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
| **Телефон в любой сети** | `https://<id>.ngrok-free.app` | На компьютере с API: `ngrok http 5097`. Адрес меняется при каждом перезапуске ngrok (бесплатный тариф) |

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
