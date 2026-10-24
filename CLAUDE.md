# Healthly Platform - Project Context

## Architecture & Tech Stack
- **Backend:** .NET 10 API (C#, Entity Framework Core, PostgreSQL, Npgsql). Located in `HealthlyApi/`.
- **Frontend:** Next.js (App Router, TypeScript, Tailwind CSS, Lucide icons). Located in `healthly-web/` (pages in `healthly-web/app/`, no `src/` folder).
- **Communication:** REST API on `http://localhost:5097`, CORS enabled for `http://localhost:3000`.

## Backend Conventions
- Controllers never accept or return EF entities — use DTOs from `HealthlyApi/Dtos/` (`PasswordHash` must never leave the API).
- Auth: JWT Bearer access token (15 min) + rotating refresh tokens (`/api/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `PUT /me/password`; `RefreshTokenService`, only SHA-256 hashes stored). Web gets the refresh token as an httpOnly cookie (`X-Refresh-Token-Transport: cookie`, `RefreshTokenCookie`), mobile in JSON. Passwords hashed with `PasswordHasher<User>`.
- Errors: always ProblemDetails — return `Problem(detail: "...", statusCode: ...)` / `ValidationProblem(...)`, never anonymous objects; annotate with `[ProducesResponseType<ProblemDetails>(...)]`. Unhandled exceptions → `Middleware/ExceptionHandlingMiddleware.cs`.
- Roles: `Patient`, `Doctor`, `Admin` (constants in `HealthlyApi/Models/Roles.cs`). Public registration always creates a `Patient`.
- Secrets (DB connection string, `Jwt:Key`) live in `dotnet user-secrets`, not in `appsettings.json`.
- Medical records: `DocumentsController` (upload/list/download/status/delete), `TestResultsController` (CRUD), `ReportsController` (`/api/users/{id}/report`). Access rule: owner or Doctor/Admin (`User.CanAccessUser`). Files stored by `FileStorageService` in `Storage:UploadsPath` (`HealthlyApi/uploads/`, gitignored).
- API docs (Development only): Scalar UI at `/scalar`, OpenAPI JSON at `/openapi/v1.json`.
- AI analysis: `POST /api/documents/{id}/analyze` → `Services/Ai/DocumentAnalysisService` → `IAiDocumentProcessor` (DI: `MockAiDocumentProcessor` for now; a real OCR+LLM implementation replaces only that registration in `Program.cs`). Any text sent to an external AI must go through `PiiAnonymizer.Anonymize` first. Normal/abnormal is decided in code against `ReferenceMedicalValues` (seeded via `HasData` in `Data/ReferenceMedicalValueSeed.cs` — new norms = edit seed + migration), not by the AI.

## Frontend Conventions
- `healthly-web/lib/api.ts` — the only place that calls `fetch`; typed functions per endpoint, `ApiError` for errors. Types in `healthly-web/types/api.ts` mirror backend DTOs.
- `healthly-web/lib/auth.tsx` — `AuthProvider`/`useAuth`. Access token in memory only, refresh token in the httpOnly cookie; `lib/api.ts` refreshes on 401 (single in-flight request, all fetches use `credentials: 'include'`). Protected pages wrap content in `AppShell` (`roles` prop) which uses `RequireAuth`.
- Landing page `app/page.tsx` only composes sections from `components/landing/*` (theme palette in `components/landing/theme.ts`).
- Data loading in components: `useApiData(load)` from `lib/useApiData.ts` (no direct setState in effects — `react-hooks/set-state-in-effect`).
- `cacheComponents` is on: client components using `useParams`/`usePathname` on dynamic routes must be inside `<Suspense>` (see `app/documents/[id]/page.tsx`). Verify with `npx next build`.
- Reusable UI in `components/ui/` (Button, Modal, ConfirmDialog, Field, Badge, Card, Alert).

## Useful Commands
- **Backend:** `dotnet run` (inside `HealthlyApi/`); `dotnet run --launch-profile lan` to reach it from phones/emulators (see `MOBILE_HANDOFF.md`, keep it in sync with API changes)
- **Migrations:** `dotnet ef migrations add <Name>` / `dotnet ef database update` (inside `HealthlyApi/`)
- **Tests:** `dotnet test` in the repo root (xUnit project `HealthlyApi.Tests/`, outside `HealthlyApi/` so it stays out of the API build and Docker context)
- **Frontend:** `npm run dev` (inside `healthly-web/`)
- **Database:** PostgreSQL `healthly_db`, managed via pgAdmin.
- **Docker (everything at once):** `docker compose up -d --build` in the root (web :3000, API :5097, Postgres :5433; volumes `postgres-data`, `uploads`). Secrets in `.env` (template `.env.example`).

## Development Rules
- Keep frontend and backend decoupled.
- Always use TypeScript with proper types (avoid `any` where possible).
- Follow Tailwind CSS for styling.
- Never commit to `main` — work in `feature/*` / `fix/*` branches and merge via PR (see `README_CONTRIBUTOR.md`).
- Never push to GitHub or open PRs — the project owner does all remote git operations.
- All user-visible text (site labels, buttons, API error messages, OpenAPI descriptions) is English. Russian only in code comments.
- Keep `PROJECT_GUIDE.md` (project map + roadmap, in Russian) up to date after each stage; `TEAM_GUIDE.md` (setup/accounts/URLs, Russian) and `MOBILE_HANDOFF.md` (API reference, English) when setup or the API changes.
