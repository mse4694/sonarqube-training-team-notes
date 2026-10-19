# Sample evidence gaps for the ZAP exercise

A source review of Team Notes left three open questions that only the HTTP
response can answer. Pick ONE gap, write its target and pass/fail predicate
before you scan, then use the ZAP report to decide: answered (pass or fail) or
still inconclusive, and why.

This is provided sample evidence from reading the source on `main`. It is not a
ZAP result and it does not say what ZAP will report.

## Evidence

| ID | Type | Source | What it shows |
| --- | --- | --- | --- |
| E1 | source | `web/next.config.ts` | The config defines `output` and `rewrites()` only: no `headers()` and no `poweredByHeader: false`. |
| E2 | source | `web/` (repository root of the web app) | There is no `middleware.ts` or `proxy.ts` that could add response headers. |
| E3 | source | `web/app/login/page.tsx` | The login form posts to `/api/auth/login`; its only hidden input is `next`. |
| E4 | scope | `docker-compose.yml` | The training stack has no reverse proxy or CDN in front of `web`, so no other layer adds headers here. |

Missing evidence: nobody has requested the running app yet. The source alone
cannot show the headers Next.js actually sends.

## Gaps (choose one)

### G1: Browser security headers on the login page

- Question: does the login page response tell the browser to block framing,
  MIME sniffing and unlisted script sources?
- Based on: E1, E2, E4.
- Target: `GET http://web:3000/login` without a session.
- Pass: the response has `Content-Security-Policy` (or at least `frame-ancestors`
  in it, or `X-Frame-Options`) AND `X-Content-Type-Options: nosniff`.
- Fail: any of those headers is absent on that response.
- Not answered by this scan: pages behind login and the API responses.

### G2: Technology disclosure

- Question: does the web app advertise its framework in a response header?
- Based on: E1.
- Target: responses from `http://web:3000/` that the scan reaches.
- Pass: no `X-Powered-By` header on those responses.
- Fail: `X-Powered-By` is present; record its value and the URL.
- Not answered by this scan: version disclosure inside HTML or JavaScript files.

### G3: Anti-CSRF token on the login form

- Question: does the login form carry an anti-CSRF token?
- Based on: E3.
- Target: the HTML of `GET http://web:3000/login`.
- Pass: the form has a hidden field that holds a per-session random token.
- Fail: the form has no such field.
- Not answered by this scan: whether a cross-site login request actually
  succeeds (that needs an active test and the cookie's SameSite behavior), so a
  missing token alone is not proof of an exploitable CSRF.

## What to write after the scan

For the chosen gap: the ZAP result (alert name and ID, or the PASS rule), the URL
and the header or HTML it is based on, your decision (answered pass, answered
fail, or inconclusive) and the part the scan could not reach.
