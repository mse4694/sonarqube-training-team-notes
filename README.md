# Team Notes - SonarQube Developer Training (hands-on starter)

A deliberately vulnerable "team notes" app for the 2-day **SonarQube Developer Training**.
Backend is **ASP.NET Core Web API (C#, .NET 10)**, frontend is **Next.js (TypeScript)**,
data in **PostgreSQL** (raw SQL via Npgsql + Dapper, Markdown via Markdig). Everything runs with Docker Compose.

> ⚠️ Every vulnerability here is intentional and every secret is **fake**. Do not deploy this.

## Branches & tags

| Ref | What |
|---|---|
| `main` | Vulnerable starter (the labs find these) |
| `solution` | One fix commit per lab id (`day1-auth` / `lab2-*` / `lab3-*` / `lab4-*` / `lab5-*` / `lab6-sca` / `lab7-*`) + tests |
| `nextjs-only-main` / `nextjs-only-solution` (tags) | The earlier Next.js-only version of this app |

`git log --oneline main..solution` lists the fix commits; `git show <sha>` is the before/after of one fix.

## Features

| Feature | Web page | API |
|---|---|---|
| Sign in / sign out | `/login` | `POST /api/auth/login`, `POST /api/auth/logout` |
| Team notes: list, create, search, edit, delete | `/notes`, `/notes/[id]` | `GET/POST /api/notes`, `GET /api/notes/search?q=`, `GET/PUT/DELETE /api/notes/{id}` |
| Markdown view of a note (Markdig) | `/notes/[id]?view=markdown` | `POST /api/markdown/render` |
| Public share links | Share card on `/notes/[id]`, public `/share/[token]` | `POST /api/notes/{id}/share`, `GET /api/notes/{id}/shares`, `DELETE /api/notes/{id}/share`, `GET /api/share/{token}` |
| Attachments, link preview | `/notes/[id]`, `/notes` | `GET /api/files?name=`, `GET /api/preview?url=` |
| Profile + personal API keys (`X-Api-Key` header) | `/settings` | `GET/PUT /api/me`, `GET/POST /api/me/api-keys`, `DELETE /api/me/api-keys/{id}` |
| Admin: users + audit log | `/admin`, `/admin/audit` | `GET /api/admin/users`, `GET /api/admin/audit` |

## Run it

```bash
cp .env.example .env
docker compose up -d --build       # web http://localhost:3000 , api http://localhost:8080
```

Switching branches with an existing database volume is fine for the app tables, but the seeded
password hashes differ (MD5 on `main`, PBKDF2 on `solution`): run `docker compose down -v` first.

Training logins (all teams):

| Email | Password | Team | Role |
|---|---|---|---|
| `a@alpha.test` | `alpha-pass-123` | alpha | member |
| `b@beta.test` | `beta-pass-123` | beta | member |
| `admin@alpha.test` | `admin-pass-123` | alpha | admin |

## Tests

```bash
# API integration tests (xUnit + WebApplicationFactory) against a sibling Postgres:
docker compose -f docker-compose.test.yml run --rm tests
docker compose -f docker-compose.test.yml down -v
# only one lab, e.g. the share-link tests:
docker compose -f docker-compose.test.yml run --rm tests \
  dotnet test api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Lab3ShareLink"
# Web helper + XSS tests (Vitest):
cd web && npm ci && npm test
```

On `main` the exploit-regression tests fail (the vulnerabilities are present); on `solution` they all pass.
Positive controls (happy path, "an admin can...", "the owner can share...") pass on both branches.

| Suite | `main` | `solution` |
|---|---|---|
| xUnit (`api.Tests`, 61 tests) | 19 passed / 42 failed | 61 passed |
| Vitest (`web`, 12 tests) | 10 passed / 2 failed | 12 passed |

`Lab6ScaTests` passes on both branches: the test project pulls a newer Newtonsoft.Json transitively,
so the SCA lab is about the API project's own reference (`dotnet list api/Api.csproj package --vulnerable`).

## Scan with SonarQube (local SonarQube Server)

```bash
# 1) start a local SonarQube (Community Build)
SONAR_SEARCH_JAVAADDITIONALOPTS=-Dnode.store.allow_mmap=false \
  docker compose -f docker-compose.sonarqube.yml up -d        # http://localhost:9000 (admin/admin)
# create project "team-notes" + an analysis token in the UI.

# 2) scan with SonarScanner for .NET (needs the SDK + a JRE; install the global tool)
dotnet tool install --global dotnet-sonarscanner
cd web && npm ci && npm run test:coverage && cd ..
dotnet sonarscanner begin /k:"team-notes" \
  /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="api.Tests/coverage/**/coverage.opencover.xml" \
  /d:sonar.javascript.lcov.reportPaths="web/coverage/lcov.info" \
  /d:sonar.exclusions="**/bin/**,**/obj/**,web/node_modules/**,web/.next/**,**/coverage/**"
dotnet test api.Tests/Api.Tests.csproj -c Release \
  --collect:"XPlat Code Coverage" --settings api.Tests/coverlet.runsettings \
  --results-directory "$PWD/api.Tests/coverage"
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

No .NET SDK on your machine? Use the Docker-only commands in the next section.

CI: `.github/workflows/sonarqube.yml` (GitHub) and `.gitlab-ci.yml` (GitLab) do the same with a
quality-gate wait. The scanner for .NET is driven by the `begin/end` arguments (not
`sonar-project.properties`, which MSBuild-based analysis ignores).

> SonarQube **Community Build** has no taint analysis, so it will NOT flag the injection-class bugs
> (SQLi / XSS / path traversal / SSRF / open redirect / Markdown XSS). It *does* flag the weak MD5
> hash (S4790), the hard-coded PostgreSQL password (S2068 / S6698), the cookie flags (S3330 / S2092),
> the share-link `new Random()` (S2245) and `UseDeveloperExceptionPage()` (S4507). It does not see
> the plaintext API keys, the missing login throttle, the secrets in log lines or the missing audit
> trail: those need manual review. Taint analysis is a Developer/Enterprise edition (and SonarQube
> Cloud) feature - that gap is itself a teaching point.

## Fork แล้ว scan ด้วย SonarQube

ใช้ได้ทั้ง SonarQube Cloud และ SonarQube Server ที่ทีมมีอยู่ โดย workflow `.github/workflows/sonarqube.yml`
อ่าน secret/variable จาก fork ของเราเอง ถ้ายังไม่ได้ตั้ง `SONAR_TOKEN` job scan จะถูก skip และ run ยังเป็นสีเขียว
พร้อม notice บอกว่าต้องตั้งค่าอะไร

### 1) Fork repo

1. กด **Fork** ที่หน้า GitHub ของ repo นี้ ติ๊กเอาทุก branch (เอา `Copy the main branch only` ออก) เพื่อให้ได้ทั้ง `main` และ `solution`
2. ใน fork ไปที่แท็บ **Actions** แล้วกด **I understand my workflows, go ahead and enable them** (GitHub ปิด Actions ของ fork ไว้เป็นค่าเริ่มต้น)

### 2) สร้าง project + token ใน SonarQube

**SonarQube Cloud**

1. Login ที่ https://sonarcloud.io ด้วย GitHub แล้วสร้างหรือเลือก organization (จด **Organization Key** ไว้)
2. **Analyze new project** แล้วเลือก repo ที่ fork มา จะได้ Project Key แบบ `<owner>_<repo>` ซึ่งตรงกับค่า default ของ workflow
3. ที่ project ไปที่ **Administration > Analysis Method** แล้วปิด **Automatic Analysis** (เราใช้ CI-based analysis เพราะต้อง build C# และส่ง coverage)
4. สร้าง token ที่ **My Account > Security > Generate Tokens**

**SonarQube Server** (ของบริษัท หรือ local จาก `docker-compose.sonarqube.yml`)

1. **Create Project > Local project** ตั้ง Project Key เช่น `team-notes`
2. สร้าง token ที่ **My Account > Security** (ชนิด Project Analysis Token ของ project นั้น หรือ Global Analysis Token)
3. GitHub-hosted runner ต้องเรียก URL ของ Server ได้ ถ้า Server อยู่ใน network ภายในให้ใช้ self-hosted runner หรือ scan แบบ local (ข้อ 5)

### 3) ตั้ง secrets / variables ใน fork

ไปที่ **Settings > Secrets and variables > Actions**

| ชื่อ | ชนิด | ใช้เมื่อ | ค่า |
|---|---|---|---|
| `SONAR_TOKEN` | Secret | ทุกครั้ง (ไม่มี = skip) | token จากข้อ 2 |
| `SONAR_HOST_URL` | Secret หรือ Variable | SonarQube Server | เช่น `https://sonarqube.example.com` (ไม่ตั้ง = `https://sonarcloud.io`) |
| `SONAR_ORGANIZATION` | Variable หรือ Secret | SonarQube Cloud | Organization Key |
| `SONAR_PROJECT_KEY` | Variable | เมื่อ key ไม่ใช่ `<owner>_<repo>` | เช่น `team-notes` |
| `SONAR_QUALITYGATE_WAIT` | Variable | ไม่บังคับ | `false` = ไม่ให้ job fail เมื่อ Quality Gate แดง (default `true`) |

ห้าม commit token ลงใน repo ใส่เป็น secret เท่านั้น

### 4) Run workflow

- push commit ใดก็ได้ไปที่ `main` หรือ `solution` หรือเปิด Pull Request หรือไปที่ **Actions > SonarQube > Run workflow**
- workflow จะ `npm ci` + Vitest (lcov) ของ `web`, เปิด PostgreSQL 17 เป็น service container แบบเดียวกับ `docker-compose.test.yml`,
  แล้ว `dotnet sonarscanner begin` > `dotnet build` > `dotnet test` (coverlet OpenCover) > `dotnet sonarscanner end`
- test ที่ fail บน `main` เป็นความตั้งใจ step test ตั้ง `continue-on-error` ไว้เพื่อให้ scan และ coverage ยังเข้า SonarQube
- `sonar.qualitygate.wait=true` ทำให้ job แดงเมื่อ Quality Gate ไม่ผ่าน (scan แรกของ project มักผ่าน เพราะ Sonar way
  ตัดสินจาก New Code เท่านั้น ทั้งที่ Security rating เป็น E ซึ่งเป็นประเด็นของ lab เรื่อง gate)

### 5) ดูผล

- หน้า run ใน Actions: step `SonarQube end` พิมพ์ลิงก์ dashboard และสถานะ Quality Gate
- SonarQube: เปิด project แล้วดู **Issues** (กรอง Software Quality = Security), **Security Hotspots** (ถ้า version นั้นยังแยกแสดง),
  **Measures > Coverage** (C# จาก OpenCover, TypeScript จาก lcov)
- scan ทั้ง `main` และ `solution` แล้วเทียบกัน: Community Build วิเคราะห์ได้แค่ branch เดียวต่อ project
  (branch analysis มีใน Server ตั้งแต่ Developer Edition และใน SonarQube Cloud ตาม plan) ถ้าใช้ไม่ได้ให้แยก project ต่อ branch
  เช่นตั้ง `SONAR_PROJECT_KEY` เป็น `team-notes-solution` ก่อน run บน `solution`

### Scan แบบ local ด้วย Docker (ไม่ต้องมี CI และไม่ต้องติดตั้ง .NET SDK)

ใช้ image `mcr.microsoft.com/dotnet/sdk:10.0` ตัวเดียวกับ `docker-compose.test.yml` และ PostgreSQL ของ test compose

```bash
export SONAR_TOKEN=<token>
export SONAR_HOST_URL=http://sonarqube:9000     # local Server จาก docker-compose.sonarqube.yml
export SONAR_PROJECT_KEY=team-notes
export SONAR_ORGANIZATION=                      # SonarQube Cloud เท่านั้น
SONAR_NET="--network sonarqube-lab_default"     # เฉพาะ local Server (ให้ container เห็น host ชื่อ sonarqube)
# SonarQube Cloud: SONAR_HOST_URL=https://sonarcloud.io , SONAR_ORGANIZATION=<org key> , SONAR_NET=
# Server อื่น: SONAR_HOST_URL=<URL จริง> , SONAR_NET=

# 1) web coverage (lcov); node_modules อยู่ใน anonymous volume จึงไม่ทับ node_modules ของเครื่องเรา
docker run --rm -v "$PWD/web:/web" -v /web/node_modules -w /web node:24-alpine \
  sh -c "npm ci && (npm run test:coverage || true); chown -R $(id -u):$(id -g) coverage"

# 2) PostgreSQL สำหรับ xUnit
docker compose -f docker-compose.test.yml up -d test-db

# 3) begin > build > test > end ใน .NET SDK container (scanner ดาวน์โหลด JRE จาก SonarQube เอง)
docker run --rm --network team-notes-test_default $SONAR_NET \
  -v "$PWD:/src" -w /src \
  -e SONAR_TOKEN -e SONAR_HOST_URL -e SONAR_PROJECT_KEY -e SONAR_ORGANIZATION \
  -e DATABASE_URL=postgres://notes:notes@test-db:5432/notes \
  -e SESSION_SECRET=test-session-secret-0123456789-not-the-committed-one \
  -e ASPNETCORE_ENVIRONMENT=Development -e UPLOADS_DIR=/src/uploads -e HOST_UID="$(id -u):$(id -g)" \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c '
    set -e
    # container รันเป็น root: คืน owner ของไฟล์ที่ build/scan สร้างให้ user เราเมื่อจบ
    trap "chown -R $HOST_UID /src/api /src/api.Tests /src/.sonarqube 2>/dev/null || true" EXIT
    dotnet tool install --global dotnet-sonarscanner && export PATH="$PATH:$HOME/.dotnet/tools"
    org=(); [ -n "$SONAR_ORGANIZATION" ] && org=(/o:"$SONAR_ORGANIZATION")
    dotnet sonarscanner begin /k:"$SONAR_PROJECT_KEY" "${org[@]}" \
      /d:sonar.host.url="$SONAR_HOST_URL" /d:sonar.token="$SONAR_TOKEN" \
      /d:sonar.cs.opencover.reportsPaths="api.Tests/coverage/**/coverage.opencover.xml" \
      /d:sonar.javascript.lcov.reportPaths="web/coverage/lcov.info" \
      /d:sonar.exclusions="**/bin/**,**/obj/**,web/node_modules/**,web/.next/**,**/coverage/**"
    dotnet build api.Tests/Api.Tests.csproj -c Release
    dotnet test api.Tests/Api.Tests.csproj -c Release --no-build \
      --collect:"XPlat Code Coverage" --settings api.Tests/coverlet.runsettings \
      --results-directory /src/api.Tests/coverage || true
    dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"'

# 4) เก็บกวาด
docker compose -f docker-compose.test.yml down -v
```

`docker-compose.sonarqube.yml` เปิด port 9000 ไว้ที่ `127.0.0.1` ของ host เท่านั้น container อื่นจึงต้องต่อเข้า network
`sonarqube-lab_default` แล้วเรียกชื่อ service `sonarqube` แทน `localhost` (การใส่ `--network` 2 ครั้งต้องใช้ Docker Engine 25 ขึ้นไป)

## Lab map

| Lab | Vulnerability | Where (main) | Language |
|---|---|---|---|
| Day 1 auth | Unsalted MD5 password hash | `api/Auth.cs` `HashPassword` | C# |
| Day 1 auth | JWT signature/expiry not validated (forgeable) | `api/Auth.cs` `ValidateSessionToken` | C# |
| Day 1 auth | Session cookie without HttpOnly/SameSite/Secure | `api/Endpoints/AuthEndpoints.cs` | C# |
| Day 1 auth | Token mirrored into `localStorage` (discussion) | `web/components/TokenStash.tsx` | TS |
| Lab 2 | SQL injection in note search | `api/Endpoints/NotesEndpoints.cs` `SearchAsync` | C# |
| Lab 2 | Stored XSS (`dangerouslySetInnerHTML`) | `web/app/notes/[id]/NoteBody.tsx` | TS |
| Lab 2 | Path traversal in attachment download | `api/Endpoints/FilesEndpoints.cs` | C# |
| Lab 2 | SSRF in link preview | `api/Endpoints/PreviewEndpoints.cs` | C# |
| Lab 2 | Open redirect via `?next=` | `api/Endpoints/AuthEndpoints.cs` | C# |
| Lab 2 | Hard-coded secrets (fake) | `api/AppConfig.cs`, `api/appsettings.json` | C# / JSON |
| Lab 3 | Share link: predictable token (`new Random(noteId)`), no expiry, revocation not enforced, no owner check | `api/Endpoints/ShareEndpoints.cs` | C# |
| Lab 3 | Login brute force: no rate limiting / lockout | `api/Endpoints/AuthEndpoints.cs` `LoginAsync` | C# |
| Lab 3 | API keys stored in plaintext, listed back, compared with `==` | `api/ApiKeys.cs`, `api/Endpoints/ApiKeyEndpoints.cs` | C# |
| Lab 4 | BOLA / IDOR on `/api/notes/{id}` | `api/Endpoints/NotesEndpoints.cs` | C# |
| Lab 4 | BFLA on `/api/admin/users` | `api/Endpoints/AdminEndpoints.cs` | C# |
| Lab 4 | Mass assignment | `api/Endpoints/MeEndpoints.cs`, `NotesEndpoints.cs` | C# |
| Lab 5 | Markdown XSS through a library (raw HTML, `javascript:` links, generic attributes) | `api/MarkdownRenderer.cs` > `web/components/MarkdownBody.tsx` | C# > TS |
| Lab 6 | Vulnerable dependency (`Newtonsoft.Json` 12.0.3) | `api/Api.csproj` | C# |
| Lab 7 | Password, session token and API key written to the log; no audit trail | `api/Endpoints/AuthEndpoints.cs`, `ApiKeyEndpoints.cs` | C# |
| Lab 7 | Stack traces returned to clients (`UseDeveloperExceptionPage` everywhere), no security headers | `api/Program.cs` | C# |

`dotnet list package --vulnerable` reports the Newtonsoft.Json advisory (GHSA-5crp-9r3c-p9vr).

### New labs at a glance (CWE / OWASP Top 10:2025, fix on `solution`, tests)

| Lab id | CWE | OWASP | Fix commit on `solution` | Tests (`api.Tests/Security`) |
|---|---|---|---|---|
| `lab3-share-link` | CWE-330/338, CWE-613, CWE-639 | A01, A04, A07 | 32-byte `RandomNumberGenerator` token, expiry (24 h default, 7 days max), revocation enforced, owner/admin + team check | `Lab3ShareLinkTests` |
| `lab3-login-throttle` | CWE-307 | A07 | fixed-window limiter (`System.Threading.RateLimiting`) per IP + email on failed sign-ins: 5 per minute, then 429 + `Retry-After` | `Lab3LoginThrottleTests` |
| `lab3-api-keys` | CWE-256/312, CWE-208 | A04, A07 | store SHA-256 of the key, show it once, `CryptographicOperations.FixedTimeEquals` | `Lab3ApiKeyTests` |
| `lab5-markdown-xss` | CWE-79 | A05 | Markdig `DisableHtml()`, explicit extensions (no generic attributes), link scheme allow-list | `Lab5MarkdownXssTests` |
| `lab7-audit-log` | CWE-532, CWE-778 | A09 | `AuditLog` service writes `audit_log` rows + structured log events without secrets | `Lab7AuditLogTests` |
| `lab7-error-handling` | CWE-209, CWE-1021 | A02, A10 | `UseExceptionHandler` + ProblemDetails (traceId only), `nosniff` / `DENY` / `no-referrer` / CSP headers | `Lab7ErrorHandlingTests` |

Exploit quick checks against `docker compose up` (`main` vs `solution`):

```bash
# Lab 3 share link: the token of note N is new Random(N).Next() in hex (8 chars) on main
docker run --rm mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'printf "Console.WriteLine(new Random(3).Next().ToString(\"x8\"));" > t.cs && dotnet run t.cs'
curl -s http://localhost:8080/api/share/<token>         # main: 200 once note 3 was shared; solution: 404
# Lab 3 brute force: the 6th wrong password in a minute
for i in $(seq 1 8); do curl -s -o /dev/null -w "%{http_code} " \
  -d "email=a@alpha.test&password=guess$i" http://localhost:8080/api/auth/login; done   # main: 303 x8; solution: 303 x5, 429 x3
# Lab 7 errors: malformed JSON
curl -s -b "session=$TOKEN" -H 'content-type: application/json' -d '{"title":' http://localhost:8080/api/notes
```
