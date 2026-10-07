# Обзор технологий Баюбай

## 1. Введение

Этот документ - для владельца проекта: он объясняет, из чего состоит бэкенд Баюбай и почему выбраны именно эти технологии. Целевой читатель - .NET-разработчик, который уверенно работает с .NET и PostgreSQL, но не сталкивался с Aspire, Mailpit, Testcontainers и азурными сервисами. Документ не заменяет официальную документацию - он даёт контекст: что это, зачем нам, где лежит в репозитории, как выглядит в повседневной работе.

Правило: документ пополняется вместе с проектом. Когда в кодовую базу приходит новая технология, в том же изменении в этот файл добавляется новый раздел (см. `CLAUDE.md`, раздел "Conventions").

## 2. Карта проекта

Бэкенд - модульный монолит: один процесс, одна база данных, но код разделён на независимые модули.

```
src/
├─ Bayubai.AppHost/          оркестрация локального запуска (.NET Aspire)
├─ Bayubai.ServiceDefaults/  общие настройки: OpenTelemetry, health checks, resilience
├─ Bayubai.Api/              хост: запуск, DI, регистрация модулей, OpenAPI - без бизнес-логики
├─ Bayubai.SharedKernel/     общие примитивы: id, ошибки, часы (IClock), язык, консультантская изоляция
├─ Bayubai.MigrationService/ применяет миграции модулей (локально и при деплое)
└─ Modules/
   └─ Bayubai.Identity/      единственный модуль в этом под-проекте: пользователи, вход, роли, профили, приглашения

tests/
├─ Bayubai.SharedKernel.Tests/       модульные тесты общих примитивов
├─ Bayubai.Identity.Tests/           модульные тесты модуля Identity
├─ Bayubai.Api.IntegrationTests/     HTTP-тесты через WebApplicationFactory + настоящий PostgreSQL (Testcontainers)
└─ Bayubai.ArchitectureTests/        тесты, проверяющие границы модулей (NetArchTest)
```

Как части связаны:
- `Bayubai.Api` - это только хост. Он подключает модули (`AddIdentityModule()`) и открывает их эндпоинты (`MapIdentityEndpoints()`), но сам не содержит бизнес-логики.
- Каждый модуль в `src/Modules/` - отдельный проект. Публичным для других частей системы является только код в корневом namespace модуля (например, `Bayubai.Identity.IdentityModule`); всё остальное - `internal`. Модули не ссылаются друг на друга напрямую - это проверяется архитектурными тестами.
- `Bayubai.SharedKernel` - единственная зависимость, которую могут использовать модули; сам он ни от одного модуля не зависит.
- `Bayubai.AppHost` не содержит бизнес-логики - это программа для локального запуска и модель деплоя (см. раздел 11).
- `Bayubai.MigrationService` - отдельный процесс, который применяет миграции баз данных; API-хост миграции не запускает (раздел 6).

Фронтенд живёт в `web/` (раздел 17):

```
web/
├─ apps/client/     приложение родителя: PWA, mobile-first
├─ apps/studio/     кабинет консультанта и админка, laptop-first
└─ packages/
   ├─ api-client/   клиент API, сгенерированный из OpenAPI (orval)
   ├─ i18n/         словари RU/EN и форматирование дат
   └─ ui/           тема, компоненты, вход, профиль
tests/e2e/          сценарии Playwright (они же живая демонстрация)
```

Деплой: `infra/` - Bicep, сгенерированный из модели Aspire (`aspire publish`), коммитится и проверяется в CI; `deploy/` - скрипты разовой подготовки Azure и шагов деплоя (раздел 16). `site/` - публичная страница `bayubai.com`, её выкладывает Cloudflare Workers (раздел 16).

## 3. .NET 10 - почему именно 10

.NET 10 - LTS-версия (Long Term Support), то есть версия с длительной поддержкой Microsoft. Это важно: LTS-версии получают обновления безопасности дольше, чем обычные (STS) релизы, и на них можно спокойно строить продукт на годы вперёд, не переезжая каждый год на новую версию.

Даты, на которые опирается решение (из спецификации проекта): поддержка .NET 8 заканчивается в ноябре 2026 года, а .NET 10 поддерживается до ноября 2028 года. Поскольку проект стартует в 2026 году, брать версию, которая вот-вот перестанет получать патчи безопасности, не было смысла.

Что именно из .NET 10 мы используем:
- **Minimal API** - облегчённый способ описывать HTTP-эндпоинты без контроллеров (раздел 4).
- **Встроенная генерация документа OpenAPI** (`Microsoft.AspNetCore.OpenApi`, пакет версии `10.0.12`) - без сторонних библиотек вроде Swashbuckle (раздел 9).
- **`.slnx`** - новый, более компактный XML-формат файла решения (`Bayubai.slnx` вместо `.sln`).

Честно про ограничение, которое мы обнаружили: .NET 10 умеет валидировать Minimal API запросы "из коробки" (`AddValidation()` / атрибут `[ValidatableType]`), но эта встроенная валидация **не увидела typy запросов, объявленные в модулях** (то есть почти все наши запросы) - это было проверено вручную 24 сентября 2026 года при написании плана. Опциональный атрибут `[ValidatableType]` вдобавок помечен как experimental (предупреждение `ASP0029`) и в проверке тоже не сработал. Поэтому мы **не используем** встроенную валидацию, а вместо неё - `DataAnnotations` на типах запроса плюс общий фильтр эндпоинта (`.WithRequestValidation<T>()`, см. `CLAUDE.md`, раздел "Conventions"). Это пример решения, принятого не потому что "так модно", а потому что альтернативу проверили и она не подошла.

## 4. ASP.NET Core Minimal API и модульный монолит

**Minimal API** - способ описывать HTTP-маршруты как обычные методы (`app.MapGet(...)`, `app.MapPost(...)`) без классов-контроллеров. Каждый модуль регистрирует свою группу маршрутов с общим префиксом, например:

```csharp
var group = app.MapGroup("/api/identity").WithTags("Identity");
group.MapEmailSignIn();
group.MapProfile();
```

(см. `src/Modules/Bayubai.Identity/IdentityModule.cs`, метод `MapIdentityEndpoints`).

**Модульный монолит** - архитектурный стиль между "всё в одном большом клубке" и микросервисами: один процесс и одна база данных (что просто эксплуатировать), но код внутри жёстко разделён на модули с явными границами (что не даёт архитектуре расползтись). У нас:
- нет MediatR - вызовы между слоями внутри модуля - обычные вызовы методов, без дополнительной библиотеки-медиатора;
- модули не ссылаются друг на друга: если модулю A нужно что-то от модуля B, вызов идёт только через публичный интерфейс в корневом namespace модуля B;
- границы проверяются автоматически: `Bayubai.ArchitectureTests` использует `NetArchTest.Rules`, чтобы тест падал в CI, если кто-то по ошибке добавил ссылку на internal-класс другого модуля (раздел 14).

