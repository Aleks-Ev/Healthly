# Healthly — Mobile Handoff

Everything a Flutter / React Native developer needs to integrate with the Healthly backend.

> **Running the backend, test accounts, base URLs — short version in Russian:** [TEAM_GUIDE.md](TEAM_GUIDE.md).
>
> **Interactive API docs:** `http://<host>:5097/scalar` — every endpoint with its request/response schemas and error codes,
> plus a "Try it" button. The raw OpenAPI 3.1 spec is at `/openapi/v1.json`; you can generate a typed client from it
> (e.g. `openapi-generator` for Dart, `openapi-typescript` for TS).

---

## 1. Quick start (5 minutes)

1. Start the backend on the computer that runs it (see [§2](#2-running-the-backend)):
   ```bash
   cd HealthlyApi
   dotnet run --launch-profile lan
   ```
2. Pick the base URL for your device from the table in [§3](#3-base-url).
3. Log in with a test account ([§5](#5-test-accounts)), e.g. `patient@healthly.com` / `Patient123!`:
   ```bash
   curl -X POST http://localhost:5097/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"patient@healthly.com","password":"Patient123!"}'
   ```
4. Send the returned `token` on every other request: `Authorization: Bearer <token>`.
5. Call `GET /api/auth/me`, then `GET /api/users/{id}/report` — the demo patient already has documents, results and trends.

---

## 2. Running the backend

**Easiest: Docker.** With Docker Desktop installed, run `docker compose up -d --build` in the project root.
It starts PostgreSQL, the API on port `5097` (reachable from emulators and phones like the `lan` profile below)
and the website on `3000`, applies the database schema and creates the test accounts. Details: `README.md` → *Run everything with Docker*.

**Without Docker:** .NET 10 SDK and PostgreSQL with the `healthly_db` database are required. In short:

```bash
cd HealthlyApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=healthly_db;Username=postgres;Password=<your password>"
dotnet user-secrets set "Jwt:Key" "<any random string, 32+ characters>"
dotnet ef database update
dotnet run --launch-profile lan
```

| Launch profile | Listens on | Use when |
|---|---|---|
| `lan` | `http://0.0.0.0:5097` — all network interfaces | **Mobile development**: emulators, real phones in the same Wi-Fi, ngrok |
| `http` (default for `dotnet run`) | `http://localhost:5097` only | Web frontend only; phones cannot reach it |

On startup in Development the API automatically creates the test accounts and demo data ([§5](#5-test-accounts)).

**Windows firewall.** For real phones to reach the PC, allow inbound connections on port 5097 once
(PowerShell **as Administrator**):

```powershell
New-NetFirewallRule -DisplayName "Healthly API 5097" -Direction Inbound -Protocol TCP -LocalPort 5097 -Action Allow -Profile Private
```

The Wi-Fi network must be set as *Private* in Windows settings.

---

## 3. Base URL

All endpoints live under `/api`. Base URL = `http://<host>:5097`.

| Where the app runs | Base URL | Notes |
|---|---|---|
| Android emulator | `http://10.0.2.2:5097` | `10.0.2.2` is the emulator's alias for the host PC's `localhost` |
| iOS simulator | `http://localhost:5097` | The simulator shares the Mac's network |
| Real phone, same Wi-Fi | `http://<PC's LAN IP>:5097`, e.g. `http://192.168.1.152:5097` | Find the IP: `ipconfig` (Windows) → *Wi-Fi adapter → IPv4 Address*. Needs the `lan` profile and the firewall rule above |
| Real phone, any network | `https://<id>.ngrok-free.app` | Run `ngrok http 5097` on the PC. HTTPS, no firewall or cleartext settings needed. The URL changes on every ngrok restart (free plan) |

Keep the base URL in one config value (e.g. `--dart-define=API_URL=...` in Flutter, `.env` / `app.config.ts` in React Native).

### Allow plain HTTP in debug builds

Local URLs are `http://`, which mobile OSes block by default. ngrok URLs are `https://` and need nothing.

- **Android** (API 28+): in the **debug** manifest (`android/app/src/debug/AndroidManifest.xml`)
  add `android:usesCleartextTraffic="true"` to `<application>`. Release builds should use HTTPS.
- **iOS**: in `Info.plist` (debug configuration) add `NSAppTransportSecurity` → `NSAllowsLocalNetworking = YES`
  (covers `localhost` and LAN IPs). On iOS 14+ the first request to a LAN IP also shows the *Local Network* permission prompt —
  add `NSLocalNetworkUsageDescription`.
- **Expo Go**: cleartext to LAN IPs works out of the box in development.

CORS does not apply to native apps. It only matters for **Flutter Web / React Native Web**: their origin must be added
to the CORS policy in `HealthlyApi/Program.cs` (currently only `http://localhost:3000` is allowed).

---

## 4. Authentication (JWT + refresh tokens)

```
POST /api/auth/login { email, password }
      → 200 { token, expiresAt, refreshToken, refreshTokenExpiresAt, user }

every request:   Authorization: Bearer <token>

401 on a request  →  POST /api/auth/refresh { refreshToken }
                       → 200: save BOTH new tokens, retry the request once
                       → 401: the session is over → delete tokens, show the login screen
```

| Token | Lifetime | Used for |
|---|---|---|
| `token` (access, JWT) | **15 minutes** | `Authorization: Bearer <token>` header on every request |
| `refreshToken` | **30 days**, renewed on every refresh | `POST /api/auth/refresh` only |

- `POST /api/auth/register` returns the same response as login — the user is logged in right after sign-up.
- **Rotation:** every `/auth/refresh` returns a **new refresh token** and the old one stops working. Always overwrite both
  stored tokens. As long as the app is used at least once in 30 days, the user never sees the login screen again.
- **One refresh at a time.** If several requests get `401` together, run **one** refresh, then retry them all
  (see the snippets in [§10](#10-client-snippets)). Sending an already exchanged refresh token again is treated as token
  theft and **ends all of the user's sessions** (a 30-second grace period covers accidental parallel refreshes).
- **On app start:** if tokens are stored, call `GET /api/auth/me` (it refreshes automatically through your 401 handler).
  `200` → logged in, the body is the fresh profile.
- **Logout:** `POST /api/auth/logout { refreshToken }` (always `204`), then delete both tokens on the device.
- **Password changed by an admin** → all of that user's refresh tokens are revoked; the user logs in again.
- **User changes their own password** (`PUT /api/auth/me/password`) → all *other* sessions end; the response contains
  a new token pair for this device — save it like after login.
- **Web only:** the website sends `X-Refresh-Token-Transport: cookie` and receives the refresh token as an httpOnly
  cookie (`refreshToken` is `null` in the body). Mobile apps don't send this header and get the token in JSON as described here.
- **Storage:** keep both tokens in secure storage — `flutter_secure_storage` (Flutter), `expo-secure-store` or
  `react-native-keychain` (React Native). Not in AsyncStorage / SharedPreferences.
- **Token claims** (if you decode the access token, e.g. with `jwt_decoder` / `jwt-decode`): `sub` = user id, `email`,
  `name` = full name, `role` = `Patient` | `Doctor` | `Admin`. Prefer `user` from the auth responses over decoding.
  The refresh token is an opaque random string — don't decode it.

---

## 5. Test accounts

Created automatically when the API starts in Development (`DemoData:Enabled` in `appsettings.Development.json`).
They exist only in local development databases.

| Role | Email | Password | What's inside |
|---|---|---|---|
| Patient | `patient@healthly.com` | `Patient123!` | **Elif Kaya**, Istanbul. Glucose and cholesterol above normal, hemoglobin below — all back to normal by the latest test. Plus 1 `Pending` document (yesterday) waiting for a doctor |
| Doctor | `doctor@healthly.com` | `Doctor123!` | **Dr. Mehmet Yilmaz**, Ankara — stable, normal results. Sees all patients and their records, can change document statuses |
| Admin | `admin@healthly.com` | `Admin123!` | **Healthly Admin**, Izmir — cholesterol and glucose slowly rising out of range (an example of a worsening trend). Full user management |

**Lab history (all three accounts):** 6 lab reports, one per month for the last 6 months (the latest one week before
the API first started), each with **Glucose** (mmol/L), **Cholesterol** (mmol/L) and **Hemoglobin** (g/L) — so
`/report` and `/trends` have data and the charts show a trend right away.

- Accounts are created **only if that email doesn't exist yet**; an existing account's password is never overwritten.
  If `admin@healthly.com` was created earlier from `SeedAdmin` user-secrets, it keeps that password.
- History is added to a demo account **only if it has no documents**.
- Dates are counted from the day the history was created. To move it to "the last 6 months" again (or to restore
  demo data after experiments), start the API once with `DemoData:ResetHistory = true` — it deletes the demo accounts'
  documents and creates the history again:
  `DemoData__ResetHistory=true dotnet run` (bash) or `DEMO_RESET_HISTORY=true` in `.env` for Docker (then set it back to `false`).
- You can always register more patients with `POST /api/auth/register`.

---

## 6. Conventions

| Topic | Rule |
|---|---|
| Format | JSON, `Content-Type: application/json`, **camelCase** field names |
| Timestamps | UTC ISO 8601: `"2026-10-20T09:00:00Z"` — convert to local time for display |
| Dates without time | `"YYYY-MM-DD"` — `dateOfBirth` only. Don't convert it to local time (it would shift the day) |
| Ids | integers |
| Enums | strings: roles `Patient` / `Doctor` / `Admin`; document status `Pending` / `Processing` / `Completed` / `Error`; gender `Female` / `Male` / `Other` |
| Nullable | `height`, `weight`, `dateOfBirth`, `lastUploadDate`, `referenceMin`, `referenceMax` can be `null`. Empty strings mean "not set" for `gender`, `phoneNumber`, `city`, `unit`, `referenceRange` |
| Test values | `value` in test results is a **string** (`"5.4"`, `"positive"`). Trends contain numeric values only (`number`) |
| Pagination | none — lists return everything |

### Status codes and error bodies

**Every error** is JSON in the [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) format,
`Content-Type: application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Document not found",
  "instance": "GET /api/documents/999",
  "traceId": "00-6f3c...-01"
}
```

| Field | Use it for |
|---|---|
| `detail` | **The message to show the user** (English) |
| `status` | Same as the HTTP status code |
| `errors` | Only on validation `400`: messages per field |
| `traceId` | Include it in bug reports — the backend logs contain the same id |

| Code | When | Example `detail` |
|---|---|---|
| `200` / `201` | OK / created | — (the resource) |
| `204` | Deleted / logged out | — (empty body) |
| `400` | Validation failed, or a bad file on upload | — (see `errors`) / `"File must be between 1 byte and 10 MB"` |
| `401` | Access token missing, invalid or **expired** → refresh ([§4](#4-authentication-jwt--refresh-tokens)); wrong password on login; bad refresh token | `"The access token has expired. Get a new one with POST /api/auth/refresh."` / `"Invalid email or password"` |
| `403` | Logged in, but not allowed (wrong role, or someone else's data) | `"You do not have permission to perform this action."` |
| `404` | Not found (record or URL) | `"User not found"` |
| `405` | Wrong HTTP method for the URL | `"This HTTP method is not supported for this endpoint."` |
| `409` | Email already registered | `"A user with this email already exists"` |
| `500` | Server bug | `"An unexpected error occurred. Please try again later."` |

Validation error (`400`) — show `errors[field][0]` under the matching input. Keys are PascalCase DTO property names
(`Email`, `FirstName`, `DateOfBirth`…) — compare case-insensitively. There is no `detail` here:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."],
    "Password": ["The Password field is required."]
  },
  "traceId": "00-..."
}
```

A simple client rule: show `detail` if present, otherwise the first value from `errors`, otherwise `title`.

---

## 7. Patient endpoints

The patient's own id is `user.id` from login / `/api/auth/me`. A patient gets `403` for any other user's id.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/auth/register` | Sign up (public) |
| `POST` | `/api/auth/login` | Log in (public) |
| `POST` | `/api/auth/refresh` | New token pair by refresh token (public) |
| `POST` | `/api/auth/logout` | Revoke the refresh token (public) |
| `PUT` | `/api/auth/me/password` | Change own password |
| `GET` | `/api/auth/me` | Current user / token check |
| `PUT` | `/api/users/{id}` | Edit own profile |
| `GET` | `/api/users/{id}/report` | Home screen: counters + latest value of each indicator |
| `GET` | `/api/users/{id}/trends` | Charts: history of each indicator |
| `GET` | `/api/users/{id}/documents` | List of uploaded documents |
| `POST` | `/api/users/{id}/documents` | Upload a document (multipart) |
| `GET` | `/api/documents/{id}` | Document details + its test results |
| `GET` | `/api/documents/{id}/file` | Download the original file |
| `DELETE` | `/api/documents/{id}` | Delete own document |
| `POST` | `/api/documents/{id}/analyze` | AI analysis of a document (**mock for now**) |
| `POST` | `/api/documents/{id}/results` | Add a test result to a document |
| `PUT` / `DELETE` | `/api/results/{id}` | Edit / delete a test result |

### Register — `POST /api/auth/register`

```json
{
  "email": "ayse@example.com",
  "password": "Passw0rd!",
  "firstName": "Ayse",
  "lastName": "Yilmaz",
  "phoneNumber": "+90 532 123 45 67",
  "dateOfBirth": "1990-05-14",
  "gender": "Female",
  "city": "Istanbul",
  "accessCode": null,
  "marketingOptIn": false,
  "acceptTerms": true,
  "acceptMedicalAuthorization": true
}
```

| Field | Rule |
|---|---|
| `email` | required, valid email, unique (`409` otherwise), stored lowercase |
| `password` | required, 8–128 characters |
| `firstName`, `lastName` | required, ≤ 100 characters; stored together as `fullName` |
| `phoneNumber` | required, Turkish: `+90` + 10 digits; spaces, dashes and brackets allowed; stored as `+905321234567` |
| `dateOfBirth` | required, `YYYY-MM-DD`, age 18–120 |
| `gender` | required: `Female`, `Male` or `Other` |
| `city` | required: `Istanbul`, `Ankara`, `Izmir`, `Antalya`, `Bursa`, `Adana`, `Gaziantep`, `Konya` |
| `accessCode` | optional, ≤ 50 characters |
| `marketingOptIn` | optional checkbox |
| `acceptTerms` | **must be `true`** — Privacy Policy, Terms of Service, Request for Lab Results |
| `acceptMedicalAuthorization` | **must be `true`** — Authorization for Use of Medical Information |

Response `201`: same shape as login.

### Login — `POST /api/auth/login`

Request `{ "email": "patient@healthly.com", "password": "Patient123!" }`. Response `200`:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-23T09:26:52Z",
  "refreshToken": "q8Lr2v0J6m7...Xw",
  "refreshTokenExpiresAt": "2026-11-22T09:11:52Z",
  "user": {
    "id": 31,
    "email": "patient@healthly.com",
    "fullName": "Elif Kaya",
    "role": "Patient",
    "height": 165,
    "weight": 60,
    "gender": "Female",
    "phoneNumber": "+905321112233",
    "dateOfBirth": "1992-04-18",
    "city": "Istanbul",
    "createdAt": "2026-10-23T00:11:14Z"
  }
}
```

`GET /api/auth/me` and every user endpoint return the same `user` object (the `User` model).

### Refresh — `POST /api/auth/refresh`

Request `{ "refreshToken": "q8Lr2v0J6m7...Xw" }` → `200` with the same body as login (new `token` **and** new
`refreshToken`). `401` → the refresh token is invalid, expired, revoked or already used: log in again.

### Logout — `POST /api/auth/logout`

Request `{ "refreshToken": "..." }` → always `204`. Delete both tokens on the device afterwards.

### Change own password — `PUT /api/auth/me/password`

```json
{ "currentPassword": "Patient123!", "newPassword": "NewPass123!" }
```

`200` → same body as login: **save the new token pair** (all other sessions of the user are ended, the old refresh token
of this device too). `newPassword` is 8–128 characters and must differ from the current one.
A wrong current password returns **`400`** with `errors.CurrentPassword` — not `401`, so your refresh logic won't fire.

### Edit profile — `PUT /api/users/{id}`

A full update: send all fields, even unchanged ones.

```json
{ "email": "patient@healthly.com", "fullName": "Elif Kaya", "height": 166, "weight": 59.5, "gender": "Female", "role": null, "password": null }
```

`height` is 30–300 cm, `weight` is 1–500 kg, both optional. A patient must send `role: null` and `password: null`;
anything else returns `403`. `phoneNumber`, `dateOfBirth` and `city` are set at registration and cannot be edited yet.

### Health report — `GET /api/users/{id}/report`

```json
{
  "user": { "...": "User model" },
  "documentsCount": 7,
  "resultsCount": 18,
  "abnormalCount": 11,
  "lastUploadDate": "2026-10-20T09:00:00Z",
  "indicators": [
    {
      "testName": "Cholesterol",
      "latestValue": "4.9",
      "unit": "mmol/L",
      "referenceRange": "<5.2",
      "isAbnormal": false,
      "measuredAt": "2026-10-02T09:00:00Z",
      "measurementsCount": 4,
      "documentId": 19
    }
  ]
}
```

`indicators` holds the **latest** value of each test (grouped by name, case-insensitive). Out-of-range indicators come first.
`abnormalCount` counts all out-of-range results, not indicators.

### Trends for charts — `GET /api/users/{id}/trends`

```json
[
  {
    "testName": "Cholesterol",
    "unit": "mmol/L",
    "referenceRange": "<5.2",
    "referenceMin": null,
    "referenceMax": 5.2,
    "points": [
      { "documentId": 16, "measuredAt": "2026-01-15T09:00:00Z", "value": 6.4, "isAbnormal": true },
      { "documentId": 17, "measuredAt": "2026-04-10T09:00:00Z", "value": 5.9, "isAbnormal": true }
    ]
  }
]
```

- Points are sorted oldest → newest. Only numeric values are included.
- `referenceMin` / `referenceMax` are the normal-range band; `null` means no bound on that side.
- A line needs **at least 2 points**. With fewer, show an empty state (the web app shows "Upload first lab result").
- Different indicators have different units and scales — draw **one indicator per chart** (the web app uses a selector).
- Chart libraries: `fl_chart` (Flutter), `victory-native` or `react-native-gifted-charts` (React Native).

### Documents

`GET /api/users/{id}/documents` — newest first:

```json
[
  {
    "id": 20,
    "userId": 31,
    "originalFileName": "thyroid-panel-2026-10-20.pdf",
    "contentType": "application/pdf",
    "fileSizeBytes": 629,
    "uploadDate": "2026-10-20T09:00:00Z",
    "status": "Pending",
    "resultsCount": 0,
    "abnormalCount": 0
  }
]
```

`GET /api/documents/{id}` returns the same fields (without the counters) plus `results`:

```json
{ "id": 19, "status": "Completed", "...": "...",
  "results": [
    { "id": 73, "medicalDocumentId": 19, "testName": "Glucose", "value": "5.0", "unit": "mmol/L", "referenceRange": "3.9-5.6", "isAbnormal": false }
  ] }
```

**Upload** — `POST /api/users/{id}/documents`, `multipart/form-data`, one field named **`file`**.
PDF, PNG, JPG/JPEG; max 10 MB. Response `201` = document details (status `Pending`, empty `results`).
Do **not** set the `Content-Type` header by hand — the HTTP client must add the multipart boundary.

**Download** — `GET /api/documents/{id}/file` returns the raw bytes with the original `Content-Type`.
It needs the Bearer header, so a plain `<Image src>` / `url_launcher` link won't work: download with the authenticated
client to a temp file, then open it (`open_filex` / `react-native-file-viewer` / `react-native-pdf`).

**Document status** — `Pending` (just uploaded) → `Processing` (set by a doctor) → `Completed` (set automatically
when the first test result is added, or by a doctor) / `Error` (set by a doctor).

### AI analysis — `POST /api/documents/{id}/analyze`

No request body. Recognizes the lab report, extracts the indicators, compares them with the reference norms and returns
a plain-language summary. **Currently a mock** (`isMock: true`): the values are generated (the same for the same document),
the file itself is not read yet. The response format will stay the same when real OCR + LLM are connected, so you can
build the screen now. Takes ~0.3 s now; a real model will take several seconds — show a loader.

```json
{
  "documentId": 33,
  "indicators": [
    { "testName": "Glucose", "value": "6.2", "unit": "mmol/L", "referenceRange": "3.9-5.6", "isAbnormal": true,
      "category": "Biochemistry", "doctorSpecialty": "Endocrinologist", "confidence": 0.94 },
    { "testName": "Leukocytes", "value": "7.7", "unit": "10^9/L", "referenceRange": "4-9", "isAbnormal": false,
      "category": "Complete blood count", "doctorSpecialty": "Hematologist", "confidence": 0.91 }
  ],
  "abnormalCount": 3,
  "summary": "[Mock AI summary] 3 of 4 indicators are outside the reference range: Glucose, Cholesterol, Hemoglobin. ...",
  "recommendations": ["Repeat a fasting glucose test and consider an HbA1c test.", "..."],
  "suggestedSpecialists": ["Endocrinologist", "Cardiologist", "Hematologist"],
  "anonymizedText": "Patient: [REDACTED_NAME]\nDate of birth: [REDACTED_DATE]\n...",
  "model": "mock-ai-v1",
  "isMock": true,
  "analyzedAt": "2026-10-23T01:35:10Z",
  "disclaimer": "This analysis is generated automatically and is not a medical diagnosis. ..."
}
```

- `isAbnormal` is decided by the server against its reference norms (not by the AI); `null` = could not be determined.
- `suggestedSpecialists` — doctors for the out-of-range indicators; empty when everything is normal.
- `anonymizedText` — the text that was sent to the AI after personal data was masked; you don't need to show it.
- **Always show `disclaimer`** next to the AI summary.
- Nothing is saved. To keep the values, add them as results: `POST /api/documents/{id}/results` for each indicator.
- Errors: `403` (someone else's document), `404` (no document, or its file is missing).

### Test results

`POST /api/documents/{documentId}/results` and `PUT /api/results/{id}`:

```json
{ "testName": "Glucose", "value": "5.4", "unit": "mmol/L", "referenceRange": "3.9-5.6", "isAbnormal": null }
```

`testName` (≤ 100) and `value` (≤ 50) are required. `isAbnormal: null` → the server decides from `referenceRange`;
supported formats: `120-160`, `<5`, `<=5`, `>1.2`, `>=1.2` (a comma works as the decimal separator).
Send `true`/`false` to set it manually (needed for non-numeric values like `"positive"`).

---

## 8. Doctor / Admin endpoints

| Method | Path | Doctor | Admin | Purpose |
|---|---|:-:|:-:|---|
| `GET` | `/api/users?role=Patient` | ✅ | ✅ | List users (sorted by id). `role` filter is optional |
| `GET` | `/api/users/{id}` | ✅ | ✅ | Any user's profile |
| `GET` | `/api/users/{id}/report` · `/trends` · `/documents` | ✅ | ✅ | Any patient's records — same endpoints as the patient |
| `GET` | `/api/documents/{id}` · `/file` | ✅ | ✅ | Any document |
| `PUT` | `/api/documents/{id}/status` | ✅ | ✅ | **Moderation**: change document status |
| `POST` | `/api/documents/{id}/results` · `PUT/DELETE /api/results/{id}` | ✅ | ✅ | Enter / correct a patient's results |
| `POST` | `/api/users` | ❌ | ✅ | Create a user with any role |
| `PUT` | `/api/users/{id}` | ❌ (own only) | ✅ | Edit any user, incl. `role` and `password` |
| `DELETE` | `/api/users/{id}` | ❌ | ✅ | Delete a user with all their documents (not yourself) |
| `DELETE` | `/api/documents/{id}` | ❌ | ✅ | Delete any document |

### Moderation flow

1. `GET /api/users?role=Patient` → pick a patient.
2. `GET /api/users/{id}/documents` → documents with `status: "Pending"` are waiting for review.
3. Open one: `GET /api/documents/{id}` and its file `GET /api/documents/{id}/file`.
4. `PUT /api/documents/{id}/status` with `{ "status": "Processing" }` while working on it.
5. Enter results: `POST /api/documents/{id}/results` for each value → the document becomes `Completed` automatically.
   If the file is unreadable: `{ "status": "Error" }`.

### Create user — `POST /api/users` (Admin)

```json
{ "email": "new.doctor@healthly.com", "fullName": "Dr. Ayse Demir", "password": "Passw0rd!", "role": "Doctor", "height": null, "weight": null, "gender": "Female" }
```

---

## 9. Access rules in one table

| Action | Patient | Doctor | Admin |
|---|:-:|:-:|:-:|
| Register / log in | ✅ | ✅ | ✅ |
| Read **own** profile, report, trends, documents, results | ✅ | ✅ | ✅ |
| Read **other users'** data | ❌ `403` | ✅ | ✅ |
| Upload documents and edit results for a user | own only | any | any |
| Change document status | ❌ | ✅ | ✅ |
| Delete a document | own only | ❌ | ✅ |
| Edit a profile | own only (no role/password) | own only (no role/password) | any, incl. role and password |
| List / create / delete users | ❌ | list only | ✅ |

Hide UI the user can't use by checking `user.role`; the server enforces the same rules anyway.

---

## 10. Client snippets

### Flutter (dio)

```dart
final dio = Dio(BaseOptions(baseUrl: const String.fromEnvironment('API_URL', defaultValue: 'http://10.0.2.2:5097')));
const storage = FlutterSecureStorage();

Future<void> saveTokens(Map<String, dynamic> auth) async {
  await storage.write(key: 'access_token', value: auth['token']);
  await storage.write(key: 'refresh_token', value: auth['refreshToken']);
}

// One refresh for all requests that got 401 at the same time
Future<bool>? refreshing;

Future<bool> refreshTokens() => refreshing ??= () async {
  try {
    final refreshToken = await storage.read(key: 'refresh_token');
    if (refreshToken == null) return false;
    // A separate Dio without interceptors, so a 401 here does not loop
    final res = await Dio(BaseOptions(baseUrl: dio.options.baseUrl))
        .post('/api/auth/refresh', data: {'refreshToken': refreshToken});
    await saveTokens(res.data);
    return true;
  } catch (_) {
    return false;
  } finally {
    refreshing = null;
  }
}();

const noRefreshPaths = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout'];

dio.interceptors.add(InterceptorsWrapper(
  onRequest: (options, handler) async {
    final token = await storage.read(key: 'access_token');
    if (token != null) options.headers['Authorization'] = 'Bearer $token';
    handler.next(options);
  },
  onError: (error, handler) async {
    final request = error.requestOptions;
    final canRefresh = error.response?.statusCode == 401
        && !noRefreshPaths.contains(request.path)
        && request.extra['retried'] != true;
    if (canRefresh && await refreshTokens()) {
      request.extra['retried'] = true;
      request.headers['Authorization'] = 'Bearer ${await storage.read(key: 'access_token')}';
      return handler.resolve(await dio.fetch(request)); // retry once with the new token
    }
    if (error.response?.statusCode == 401 && !noRefreshPaths.contains(request.path)) {
      await storage.deleteAll(); // session over → navigate to the login screen
    }
    handler.next(error);
  },
));

// Log in
final res = await dio.post('/api/auth/login', data: {'email': email, 'password': password});
await saveTokens(res.data);
final userId = res.data['user']['id'];

// Log out
await dio.post('/api/auth/logout', data: {'refreshToken': await storage.read(key: 'refresh_token')});
await storage.deleteAll();

// Upload a document (FormData can't be re-sent: for uploads, refresh first if the token is about to expire)
final form = FormData.fromMap({'file': await MultipartFile.fromFile(path, filename: 'lab.pdf')});
await dio.post('/api/users/$userId/documents', data: form);

// Error message for the UI (ProblemDetails)
String errorMessage(DioException e) {
  final data = e.response?.data;
  if (data is Map && data['detail'] != null) return data['detail'];
  if (data is Map && data['errors'] is Map) return (data['errors'] as Map).values.first[0];
  if (data is Map && data['title'] != null) return data['title'];
  return 'Something went wrong (${e.response?.statusCode ?? 'network error'})';
}
```

### React Native (fetch, TypeScript)

```ts
import * as SecureStore from 'expo-secure-store';

const API_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5097';
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

export async function saveTokens(auth: { token: string; refreshToken: string }) {
  await SecureStore.setItemAsync('access_token', auth.token);
  await SecureStore.setItemAsync('refresh_token', auth.refreshToken);
}

// One refresh for all requests that got 401 at the same time
let refreshing: Promise<boolean> | null = null;

function refreshTokens(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const refreshToken = await SecureStore.getItemAsync('refresh_token');
      if (!refreshToken) return false;
      const res = await fetch(`${API_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) return false;
      await saveTokens(await res.json());
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

async function send(path: string, init: RequestInit): Promise<Response> {
  const token = await SecureStore.getItemAsync('access_token');
  const headers: Record<string, string> = { ...(init.headers as Record<string, string>) };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (init.body && !(init.body instanceof FormData)) headers['Content-Type'] = 'application/json';
  return fetch(`${API_URL}/api${path}`, { ...init, headers });
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  let res = await send(path, init);

  if (res.status === 401 && !NO_REFRESH_PATHS.includes(path)) {
    if (await refreshTokens()) res = await send(path, init); // retry once with the new token
    if (res.status === 401) {
      await SecureStore.deleteItemAsync('access_token');
      await SecureStore.deleteItemAsync('refresh_token'); // session over → go to login
    }
  }

  if (!res.ok) {
    const problem = await res.json().catch(() => ({}));
    const fieldError = problem.errors ? (Object.values(problem.errors) as string[][])[0]?.[0] : undefined;
    throw new Error(problem.detail ?? fieldError ?? problem.title ?? `Request failed (${res.status})`);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// Log in / log out
await saveTokens(await api('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }));
await api('/auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: await SecureStore.getItemAsync('refresh_token') }) });

// Upload a document (React Native FormData file object)
const form = new FormData();
form.append('file', { uri: fileUri, name: 'lab.pdf', type: 'application/pdf' } as unknown as Blob);
await api(`/users/${userId}/documents`, { method: 'POST', body: form });
```

Ready-made TypeScript types for every request/response: `healthly-web/types/api.ts` (copy as is).
The web client `healthly-web/lib/api.ts` is a working reference for every call.

---

## 11. Known limitations

- Refresh tokens are not bound to a device; there is no "list / end my other sessions" screen yet.
- No "forgot password" flow (email reset). Users change their password with `PUT /api/auth/me/password`; Admin can set one via `PUT /api/users/{id}`.
- `phoneNumber`, `dateOfBirth`, `city` cannot be edited after registration.
- A document's date is its **upload** date (there is no separate sampling date), and trends use it.
- Test results are entered manually; nothing is extracted from the uploaded file.
- No pagination, search or push notifications on the server.
- Scalar and the demo accounts are available only in the Development environment.

## 12. Troubleshooting

| Symptom | Fix |
|---|---|
| Android: `CLEARTEXT communication not permitted` | Add `usesCleartextTraffic` for debug builds ([§3](#allow-plain-http-in-debug-builds)) or use ngrok |
| Timeout / connection refused from a real phone | Run with `--launch-profile lan`, add the firewall rule, phone and PC on the same Wi-Fi, use the PC's IP (not `localhost`) |
| Emulator can't connect to `localhost:5097` | Android emulator: use `10.0.2.2` |
| `401` on every request | Header must be exactly `Authorization: Bearer <token>` (one space), no quotes around the token. Read `detail`: "expired" → refresh |
| `401` from `/auth/refresh` right after a successful refresh | The old refresh token was sent again — save the new one from every refresh response; refresh only once for parallel requests |
| Logged out on all devices unexpectedly | A refresh token was reused after rotation (treated as theft), or an admin changed the password |
| `403` | Patient calling another user's id, or a role-restricted endpoint ([§9](#9-access-rules-in-one-table)) |
| `400` on upload | Field must be named `file`; only PDF/PNG/JPG up to 10 MB; let the client set the multipart `Content-Type` |
| `400` on register | Read `errors` — usually phone format, age under 18, or a checkbox left `false` |
| Login with `admin@healthly.com` / `Admin123!` fails | That admin existed before the demo seeder; ask the backend owner for its password ([§5](#5-test-accounts)) |
