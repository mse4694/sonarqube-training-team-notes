# Team Notes: SonarQube Developer Training starter

> ⚠️ **Deliberately vulnerable app สำหรับ workshop เท่านั้น** ห้าม deploy ขึ้น server จริง ห้ามเปิด port ออก internet.
> secret ทุกตัวใน repo นี้เป็นค่า **ปลอม** (เช่น `AKIAIOSFODNN7EXAMPLE` คือ key ตัวอย่างจาก AWS docs)

แอปจดโน้ตแบบ multi-tenant ขนาดเล็ก: ผู้ใช้อยู่ใน team (tenant) และมี role `member` / `admin`

- **Stack:** Next.js 16 (App Router, TypeScript, Route Handlers + Server Actions) · PostgreSQL 17 · `pg` (raw SQL) · `jose` (session JWT) · Vitest + PGlite (Postgres ใน WASM สำหรับ test)
- **Branches:**
  - `main` = starter ที่มีช่องโหว่ฝังไว้ (สำหรับ lab)
  - `solution` = แก้ทีละ lab (1 commit ต่อ 1 fix, ขึ้นต้นด้วย lab id) + exploit-regression tests

## Setup

```bash
# 1) app + db ด้วย Docker
docker compose up -d --build
open http://localhost:3000            # login หน้า /login

# 2) หรือรันแบบ dev (ต้องมี db จากข้อ 1 อยู่ที่ localhost:5433)
cp .env.example .env
npm ci
npm run dev

# tests (ไม่ต้องใช้ Docker: ใช้ PGlite in-memory)
npm test
npm run test:coverage                 # -> coverage/lcov.info สำหรับ SonarQube
```

บัญชี seed (`db/init.sql`):

| User | Email | Password | Team | Role |
|---|---|---|---|---|
| User A | `a@alpha.test` | `alpha-pass-123` | alpha (id 1) | member |
| User B | `b@beta.test` | `beta-pass-123` | beta (id 2) | member |
| Admin | `admin@alpha.test` | `admin-pass-123` | alpha (id 1) | admin |

Notes: id 1, 2 (A, alpha) · id 3, 4 (B, beta) · id 5 (Admin, alpha)

## Lab map (branch `main`)

| Lab | ช่องโหว่ | ไฟล์ : บรรทัด | Fix บน `solution` |
|---|---|---|---|
| Day 1 auth | Weak password hashing (unsalted MD5) | `lib/auth.ts:17`, `db/init.sql` (seed `md5()`) | `@node-rs/argon2` (argon2id) |
| Day 1 auth | "Remember me" JWT แค่ `jwt.decode()` ไม่ verify (ปลอม token / `alg: none` ได้) | `lib/auth.ts:41` | `jwt.verify(..., { algorithms: ['HS256'] })` |
| Day 1 auth | Session cookie `httpOnly: false`, ไม่มี `SameSite`/`Secure` | `app/api/auth/login/route.ts:35` | `httpOnly`, `sameSite: 'lax'`, `secure` |
| Lab 2 taint | SQL injection (string-concatenated SQL) | `app/api/notes/search/route.ts:12` | parameterized query |
| Lab 2 taint | Stored XSS ผ่าน `dangerouslySetInnerHTML` | `app/notes/[id]/NoteBody.tsx:3` | render เป็น text |
| Lab 2 taint | Path traversal (`/api/files?name=../...`) | `app/api/files/route.ts:14` | resolve + ตรวจว่าอยู่ใน `uploads/` |
| Lab 2 taint | SSRF (`/api/preview?url=` -> `fetch(userUrl)`) | `app/api/preview/route.ts:12` | allow http(s) + block private/loopback/link-local IP |
| Lab 2 taint | Open redirect (`/login?next=https://evil`) | `app/api/auth/login/route.ts:33`, `app/login/page.tsx:9` | รับเฉพาะ relative path |
| Lab 2 secrets | Hard-coded secrets (DB password ใน connection string, JWT secret, AWS key) | `lib/config.ts:5-18` | อ่านจาก env + `.env.example` |
| Lab 4 manual | BOLA/IDOR: `GET/PATCH/DELETE /api/notes/[id]` ไม่เช็ค tenant/owner | `app/api/notes/[id]/route.ts:12,21,30`, `app/notes/[id]/page.tsx` | scope ด้วย `team_id` + owner/admin |
| Lab 4 manual | BFLA: `/api/admin/users` ซ่อนแค่ปุ่มใน UI แต่ route ไม่เช็ค role | `app/api/admin/users/route.ts:7-11` | `role === 'admin'` -> ไม่งั้น 403 |
| Lab 4 manual | Mass assignment: PATCH ส่ง `{...body}` (เปลี่ยน `team_id`/`owner_id`/`role` ได้) | `app/api/notes/[id]/route.ts:21`, `app/api/me/route.ts:19` | allowlist field |
| Lab 6 SCA | `jsonwebtoken@8.5.1` (GHSA-8cf7-32gw-wr33, GHSA-hjrf-2m68-5959, GHSA-qwph-4952-7xr6 / CVE-2022-23539/23541/23540) ใช้จริงใน `lib/auth.ts` | `package.json:19` | upgrade เป็น 9.x |

ดูผลว่า SonarQube Community Build เจออะไร/พลาดอะไรได้ในเอกสาร course (`APP_SPEC.md`)

## Scan ด้วย SonarQube (local)

```bash
# 1) SonarQube Community Build + Postgres
#    Linux host ต้องมี vm.max_map_count >= 262144 (ดู comment ใน docker-compose.sonarqube.yml)
docker compose -f docker-compose.sonarqube.yml up -d
#    http://localhost:9000  -> admin/admin -> เปลี่ยนรหัส -> Create project "team-notes" -> สร้าง token

# 2) test + coverage
npm ci && npm run test:coverage

# 3) scan (scanner อ่าน sonar-project.properties)
docker run --rm --network host \
  -e SONAR_HOST_URL=http://localhost:9000 \
  -e SONAR_TOKEN=<your-token> \
  -v "$PWD:/usr/src" sonarsource/sonar-scanner-cli
```

CI: `.github/workflows/sonarqube.yml` (GitHub Actions: test + coverage -> `SonarSource/sonarqube-scan-action@v8` + รอ Quality Gate) และ `.gitlab-ci.yml` (optional)
ตั้ง `SONAR_TOKEN` (secret) และ `SONAR_HOST_URL` (variable) ใน repo settings

> Community Build **ไม่มี taint analysis** (SQLi/XSS/SSRF/path traversal data-flow) และไม่มี branch analysis. ใน class ใช้ Developer/Enterprise edition (`sonarqube:enterprise` + license) เพื่อดู data-flow ของ Lab 2

## Exploit ตัวอย่าง (main)

```bash
# login เป็น User A แล้วเก็บ cookie
curl -s -c a.txt -o /dev/null -d 'email=a@alpha.test&password=alpha-pass-123' http://localhost:3000/api/auth/login

# SQLi: อ่านโน้ตของ team beta
curl -s -b a.txt "http://localhost:3000/api/notes/search?q=zzz')%20OR%201=1%20--"

# IDOR: User A อ่านโน้ต id 3 ของ User B
curl -s -b a.txt http://localhost:3000/api/notes/3

# BFLA: member เรียก admin API
curl -s -b a.txt http://localhost:3000/api/admin/users
```