Такой стиль даёт монолиту дисциплину, при которой в будущем (если понадобится) модуль можно будет вынести в отдельный сервис без переписывания бизнес-логики.

## 5. PostgreSQL

PostgreSQL - open source реляционная СУБД. Мы выбрали её по нескольким причинам:
- **Open source** - без лицензионных платежей и без риска смены модели лицензирования.
- **Одна база, схема на модуль**: в этом под-проекте у модуля `Identity` своя PostgreSQL-схема и свой `DbContext` со своими миграциями; когда появятся новые модули, каждый получит свою схему в той же базе.
- **`jsonb`** - тип для хранения произвольных JSON-данных прямо в таблице с возможностью индексировать и делать запросы внутрь него. Пока не используется, но заложен на будущее - под гибкие шаблоны анкет и правил (сама структура анкет ещё не спроектирована).
- **NodaTime-плагин Npgsql** (`Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`) - без него PostgreSQL не умел бы напрямую сохранять типы NodaTime (`Instant`, `LocalDateTime` и т.д.), пришлось бы вручную конвертировать в `DateTime` и обратно (раздел 7).
- **Managed-вариант в Azure**: в проде используется Azure Database for PostgreSQL Flexible Server - управляемая версия той же PostgreSQL, без необходимости самим администрировать сервер (раздел 16).
- **Переносимость**: так как всё работает в контейнерах, при необходимости (например, если проблемы с 152-ФЗ или с доступностью Azure из России) базу и всё окружение можно перенести на VPS в РФ - это будет передеплой, а не переписывание кода.

## 6. EF Core + Npgsql, миграции

**EF Core** (Entity Framework Core) - ORM (Object-Relational Mapper) от Microsoft: он переводит операции с C#-объектами в SQL-запросы к базе. **Npgsql** - провайдер EF Core для PostgreSQL (без него EF Core не умеет с ней работать).

Как добавить миграцию для модуля Identity (команда из `CLAUDE.md`):

```bash
dotnet ef migrations add <Name> --project src/Modules/Bayubai.Identity --output-dir Persistence/Migrations --namespace Bayubai.Identity.Persistence.Migrations
```

**Почему миграции не запускаются при старте API.** Если приложение само накатывает миграции при запуске, это опасно при масштабировании (несколько экземпляров API могут попытаться мигрировать базу одновременно) и не даёт контролируемо откатить или отследить момент применения миграции в проде. Поэтому у нас есть отдельный процесс - **`Bayubai.MigrationService`** (`src/Bayubai.MigrationService`), который явно вызывает `MigrateIdentityDatabaseAsync` и применяет миграции до того, как поднимется API. Локально `Bayubai.AppHost` запускает его и ждёт завершения (`WaitForCompletion(migrations)`, см. `src/Bayubai.AppHost/AppHost.cs`) перед стартом `Bayubai.Api`; при деплое в Azure это задание (job) Container Apps `migrations`, которое workflow деплоя запускает и дожидается (раздел 16).

## 7. NodaTime

**NodaTime** - альтернативная библиотека даты и времени для .NET, созданная потому что встроенные `DateTime` и `DateTimeOffset` исторически путают "момент времени", "локальное время" и "часовой пояс" и легко приводят к багам (особенно с летним/зимним временем и разными таймзонами пользователей).

У нас: `DateTime` не используется ни в доменном, ни в persistence-коде (кроме сгенерированных EF-миграций - это исключение зафиксировано в плане). Вместо этого:
- **`Instant`** - точный момент времени в UTC, без привязки к часовому поясу - для меток "когда это произошло" (создание приглашения, отправка письма и т.д.);
- **часовой пояс пользователя** хранится как IANA id (например, `Europe/Moscow`), а не как смещение - потому что смещение может измениться из-за перехода на летнее время, а имя зоны - нет;
- **`LocalDateTime` + зона** - когда нужно локальное "настенное" время человека;
- **`IClock`** - абстракция над "текущим временем", которая внедряется через DI. В продакшене это `SystemClock.Instance` (см. `src/Bayubai.Api/Program.cs`), а в тестах - `FakeClock` из `NodaTime.Testing`, что позволяет тестам управлять временем напрямую, не дожидаясь реальных минут и не гоняясь за `DateTime.Now` в моках.

## 8. ASP.NET Core Identity без паролей

Аутентификация построена на **ASP.NET Core Identity** - стандартной библиотеке Microsoft для управления пользователями, ролями и входом в систему - но без паролей вообще. Управляемые решения (Azure AD B2C, Entra External ID) не подошли: B2C закрыт для новых клиентов, а Entra External ID не поддерживает Telegram и не имеет готовой интеграции с VK или Yandex.

Способы входа:
- **Google** - через OpenID Connect (OIDC).
- **Yandex ID** - через OAuth 2.0 (пакет `AspNet.Security.OAuth.Yandex`).
- **VK ID** - через OAuth 2.1 с PKCE (Proof Key for Code Exchange - защита кода авторизации от перехвата; пакет `AspNet.Security.OAuth.VkId`).
- **Telegram** - через Login Widget, с проверкой подписи HMAC секретом бота.
- **Email** - magic link (одноразовая ссылка для входа), живёт 15 минут, одноразовая, отправляется по SMTP через Brevo в проде и через Mailpit локально (раздел 12).

Сессии - **cookie**, а не токены в JavaScript: `HttpOnly` (недоступна из JS - защита от XSS), `Secure` (только по HTTPS), `SameSite=Lax` (базовая защита от CSRF). Настройка cookie - в `IdentityModule.ConfigureSessionCookie` (`src/Modules/Bayubai.Identity/IdentityModule.cs`). Такой подход требует, чтобы приложения и API были на одном регистрируемом домене (`app.`, `studio.`, `api.` - поддомены одного домена), иначе браузер cookie между ними не пропустит.

Почему аккаунты не склеиваются по email: у Telegram email вообще нет, а автоматическое объединение аккаунтов по непроверенному email открывает путь к захвату чужого аккаунта (кто-то регистрируется на чужой email раньше настоящего владельца). Поэтому привязка второго способа входа возможна только вручную, пока пользователь уже вошёл в систему.

## 9. OpenAPI

