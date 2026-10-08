# Team Notes - SonarQube Developer Training (hands-on starter)

A deliberately vulnerable "team notes" app for the 2-day **SonarQube Developer Training**.
Backend is **ASP.NET Core Web API (C#, .NET 10)**, frontend is **Next.js (TypeScript)**,
data in **PostgreSQL** (raw SQL via Npgsql + Dapper). Everything runs with Docker Compose.

> ⚠️ Every vulnerability here is intentional and every secret is **fake**. Do not deploy this.

## Branches & tags

| Ref | What |
|---|---|
| `main` | Vulnerable starter (the labs find these) |
| `solution` | One fix commit per lab id (`day1-auth` / `lab2-*` / `lab4-*` / `lab6-sca`) + tests |
| `nextjs-only-main` / `nextjs-only-solution` (tags) | The earlier Next.js-only version of this app |

## Run it

```bash
cp .env.example .env
docker compose up -d --build       # web http://localhost:3000 , api http://localhost:8080
```

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
# Web helper + XSS tests (Vitest):
cd web && npm ci && npm test
```

On `main` the security tests fail (the vulnerabilities are present); on `solution` they all pass.

## Scan with SonarQube

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

CI: `.github/workflows/sonarqube.yml` (GitHub) and `.gitlab-ci.yml` (GitLab) do the same with a
quality-gate wait. The scanner for .NET is driven by the `begin/end` arguments (not
`sonar-project.properties`, which MSBuild-based analysis ignores).

> SonarQube **Community Build** has no taint analysis, so it will NOT flag the injection-class bugs
> (SQLi / XSS / path traversal / SSRF / open redirect). It *does* flag the weak MD5 hash and the
> hard-coded PostgreSQL password. Taint analysis is a Developer/Enterprise edition feature - that
> gap is itself a teaching point. See `APP_SPEC.md` for the full detected-vs-missed table.

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
| Lab 4 | BOLA / IDOR on `/api/notes/{id}` | `api/Endpoints/NotesEndpoints.cs` | C# |
| Lab 4 | BFLA on `/api/admin/users` | `api/Endpoints/AdminEndpoints.cs` | C# |
| Lab 4 | Mass assignment | `api/Endpoints/MeEndpoints.cs`, `NotesEndpoints.cs` | C# |
| Lab 6 | Vulnerable dependency (`Newtonsoft.Json` 12.0.3) | `api/Api.csproj` | C# |

`dotnet list package --vulnerable` reports the Newtonsoft.Json advisory (GHSA-5crp-9r3c-p9vr).
