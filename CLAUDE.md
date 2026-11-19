# Healthly Platform - Project Context

## Architecture & Tech Stack
- **Backend:** .NET 10 API (C#, Entity Framework Core, PostgreSQL, Npgsql). Located in `HealthlyApi/`.
- **Frontend:** Next.js (App Router, TypeScript, Tailwind CSS, Lucide icons). Located in `healthly-web/` (pages in `healthly-web/app/`, no `src/` folder).
- **Communication:** REST API on `http://localhost:5097`, CORS enabled for `http://localhost:3000`.

## Backend Conventions
- Controllers never accept or return EF entities — use DTOs from `HealthlyApi/Dtos/` (`PasswordHash` must never leave the API).
- Auth: JWT Bearer access token (15 min) + rotating refresh tokens (`/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `PUT /me/password`; `RefreshTokenService`, only SHA-256 hashes stored). Web gets the refresh token as an httpOnly cookie (`X-Refresh-Token-Transport: cookie`, `RefreshTokenCookie`), mobile in JSON. Passwords hashed with `PasswordHasher<User>`.
- Errors: always ProblemDetails — return `Problem(detail: "...", statusCode: ...)` / `ValidationProblem(...)`, never anonymous objects; annotate with `[ProducesResponseType<ProblemDetails>(...)]`. Unhandled exceptions → `Middleware/ExceptionHandlingMiddleware.cs`.
- Roles: `Patient`, `Doctor`, `Admin`, `LabStaff` (constants in `HealthlyApi/Models/Roles.cs`). Public registration always creates a `Patient` (with a Healthly ID `PatientCode`, `Services/PatientCodes`). `LabStaff` belongs to a `Laboratory` (JWT claim `lab`), works on `/lab` (`LabController`: find patient by Healthly ID, upload, queue) and may open only documents uploaded by their laboratory — document endpoints check `CareAccess.CanAccessDocumentAsync`, never only the user check. Document actions are written to `AuditEvents` via `AuditLog` (same `SaveChanges` as the action).
- Secrets (DB connection string, `Jwt:Key`) live in `dotnet user-secrets`, not in `appsettings.json`.
- Medical records: `DocumentsController` (upload/list/download/status/delete), `TestResultsController` (CRUD), `ReportsController` (`/api/users/{id}/report`). Access rule: owner, Admin, or a doctor with an **active care connection** (`Services/Care/CareAccess.CanAccessUserAsync`; patients choose doctors/labs on `/care`, `CareController`, statuses in `CareRules`). `GET /api/users` returns a doctor only their patients. Files stored by `FileStorageService` in `Storage:UploadsPath` (`HealthlyApi/uploads/`, gitignored).
- API docs (Development only): Scalar UI at `/scalar`, OpenAPI JSON at `/openapi/v1.json`.
- AI analysis ("AI proposes, a person confirms"): `POST /api/documents/{id}/analyze` saves a draft in `DocumentAnalyses` (repeat calls return it without a new AI request, `?force=true` re-analyzes), `GET /analysis` reads it, `POST /analysis/confirm` writes the person-checked values into `TestResults` (converted to catalog units, with `IsCritical`). Pipeline: `Services/Ai/DocumentAnalysisService` → `IAiDocumentProcessor`. DI in `Program.cs`: `FallbackAiDocumentProcessor` over `GeminiAiDocumentProcessor` → `GroqAiDocumentProcessor` (only providers with `Ai:Gemini:ApiKey` / `Ai:Groq:ApiKey` in user-secrets; none → `MockAiDocumentProcessor`). Shared pipeline in `LlmDocumentProcessor`: `DocumentTextExtractor` (scoped; PdfPig for PDF text, `Services/Ai/Ocr/` Tesseract CLI for photos/scanned pages via `IOcrEngine`; the file itself never leaves the server; unreadable → 422) → anonymize → LLM JSON → `ParseResponse` → low-confidence OCR words cap indicator `confidence` at 0.4 (in code, not by the AI). Provider failures throw `AiProviderException` (→ next provider), all failed → `AiAnalysisException` 503. Any text sent to an external AI must go through `PiiAnonymizer.Anonymize` first. Normal/abnormal is decided in code (`DocumentAnalysisService.Evaluate`: lab units converted by `UnitConverter`, unknown unit → printed range; suspected lost decimal points → `isAbnormal: null`, confidence 0.4) against `ReferenceMedicalValues`, not by the AI. Norms live only in `HealthlyApi/Data/reference-values.csv` (edited by medical advisors, guide: `Data/REFERENCE_VALUES.md`; rows per sex/age, aliases, critical limits → `isCritical`), validated by `ReferenceValuesCsv` and synced into the table by `DbInitializer` on every start — never hard-code medical numbers. The current file is an unreviewed DRAFT.
- AI chat "Healthly AI" (`ChatController`, `/api/chat/messages` GET/POST/DELETE, table `ChatMessages`): `Services/Ai/Chat/HealthChatService` → `HealthChatContextBuilder` (only sex, age, height, weight + latest confirmed `TestResults`; never name, e-mail, phone, date of birth, city, Healthly ID) → `HealthChatPrompt` (rules: health + Healthly support only, evidence-based FDA/TİTCK/WHO, triage ER vs specialist, 10 fixed off-topic replies; patient data and messages through `PiiAnonymizer`, public lab/doctor directory not masked) → `IChatCompletionClient`: `FallbackChatClient` over `GeminiChatClient` → `GroqChatClient` (same `Ai:*:ApiKey`), none → `MockChatClient`. Rate limit `RateLimiting.AiChat` (10/min + 30/h per user). Disclaimer under every answer (frontend `t.chat.disclaimer`). Changing the prompt rules → keep `HealthChatTests` green.

## Frontend Conventions
- `healthly-web/lib/api.ts` — the only place that calls `fetch`; typed functions per endpoint, `ApiError` for errors. Types in `healthly-web/types/api.ts` mirror backend DTOs.
- `healthly-web/lib/auth.tsx` — `AuthProvider`/`useAuth`. Access token in memory only, refresh token in the httpOnly cookie; `lib/api.ts` refreshes on 401 (single in-flight request, all fetches use `credentials: 'include'`). Protected pages wrap content in `AppShell` (`roles` prop) which uses `RequireAuth`.
- Landing page `app/page.tsx` only composes sections from `components/landing/*` (theme palette in `components/landing/theme.ts`).
- Data loading in components: `useApiData(load)` from `lib/useApiData.ts` (no direct setState in effects — `react-hooks/set-state-in-effect`).
- `cacheComponents` is on: client components using `useParams`/`usePathname` on dynamic routes must be inside `<Suspense>` (see `app/documents/[id]/page.tsx`). Verify with `npx next build`.
- Reusable UI in `components/ui/` (Button, Modal, ConfirmDialog, Field, Badge, Card, Alert).
- UI texts of new screens go to `locales/en.ts` + `locales/tr.ts` (`useI18n()` → `t.…`); the AI chat page is `app/chat` + `components/chat/HealthChat.tsx`.

## Useful Commands
- **Backend:** `dotnet run` (inside `HealthlyApi/`); `dotnet run --launch-profile lan` to reach it from phones/emulators (see `MOBILE_HANDOFF.md`, keep it in sync with API changes)
- **Migrations:** `dotnet ef migrations add <Name>` / `dotnet ef database update` (inside `HealthlyApi/`)
- **Tests:** `dotnet test` in the repo root (xUnit project `HealthlyApi.Tests/`, outside `HealthlyApi/` so it stays out of the API build and Docker context)
- **Frontend:** `npm run dev` (inside `healthly-web/`)
- **Database:** PostgreSQL `healthly_db`, managed via pgAdmin.
- **Docker (everything at once):** `docker compose up -d --build` in the root (web :3000, API :5097, Postgres :5433; volumes `postgres-data`, `uploads`). Secrets in `.env` (template `.env.example`).
- **Public demo server:** `docker compose --profile tunnel up -d --build` → https://healthilyclub.com via Cloudflare Tunnel (TEAM_GUIDE.md §7). The site proxies `/api/*` to the API (`API_PROXY_TARGET`, `next.config.ts` rewrites, `PUBLIC_API_URL=/api`) so cookies stay same-site; API trusts `X-Forwarded-*` (`Proxy:TrustForwardedHeaders`). Rate limits in `Services/RateLimiting.cs` (`[EnableRateLimiting]` on login/register, analyze, lab lookup). Production = no demo accounts; first admin from `SeedAdmin:*` in every environment.

## Development Rules
- Keep frontend and backend decoupled.
- Always use TypeScript with proper types (avoid `any` where possible).
- Follow Tailwind CSS for styling.
- Never commit to `main` — work in `feature/*` / `fix/*` branches and merge via PR (see `README_CONTRIBUTOR.md`).
- Never push to GitHub or open PRs — the project owner does all remote git operations.
- All user-visible text (site labels, buttons, API error messages, OpenAPI descriptions) is English. Russian only in code comments.
- Keep `PROJECT_GUIDE.md` (project map + roadmap, in Russian) up to date after each stage; `TEAM_GUIDE.md` (setup/accounts/URLs, Russian) and `MOBILE_HANDOFF.md` (API reference, English) when setup or the API changes.