**OpenAPI** - стандарт описания HTTP API в машиночитаемом формате (какие есть эндпоинты, какие у них параметры, какие ответы). У нас документ генерируется прямо из кода (`builder.Services.AddOpenApi(...)` и `app.MapOpenApi()` в `src/Bayubai.Api/Program.cs`) и доступен по адресу `/openapi/v1.json`. Это единый источник правды о контракте API для всех клиентов.

В плане 2 из этого документа будет автоматически сгенерирован TypeScript-клиент для фронтенда с помощью инструмента **orval** - то есть фронтенд не будет писать HTTP-запросы руками, а получит готовые типизированные хуки.

## 10. Scalar

**Scalar** - UI для просмотра OpenAPI-документа и ручных запросов к API: интерактивная страница со списком эндпоинтов, схемами запросов и ответов и кнопкой "Try it" прямо в браузере, без отдельного инструмента вроде Postman. Пакет `Scalar.AspNetCore` (`app.MapScalarApiReference()` в `src/Bayubai.Api/Program.cs`) строит эту страницу поверх уже имеющегося документа `/openapi/v1.json` (раздел 9).

Страница подключена только для `Development` (`app.Environment.IsDevelopment()`) - в тестовом окружении и в проде её нет, это инструмент локальной разработки и демонстраций, а не часть публичного API. Обслуживается с того же адреса, что и сам API, а не с отдельного origin, поэтому кнопка "Try it" отправляет запросы с той же cookie-сессией, что уже есть в браузере.

Адрес: `{адрес api}/scalar`. Под Aspire (`dotnet run --project src/Bayubai.AppHost`) адрес API смотрите в дашборде. При отдельном запуске API (`dotnet run --project src/Bayubai.Api`) без `--launch-profile` используется первый профиль из `launchSettings.json` - `http`, `http://localhost:5042` - а сессионная cookie помечена `Secure` и не отправляется по http, поэтому для авторизованных запросов запускайте `dotnet run --project src/Bayubai.Api --launch-profile https`, тогда Scalar будет на `https://localhost:7136/scalar`.

Официальная документация: https://scalar.com/products/api-references/integrations/aspnetcore/integration

### Как запустить локально и показать

Что нужно один раз перед первым запуском:
- Docker Desktop (или аналог) запущен - без него Aspire не поднимет PostgreSQL и Mailpit (раздел 13).
- Установлен .NET SDK версии, закреплённой в `global.json` (сейчас 10.0.204).
- Локальный сертификат разработки одобрен: `dotnet dev-certs https --trust`.
- Node.js 22.18+ и pnpm 10.34.5 (`npm install -g pnpm@10.34.5`) - для веб-приложений.

Дальше:
1. Администратор для демонстрации уже есть: `admin@bayubai.local` (письмо со ссылкой приходит в Mailpit). Свой email можно добавить так: `dotnet user-secrets --project src/Bayubai.Api set "Identity:AdminEmails:0" "<ваш email>"`.
2. Запустите весь стек: `dotnet run --project src/Bayubai.AppHost`.
3. В консоли появится ссылка на Aspire-дашборд с одноразовым токеном входа - откройте её в браузере.
4. В дашборде найдите ресурс `api` и откройте его адрес - это и есть `{адрес api}` выше; `{адрес api}/scalar` откроет Scalar, а `{адрес api}/openapi/v1.json` - сырой OpenAPI-документ.
5. Там же найдите ресурс `email` (Mailpit) и откройте его веб-интерфейс - туда приходят magic-link письма вместо реального почтового ящика (раздел 12).
6. Для готового пошагового сценария вместо ручного набора запросов используйте `src/Bayubai.Api/Bayubai.Api.http` (Visual Studio, Rider или расширение REST Client в VS Code): вход администратора, создание консультанта, приглашение и вход родителя, обновление и удаление профиля.
7. Родительское приложение - http://localhost:5173, кабинет консультанта и админка - http://localhost:5174. Кнопка "Войти через тестовый вход" входит без почты (аккаунт выбирается cookie `bb_fake_subject`, по умолчанию `fake-user`).
8. Готовый сценарий показа в браузере: при запущенном AppHost выполните в `tests/e2e/` команду `pnpm walkthrough` - Playwright откроет видимый браузер и медленно пройдёт вход, приглашение и принятие (раздел 14).

Учтите: на `localhost` cookie не различают порты, поэтому в одном браузере вход в `studio` означает вход и в `client`. Для показа "консультант и родитель одновременно" используйте разные браузеры или окно инкогнито.

OAuth-провайдеры (Google, Yandex ID, VK ID) и Telegram по умолчанию не настроены - без собственных ключей в user-secrets соответствующий способ входа просто не появляется в ответе `/api/identity/providers`. Чтобы включить их локально:

```bash
dotnet user-secrets --project src/Bayubai.Api set "Identity:Providers:<Name>:ClientId" "<client id>"
dotnet user-secrets --project src/Bayubai.Api set "Identity:Providers:<Name>:ClientSecret" "<client secret>"
dotnet user-secrets --project src/Bayubai.Api set "Identity:TelegramBotToken" "<токен бота>"
dotnet user-secrets --project src/Bayubai.Api set "Identity:TelegramBotName" "<имя бота>"
```

где `<Name>` - `Google`, `Yandex` или `VkId` (см. `src/Modules/Bayubai.Identity/External/ExternalProviders.cs`).

Redirect URI (callback), который нужно зарегистрировать в консоли провайдера: `/api/identity/signin-<provider>` в нижнем регистре (`signin-google`, `signin-yandex`, `signin-vkid`). Локально фронтенд ходит к API через прокси Vite, поэтому с точки зрения браузера и провайдера хост - это хост самого приложения, а не API: чтобы настоящие Google/Yandex/VK ID реально сработали локально, в консоли провайдера нужно зарегистрировать `http://localhost:5173/api/identity/signin-<provider>` (родительское приложение) и `http://localhost:5174/api/identity/signin-<provider>` (кабинет консультанта). В продакшене регистрируется один адрес - origin самого API (`https://api.<domain>/api/identity/signin-<provider>`, раздел "Notes for plan 3" в плане).

## 11. .NET Aspire

**.NET Aspire** - набор инструментов Microsoft для локальной разработки распределённых приложений: он поднимает связанные сервисы (базу, очереди, другие процессы) одной командой, настраивает между ними service discovery, прокидывает переменные окружения и даёт единый дашборд с логами и трассировками.

`src/Bayubai.AppHost/AppHost.cs` выбирает между локальной и азурной моделью по `builder.ExecutionContext.IsPublishMode`:

```csharp
if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureDeployment();
}
else
{
    builder.AddLocalStack();
}
```

Локальная модель (`src/Bayubai.AppHost/LocalStack.cs`) описывает окружение так:

```csharp
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase(DatabaseAccess.Database);
var appRolePassword = builder.AddParameter(
    "postgres-app-password", new GenerateParameterDefault { MinLength = 22, Special = false }, secret: true, persist: true);
// Fixed ports so Playwright can read the inbox at a known address.
var email = builder.AddMailPit("email", httpPort: 8025, smtpPort: 1025);

var migrations = builder.AddProject<Projects.Bayubai_MigrationService>("migrations")
    .WithReference(database)
    .WithAppRoleSetup(appRolePassword)
    .WaitFor(database);

// Local demo and e2e only: a one-click test sign-in and a known admin; index 99 leaves user-secrets admins at 0 untouched.
var api = builder.AddProject<Projects.Bayubai_Api>("api")
    .WithEnvironment(
        $"ConnectionStrings__{DatabaseAccess.Database}",
        ReferenceExpression.Create(
            $"Host={postgres.Resource.Host};Port={postgres.Resource.Port};Database={DatabaseAccess.Database};Username={DatabaseAccess.AppRole};Password={appRolePassword}"))
    .WithReference(email)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithEnvironment("Identity__Providers__Fake__Enabled", "true")
    .WithEnvironment("Identity__AdminEmails__99", "admin@bayubai.local");

// Ports match Frontend:Origins in the API's appsettings.Development.json.
var client = builder.AddViteApp("client", "../../web/apps/client")
    .WithPnpm()
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WaitFor(api);

// The client's installer already installed the whole pnpm workspace.
builder.AddViteApp("studio", "../../web/apps/studio")
    .WithPnpm(install: false)
    .WithEndpoint("http", endpoint => endpoint.Port = 5174)
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WaitFor(client);
```

То есть при запуске поднимаются: контейнеры PostgreSQL и Mailpit (у Mailpit фиксированные порты: интерфейс и API на http://localhost:8025), процесс `Bayubai.MigrationService`, затем `Bayubai.Api`, затем оба веб-приложения: `client` на http://localhost:5173 и `studio` на http://localhost:5174 (пакет `Aspire.Hosting.JavaScript`: он сам выполняет `pnpm install` и запускает `vite`). Тестовый способ входа "тестовый вход" и `admin@bayubai.local` как администратор - часть только этой, локальной модели; при деплое в Azure используется другая модель, `AddAzureDeployment()` (см. «Публикация в Azure» ниже).

Запуск:

```bash
dotnet run --project src/Bayubai.AppHost
```

После запуска открывается **Aspire-дашборд** в браузере - там видно список всех запущенных ресурсов, их логи в реальном времени, распределённые трассировки запросов (через OpenTelemetry - см. `Bayubai.ServiceDefaults`) и метрики.

Чем это удобнее docker-compose: docker-compose описывает только контейнеры и их сети, а Aspire ещё и умеет управлять процессами .NET напрямую (без обёртывания в Docker), автоматически прокидывает connection string'и и адреса сервисов друг другу через переменные окружения, и даёт единый экран для логов/трейсов сразу для контейнеров и .NET-процессов вместе - не нужно параллельно смотреть `docker logs` и консоль `dotnet run`.

### Публикация в Azure

У AppHost две модели. При `dotnet run` работает локальная (`LocalStack.cs`): контейнеры, Mailpit, Vite. При `aspire publish` и `aspire deploy` - азурная (`AzureDeployment.cs`): Container Apps, PostgreSQL Flexible Server, Key Vault, Application Insights и два Static Web Apps (раздел 16). `aspire publish` превращает модель в Bicep (декларативный язык описания ресурсов Azure) в папке `infra/`; этот вывод коммитится, чтобы изменение инфраструктуры было видно в pull request, а CI проверяет, что `infra/` совпадает с моделью. Настройки, от которых зависит модель, лежат в `src/Bayubai.AppHost/appsettings.json` (раздел `Deploy`), а не в переменных окружения, поэтому `infra/` определяется только закоммиченными файлами.

Aspire CLI закреплён как локальный инструмент (`.config/dotnet-tools.json`): `dotnet tool restore`, затем `dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra`.

Официальная документация: https://aspire.dev/deployment/azure/

## 12. Mailpit

**Mailpit** - фейковый SMTP-сервер с веб-интерфейсом для разработки: приложение отправляет письмо на него как на настоящий SMTP, но письмо никуда за пределы машины не уходит - оно оседает в Mailpit, и его можно посмотреть в браузере.

У нас это значит: письма с magic link при локальной разработке не отправляются реальным получателям - они видны в веб-интерфейсе Mailpit (по умолчанию поднимается Aspire'ом вместе с остальным стеком, см. раздел 11). Это удобно для разработки и e2e-тестов - не нужен реальный email-провайдер и не нужно проверять реальный почтовый ящик.

В продакшене вместо Mailpit письма отправляет **Brevo** - сервис рассылок (французская компания, данные в ЕС) через свой SMTP-relay `smtp-relay.brevo.com:587` с STARTTLS; бесплатный тариф - 300 писем в день. Код тот же самый `SmtpEmailSender`, меняются только настройки `Email:*`, поэтому другой провайдер - это смена конфигурации. Отправитель во всех письмах - `Баюбай <no-reply@<домен>>` (`Email:From`, в азурной модели `src/Bayubai.AppHost/AzureDeployment.cs`). Azure Communication Services Email, который был в спецификации, не взяли: Microsoft выводит его из эксплуатации 30 сентября 2028 года. Чтобы письма не попадали в спам, домен отправителя подтверждается в Brevo DNS-записями (DKIM, DMARC).

Официальная документация: https://developers.brevo.com/docs/smtp-integration

## 13. Docker

Docker - платформа для запуска приложений в изолированных контейнерах. В этом проекте Docker не запускает саму продакшен-нагрузку локально, но нужен для двух вещей:
- **.NET Aspire** поднимает PostgreSQL и Mailpit как Docker-контейнеры (раздел 11) - без установленного и запущенного Docker Desktop (или аналога) `dotnet run --project src/Bayubai.AppHost` не сможет их создать;
- **Testcontainers** в интеграционных тестах поднимает настоящий PostgreSQL в контейнере на время тестового прогона (раздел 14) - `dotnet test Bayubai.slnx` тоже требует, чтобы Docker был запущен (это явно указано в `CLAUDE.md`).

## 14. Тесты

- **xUnit v3** - фреймворк для unit- и интеграционных тестов в .NET (используется версия `xunit.v3`).
- **Shouldly** - библиотека для более читаемых assert'ов: вместо `Assert.Equal(expected, actual)` пишется `actual.ShouldBe(expected)`, а при падении тест выводит понятное сообщение об ошибке.
- **Testcontainers** (`Testcontainers.PostgreSql`) - для `Bayubai.Api.IntegrationTests`: вместо мока базы данных или SQLite поднимается настоящий PostgreSQL в Docker-контейнере на время теста, так тесты проверяют поведение на той же СУБД, что и в проде (включая NodaTime-типы, `jsonb` и специфичные для PostgreSQL детали).
- **NetArchTest** (`NetArchTest.Rules`) - библиотека для тестов, которые проверяют не поведение кода, а его структуру: например, "ни один класс из модуля Identity, кроме публичного API, не должен быть виден снаружи" (`Bayubai.ArchitectureTests`).
- **FakeClock** (`NodaTime.Testing`) - подменяет `IClock` в тестах, чтобы управлять "текущим временем" напрямую (раздел 7).
- **Playwright** (`tests/e2e/`) - сквозные тесты в настоящем браузере против всего стека, поднятого Aspire: вход по ссылке из письма (письмо читается из Mailpit через его HTTP API), профиль и мгновенная смена языка, тема, консультант приглашает родителя из другого часового пояса и оба видят местное время друг друга, повторное приглашение, второй способ входа через тестовый провайдер. Родительские страницы открываются в размере телефона. `pnpm test` - без окна (так же в CI), `pnpm walkthrough` - в видимом браузере с паузами, как живая демонстрация. Если AppHost не запущен, Playwright запускает его сам.
- **how-to-test** - сценарии Playwright к конкретной задаче: `tests/e2e/how-to-test/bb-<номер>/`, запуск `pnpm how-to-test bb-<номер>` из `tests/e2e/` в видимом браузере. Их пишет Claude по навыку `.claude/skills/how-to-test`, чтобы изменение можно было увидеть своими глазами; навык `.claude/skills/build-test` выбирает, какие проверки запускать для изменённых файлов, а `REVIEW.md` - чек-лист ревью каждого pull request.

Команды (из `CLAUDE.md`):

```bash
dotnet build Bayubai.slnx
dotnet test Bayubai.slnx                       # нужен запущенный Docker
dotnet test tests/Bayubai.Identity.Tests       # один проект
```

## 15. CI

GitHub Actions workflow `.github/workflows/backend.yml` запускается на каждый pull request и на push в `main`. Что он делает:
1. Скачивает код (`actions/checkout`).
2. Ставит .NET SDK ровно той версии, что закреплена в `global.json` (`actions/setup-dotnet` с `global-json-file: global.json`) - то есть в CI используется та же версия SDK, что и локально.
3. `dotnet restore Bayubai.slnx` - восстанавливает NuGet-пакеты.
4. `dotnet build Bayubai.slnx --no-restore --configuration Release` - собирает решение в конфигурации Release.
5. `dotnet test Bayubai.slnx --no-build --configuration Release` - прогоняет все тесты решения (unit, интеграционные через Testcontainers и архитектурные).

В backend-workflow есть ещё шаг "Committed infra matches the Azure model": он заново генерирует `infra/` из модели Aspire и падает, если результат отличается от закоммиченного (раздел 11).

Ещё четыре workflow:
- `.github/workflows/frontend.yml` - в `web/`: `pnpm install --frozen-lockfile`, линтер, проверка типов, тесты Vitest (включая проверку одинаковых ключей RU/EN и перевода каждого кода ошибки), сборка обоих приложений, проверка, что сборка не изменила закоммиченные `routeTree.gen.ts` (их генерирует плагин TanStack Router при сборке - расхождение означает, что дерево маршрутов забыли перегенерировать и закоммитить), и проверка, что сгенерированный клиент API совпадает с `openapi.json`. Вместе с тестом `OpenApiContractTests` в backend-workflow это даёт цепочку "код API -> openapi.json -> клиент".
- `.github/workflows/e2e.yml` - ставит .NET, Node, pnpm и Chromium, доверяет dev-сертификату и запускает сценарии Playwright; Playwright сам поднимает весь стек через Aspire AppHost (Docker на раннерах GitHub есть). При падении отчёт Playwright прикладывается к запуску.
- `.github/workflows/hygiene.yml` - две проверки гигиены. **gitleaks** ищет в файлах и во всей истории git то, что похоже на секреты (ключи, токены, пароли). Проверка запрещённых имён ищет в файлах, в сообщениях коммитов и в именах авторов имена, которых не должно быть в публичном репозитории; сам список лежит в секрете репозитория `FORBIDDEN_REFERENCES`, а в лог попадают только имена файлов и число совпадений, чтобы список не утёк через лог.
- `.github/workflows/deploy.yml` - деплой в Azure после каждого merge в `main` (раздел 16); включается переменной репозитория `DEPLOY_ENABLED`.

Кроме workflow:
- **CodeQL** - статический анализ кода на уязвимости от GitHub (C#, TypeScript, сами workflow). Включён как "default setup" в настройках репозитория, отдельного файла нет; находки видны во вкладке Security.
- **Dependabot** (`.github/dependabot.yml`) - раз в неделю открывает pull request'ы с обновлениями NuGet-, npm-пакетов и GitHub Actions, сгруппированные по экосистеме. Все GitHub Actions в workflow закреплены по полному SHA коммита с версией в комментарии (`@<sha> # v4.4.0`): перемещённый или взломанный тег не изменит то, что запускается в CI и в деплое; Dependabot обновляет SHA и комментарий вместе.
- **Push protection** - GitHub отклоняет push, в котором распознал секрет, ещё до того, как он попадёт в репозиторий.

`main` защищён ruleset'ом `main`: изменения попадают туда только через pull request, обязательные проверки - `build-and-test`, `checks`, `smoke`, `secrets-scan`, `forbidden-references` (ветка PR должна быть актуальной относительно `main`); прямой push, force push и удаление ветки запрещены.

Официальная документация: https://github.com/gitleaks/gitleaks, https://docs.github.com/en/code-security/code-scanning/enabling-code-scanning/configuring-default-setup-for-code-scanning, https://docs.github.com/en/code-security/dependabot, https://docs.github.com/en/code-security/secret-scanning/push-protection-for-repositories-and-organizations
YouTube (EN): `gitleaks GitHub Actions`, `GitHub CodeQL default setup`, `Dependabot tutorial`

## 16. Azure и деплой

Продакшен работает в Azure, регион West Europe, в группе ресурсов `rg-bayubai`. Всё описано кодом: модель ресурсов - `src/Bayubai.AppHost/AzureDeployment.cs`, сгенерированный из неё Bicep - `infra/` (раздел 11), деплой - `.github/workflows/deploy.yml`, разовая подготовка и эксплуатация - `deploy/bootstrap.sh` и `deploy/README.md`.

Ресурсы:
- **Azure Container Apps** - управляемый запуск контейнеров без администрирования виртуальных машин. Здесь живут API (одна всегда тёплая реплика, максимум две) и задание (job) `migrations`, которое применяет миграции базы.
- **Azure Container Registry** - хранилище Docker-образов, которые собирает деплой.
- **Azure Database for PostgreSQL Flexible Server** (Burstable B1ms, 32 ГБ, бэкапы 7 дней) - управляемый PostgreSQL; вход по паролю, чтобы приложению не нужен был Azure SDK.
- **Azure Static Web Apps** (бесплатный тариф) - `bb-client` и `bb-studio`, статические сборки двух приложений, с бесплатными сертификатами для `app.` и `studio.`.
- **Key Vault** (`kv-bayubai-<6 hex>`, имя печатает `bootstrap.sh`) - хранилище секретов: пароли базы (администратора и роли приложения), email администратора, логин и ключ SMTP, ключи OAuth-провайдеров, токен Telegram-бота.
- **Application Insights + Log Analytics** - логи, метрики и трассировки, которые локально видны в Aspire-дашборде (раздел 11).
- **Бюджет** 40 USD в месяц с письмами при 80% и 100% (создаётся один раз скриптом `deploy/bootstrap.sh`). Оценка расходов - около 31 USD в месяц: PostgreSQL ~19, Container Registry ~5, тёплая реплика API ~6, остальное почти бесплатно.

**Как секреты попадают в приложение.** Секреты приложения (email администратора, SMTP, OAuth-провайдеры, Telegram) Container Apps хранит не значениями, а ссылками на секреты Key Vault (Key Vault references) и читает их управляемым удостоверением (managed identity) приложения. Исключение - строка подключения к базе `ConnectionStrings__bayubai`: модель собирает её сама, а деплой записывает её значение, включая пароль, прямо в секреты Container Apps (`infra/api/api.bicep`, `infra/migrations/migrations.bicep`). Приложение получает всё это как обычные переменные окружения (`Email__Password`, `ConnectionStrings__bayubai` и т.д.) и ничего не знает про Key Vault. В коде нет ни клиента Key Vault, ни другого Azure SDK (архитектурный тест, раздел 18), поэтому переезд на другой хостинг - это новая инфраструктура, а не переписывание кода. Секреты кладёт в Key Vault владелец (`bootstrap.sh` спрашивает их без вывода на экран); в репозитории и в GitHub их нет.

**Роли в базе.** Задание `migrations` входит администратором сервера: применяет миграции и создаёт (или обновляет) роль `bayubai_app` с правами только на чтение и запись строк в схемах модулей (`PostgresAccess` в `Bayubai.SharedKernel`). API входит как `bayubai_app` и пароля администратора не знает, поэтому ошибка в API не может изменить или удалить схему. Локальный AppHost и интеграционные тесты запускают API так же, поэтому забытый grant ломает тесты, а не прод. Пароль роли - отдельный секрет `postgres-app-password`. Обе строки подключения в проде используют `SSL Mode=VerifyFull`: Npgsql проверяет, что сертификат сервера выпущен доверенным центром и выдан на это имя хоста.

**Как GitHub попадает в Azure.** Через OIDC (federated credentials): GitHub Actions получает короткоживущий токен, которому Azure доверяет для окружения `production` этого репозитория. Паролей и ключей Azure в GitHub нет. Роль "Role Based Access Control Administrator" у удостоверения деплоя ограничена условием (constrained delegation): оно может выдавать и снимать только роли Key Vault Secrets User и AcrPull, которые шаблон назначает приложениям.

**`aspire deploy`.** Команда Aspire CLI, которая собирает образы, пушит их в Container Registry и применяет Bicep. Раньше для этого использовали azd (Azure Developer CLI); начиная с Aspire 13 рекомендуемый путь - `aspire deploy`, azd поддерживается только для существующих проектов. После выкладки workflow запускает задание `migrations` и ждёт его, затем выкладывает оба фронтенда. Фронтенды собираются заранее в отдельном job `build-web`: `pnpm install` и `pnpm build` выполняют чужой код (npm-пакеты), поэтому у этого job нет права `id-token` (он не может войти в Azure) и нет пароля базы; готовые `dist` он передаёт job `production` как артефакты. Пароли базы в job `production` видит только шаг `aspire deploy` (через выход шага, а не `GITHUB_ENV`). Миграции идут после новой версии API, поэтому они обязаны быть обратно совместимыми (`REVIEW.md`).

**Домен.** Собственный домен с поддоменами `app.`, `studio.` и `api.`. Cookie сессии ставит `api.<домен>`, и браузер отправляет её на запросы с `app.<домен>`, потому что это один сайт (same-site). Стандартные адреса Azure (`*.azurestaticapps.net`, `*.azurecontainerapps.io`) - это другие сайты, на них вход не работает; поэтому превью-окружения Static Web Apps для pull request не используются.

**Cloudflare Workers (`bayubai.com`).** Корневой домен показывает страницу-заглушку из `site/`: что такое Баюбай и контакт, на русском и английском. Её отдаёт Cloudflare Worker `bayubai-site` со статическими файлами (static assets) - бесплатный хостинг, который Cloudflare сейчас рекомендует вместо Pages; домен и DNS и так в Cloudflare, поэтому сертификат и запись для `bayubai.com` он делает сам. Worker подключён к репозиторию (Workers Builds): каждый мердж в `main` выполняет `wrangler deploy --assets=./site` без сборки, отдельного workflow в GitHub нет. Заглушка нужна, пока нет настоящего публичного лендинга (например, Brevo проверяет сайт отправителя); лендинг потом заменит её в том же месте.

Регион - EU; вопрос 152-ФЗ остаётся открытым, владелец решил деплоить в Azure, сохраняя возможность переезда (раздел 5).

## 17. Фронтенд

Два приложения и три общих пакета в одном pnpm workspace (`web/`). Команды запускаются из `web/`: `pnpm install`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`.

### 17.1 pnpm workspace и TypeScript

**pnpm** - пакетный менеджер для Node.js. Workspace - это несколько пакетов в одном репозитории с общим `pnpm-lock.yaml`: приложения подключают общие пакеты как `"@bayubai/ui": "workspace:*"`, без публикации в npm. Версия pnpm закреплена в `web/package.json` (`packageManager`), версии всех зависимостей - точные.

**TypeScript** - JavaScript с типами; общие настройки компилятора в `web/tsconfig.base.json` (`strict`). TypeScript держим на 6.0: линтер typescript-eslint пока не поддерживает TypeScript 7.

Официальная документация: https://pnpm.io/workspaces, https://www.typescriptlang.org/docs/
YouTube (EN): `pnpm workspaces monorepo tutorial`

### 17.2 orval: клиент API из OpenAPI

**orval** читает OpenAPI-документ и генерирует TypeScript-типы и хуки TanStack Query (`useGetMe`, `useStartEmailSignIn`...), так что фронтенд не пишет HTTP-запросы руками. Цепочка контракта:
1. интеграционный тест `OpenApiContractTests` сравнивает документ, который отдаёт API, с закоммиченным `web/packages/api-client/openapi.json` (после намеренного изменения API: `BAYUBAI_UPDATE_OPENAPI=1 dotnet test tests/Bayubai.Api.IntegrationTests`);
2. `pnpm generate:api` генерирует `web/packages/api-client/src/generated/` из этого файла; результат коммитится, CI проверяет, что он не устарел.

Имена хуков берутся из `.WithName(...)` у эндпоинтов, поэтому у каждого эндпоинта должно быть имя. Ошибки приходят как `ApiProblem` с кодом (`identity.invite_expired`), который UI переводит.

Официальная документация: https://orval.dev/
YouTube (EN): `orval openapi react query`

### 17.3 ESLint и Vitest

**ESLint** проверяет код на ошибки и опасные паттерны; конфигурация одна на весь workspace (`web/eslint.config.js`), включая правила хуков React. **Vitest** - тестовый раннер, совместимый с Vite; тесты лежат рядом с кодом (`*.test.ts(x)`).

Официальная документация: https://eslint.org/docs/latest/, https://vitest.dev/guide/
YouTube (EN): `Vitest tutorial`

### 17.4 i18next: RU и EN

**i18next** (с **react-i18next**) хранит тексты интерфейса в словарях `web/packages/i18n/src/locales/{ru,en}/<раздел>.json`. В коде нет ни одной строки интерфейса - только ключи вида `t('auth:email.submit')`. Ключи плоские; у русского три формы множественного числа (`_one`, `_few`, `_many`), они выбираются через `Intl.PluralRules`. Тест проверяет, что у RU и EN одинаковые ключи и что каждый код ошибки API (`ErrorCode` из OpenAPI) переведён.

Язык до входа берётся из браузера, после входа - из профиля; смена языка в профиле применяется сразу. Даты и время форматируются через `Intl` в часовом поясе того, кто смотрит; "местное время" другого человека (консультанта или клиента) - в его часовом поясе с подписью пояса.

Официальная документация: https://www.i18next.com/, https://react.i18next.com/
YouTube (RU): `i18next react локализация`

### 17.5 Tailwind CSS, Radix, тема

**Tailwind CSS** (v4) - CSS через классы прямо в разметке (`rounded-full px-5`). Цвета заданы токенами в `web/packages/ui/src/styles.css`: палитра по умолчанию взята с сайта консультанта-пилота (коралловый акцент, персиковый фон), но это только значения переменных, так что другой консультант может получить свою тему. **Radix** даёт доступные примитивы (метка поля, `Slot` для кнопки-ссылки), **lucide-react** - иконки. Компоненты написаны в стиле **shadcn/ui**: это не библиотека, а исходники в нашем пакете `ui`, которые мы правим сами.

Тема: "как в системе" (по умолчанию), светлая или тёмная - переключатель в шапке обоих приложений. Выбор хранится на устройстве (`localStorage`, ключ `bb.theme`) и применяется скриптом в `index.html` ещё до отрисовки, чтобы ночью не мигал белый экран.

### 17.6 MSW: фейковый API в компонентных тестах

**MSW** (Mock Service Worker) перехватывает `fetch` в тестах и отвечает как API: тест говорит "на `POST /api/identity/email/start` ответь 202" и проверяет, что компонент отправил и показал. Общий набор для тестов - `@bayubai/ui/testing`.

Официальная документация: https://tailwindcss.com/docs, https://www.radix-ui.com/primitives, https://ui.shadcn.com/, https://mswjs.io/docs/
YouTube (EN): `Tailwind CSS v4 crash course`, `shadcn ui tutorial`, `MSW mock service worker tutorial`

### 17.7 Vite, React, TanStack Router и Query

**Vite** - dev-сервер и сборщик: мгновенно перезагружает изменения, собирает продакшен-бандл. В разработке Vite проксирует `/api` на API, поэтому для браузера это один адрес и сессионная cookie остаётся "своей"; в проде адрес API задаётся переменной `VITE_API_BASE_URL`. **React** - библиотека интерфейса. **TanStack Router** - типизированная маршрутизация: страница = файл в `src/routes/` (`invite.$token.tsx` - это `/invite/:token`), защищённые страницы лежат под `_authed` и без сессии уводят на `/sign-in?next=...`. **TanStack Query** кэширует ответы API; хуки для него генерирует orval (раздел 17.2).

### 17.8 PWA

Приложение родителя - **PWA** (`vite-plugin-pwa`): у него есть манифест и иконки, его можно установить на телефон как приложение. Service worker кэширует только оболочку приложения, запросы к API всегда идут в сеть. Иконки генерируются при сборке из `public/icon.svg`.

Официальная документация: https://vite.dev/guide/, https://react.dev/, https://tanstack.com/router/latest/docs, https://tanstack.com/query/latest/docs, https://vite-pwa-org.netlify.app/guide/
YouTube (EN): `TanStack Router tutorial`, `TanStack Query v5 tutorial`, `vite-plugin-pwa tutorial`

## 18. API в продакшене

Несколько настроек, без которых API работает локально, но ломается за балансировщиком или при перезапуске. Они не зависят от Azure: на любом хостинге за TLS-прокси работают так же.

- **Forwarded headers.** В Azure Container Apps HTTPS заканчивается на входном прокси (ingress), а в контейнер запрос приходит по обычному http. Без `UseForwardedHeaders` API считал бы, что запрос пришёл по http, и, например, отдавал бы Google адрес возврата `http://...`, который провайдер отвергает. Прокси сообщает исходную схему и адрес клиента в заголовках `X-Forwarded-Proto` и `X-Forwarded-For`; `src/Bayubai.Api/Program.cs` им доверяет, потому что снаружи к контейнеру можно попасть только через ingress. Заголовок `Host` от прокси не принимается, чтобы его нельзя было подменить. Middleware должен отработать ровно один раз: каждый проход снимает одну, самую правую, запись `X-Forwarded-For`. Ingress дописывает настоящий адрес клиента справа, всё левее пишет сам клиент. В Azure Aspire задаёт переменную `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, и тогда хост ASP.NET Core сам запускает этот middleware; поэтому `Program.cs` вызывает `UseForwardedHeaders` только когда переменная не задана (любой другой хостинг за TLS-прокси). Второй проход взял бы запись, которую написал клиент, и клиент мог бы выдать себя за любой адрес и обойти лимит по IP (тест `ProductionHostingTests`).
- **Ключи Data Protection в базе.** ASP.NET Core шифрует cookie сессии и состояние OAuth ключами Data Protection. По умолчанию ключи живут в файловой системе контейнера и пропадают при каждом перезапуске - всех бы разлогинивало. У нас ключи лежат в таблице `identity.data_protection_keys` (пакет `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) и общие для всех реплик. У Container Apps есть своё хранилище ключей, и Aspire его включает, но явно настроенное хранилище приложения имеет приоритет (проверено по исходникам ASP.NET Core 10), так что источник один - база.
- **`/alive`.** Проверка "процесс жив": Container Apps вызывает её каждые несколько секунд и при сбоях перезапускает контейнер. Отвечает только `Healthy`, поэтому открыта везде; подробный `/health` - только в Development.
- **Ограничение частоты (rate limiting).** Встроенный в ASP.NET Core `RateLimiter`: с одного IP-адреса (это адрес, который дописал ingress, см. Forwarded headers выше) можно запросить не больше 20 писем для входа за 10 минут (`Identity:EmailStartsPerAddressWindow`). Счётчик живёт в памяти каждой реплики API, поэтому при двух репликах фактический потолок - до 40 писем; это приемлемо, потому что API работает не более чем в двух репликах. Сверх этого API отвечает 429 с кодом `rate_limited`, который интерфейс переводит. Это дополнение к лимиту в 3 письма на один email: тот защищает конкретный ящик, этот - почтовый сервис от перебора адресов.
- **Проверка конфигурации при старте.** Если `Frontend:Origins` или `Frontend:ClientAppUrl` пустые или не абсолютные адреса, API не стартует (`ValidateOnStart`), а не ломает молча CORS и ссылки в письмах.
- **Тестовый вход только локально.** Тестовый провайдер входа разрешён только в окружениях `Development` и `Testing`; в любом другом (Production, Staging) API с ним не стартует.
- **Application Insights.** Экспортер Azure Monitor подключён в `Bayubai.ServiceDefaults` и включается, только если задана переменная `APPLICATIONINSIGHTS_CONNECTION_STRING` (её задаёт деплой в Azure). Архитектурный тест следит, чтобы модули, SharedKernel и API не использовали Azure SDK: переезд на другой хостинг - это смена конфигурации, а не кода.

Официальная документация: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer, https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers
YouTube (EN): `ASP.NET Core data protection keys explained`

## 19. Что почитать и посмотреть

**.NET 10 / ASP.NET Core / Minimal API**
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi
- YouTube (RU): `ASP.NET Core Minimal API обзор`
- YouTube (EN): `ASP.NET Core Minimal APIs tutorial`

**PostgreSQL**
- https://www.postgresql.org/docs/
- YouTube (RU): `PostgreSQL для разработчиков`
- YouTube (EN): `PostgreSQL crash course`

**EF Core + Npgsql**
- https://learn.microsoft.com/en-us/ef/core/
- https://www.npgsql.org/efcore/mapping/nodatime.html
- YouTube (RU): `Entity Framework Core обзор`
- YouTube (EN): `EF Core tutorial`

**NodaTime**
- https://nodatime.org/3.3.x/userguide/
- YouTube (EN): `NodaTime C# tutorial`

**ASP.NET Core Identity**
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
- Telegram Login Widget: https://core.telegram.org/widgets/login
- Telegram Mini Apps: https://core.telegram.org/bots/webapps
- YouTube (RU): `ASP.NET Core Identity без пароля`
- YouTube (EN): `ASP.NET Core Identity passwordless magic link`

**Playwright**
- https://playwright.dev/docs/intro
- YouTube (EN): `Playwright end to end testing tutorial`

**.NET Aspire**
- https://learn.microsoft.com/en-us/dotnet/aspire/
- Видео: [What Is .NET Aspire? The Insane Future of .NET! - Nick Chapsas](https://www.youtube.com/watch?v=DORZA_S7f9w)
- YouTube (RU): `.NET Aspire обзор`

**Mailpit**
- https://mailpit.axllent.org/docs/
- YouTube (EN): `Mailpit dotnet local email testing`

**Docker**
- https://docs.docker.com/get-started/
- YouTube (RU): `Docker для разработчика обзор`
- YouTube (EN): `Docker for developers crash course`

**Тесты: xUnit, Shouldly, Testcontainers, NetArchTest**
- https://xunit.net/
- https://docs.shouldly.org/
- https://dotnet.testcontainers.org/ и https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/
- https://github.com/BenMorris/NetArchTest
- YouTube (RU): `Testcontainers .NET интеграционные тесты`
- YouTube (EN): `Testcontainers dotnet integration testing`

**Azure и деплой**
- Aspire: деплой в Azure: https://aspire.dev/deployment/azure/
- Container Apps: https://learn.microsoft.com/en-us/azure/container-apps/overview
- Azure Database for PostgreSQL Flexible Server: https://learn.microsoft.com/en-us/azure/postgresql/overview
- Static Web Apps: https://learn.microsoft.com/en-us/azure/static-web-apps/overview
- Key Vault references в Container Apps: https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets
- Application Insights + OpenTelemetry: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview
- GitHub Actions + Azure OIDC: https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect
- Brevo SMTP: https://developers.brevo.com/docs/smtp-integration
- YouTube (RU): `Azure Container Apps обзор`
- YouTube (EN): `.NET Aspire deploy to Azure Container Apps`, `GitHub Actions OIDC Azure login`

**Фронтенд**
- https://pnpm.io/workspaces
- https://vite.dev/guide/ и https://react.dev/
- https://www.typescriptlang.org/docs/
- https://tanstack.com/router/latest/docs и https://tanstack.com/query/latest/docs
- https://orval.dev/
- https://www.i18next.com/
- https://tailwindcss.com/docs и https://ui.shadcn.com/
- https://vite-pwa-org.netlify.app/guide/
- https://vitest.dev/guide/ и https://mswjs.io/docs/
- YouTube (RU): `Vite React TypeScript обзор`
- YouTube (EN): `TanStack Router tutorial`
