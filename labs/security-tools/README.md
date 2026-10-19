# Security tools: complementary measurements

Start the ZAP exercise from `zap-evidence-gaps.md`: sample source evidence with
three open HTTP questions. Pick one gap and write its target and pass/fail
predicate before you scan; it does not depend on any earlier workshop's output.
These tool examples measure HTTP responses, Dockerfile policy, a team-written AST
rule and a live TLS fixture. They do not repeat Sonar project setup, dependency
review or another application repair/commit/rescan loop. TLS/Dockerfile
observations do not prove Markdown behavior or package/build identity.

The files are included in the Team Notes GitHub repository. Start from its existing root:

```bash
TRAINING_ROOT="$PWD"
LAB_FIXTURES="$TRAINING_ROOT/labs/security-tools"
TOOLS_DIR=$(mktemp -d "${TMPDIR:-/tmp}/security-tools.XXXXXX")
```

The complete executable recipes are in the course Lab and source Workshop 12.
Prepare the tools before the exercise: ZAP's official stable Docker channel
(record its resolved digest), Trivy0.75.0, Semgrep CE1.180.0, Bash/OpenSSL3 and
testssl.sh3.2.4. The sample command versions were checked against primary docs.
Do not silently replace them with a development branch.

- **ZAP:** join the actual web service's Docker network and target only the
  authorized training `/login` surface. Container127.0.0.1 is not the host.
  This baseline is unauthenticated and passive; its alerts/PASS instances do
  not validate authenticated API or Markdown behavior. Codes0/1/2/3 describe
  the packaged scan result; Docker/connectivity errors are setup failures.
  Only a newly created output directory is mounted, never the Docker socket.
- **Trivy:** copy only permitted app Dockerfiles plus `config/bad` and
  `config/good` into a temporary directory. Run the Dockerfile misconfiguration
  scanner, not package/CVE scanning. The fixtures differ only in USER; they are
  not built or deployed. Read the emitted stage/check/location, not just total
  finding counts or exit0. A checks-bundle download may be required.
- **Semgrep:** `semgrep/auth-header-log.yml` and `.js` form the paired local
  rule/corpus. First narrow the rule to console.log and observe the console.warn
  expectation fail; restore the supplied `$METHOD` pattern without deleting
  annotations. Run `semgrep --test` and local `scan` with metrics off. The alias
  case's `todoruleid` annotation records a known false negative, not safety.
  Never execute the JavaScript fixture; it contains no actual credential.
- **TLS:** `run-tls-fixture.sh` accepts the pinned testssl folder and a fresh
  output directory. It creates a unique self-signed key/cert under `/tmp`,
  starts its own loopback19443 TLS1.2-only server, checks readiness against its
  own certificate, and cleans its PID/keys on EXIT/INT/TERM. If the port is
  occupied, stop instead of testing someone else's listener. Keys do not enter
  the output folder. The measured protocol/certificate facts apply only to the
  temporary fixture. Exit0 is completion, not a secure-production verdict.

## Verification recorded for this teaching set

Isolated trials used Semgrep1.180.0, Trivy0.75.0 and testssl.sh3.2.4. Semgrep's
paired test passed and its fixture scan had two direct findings. Trivy's bad
fixture emitted root-user DS-0002 and missing-HEALTHCHECK DS-0026; the good fixture
removed the targeted root check while the other remained. TLS measured1.2
support,1.3 absence and the self-signed chain issue; hostname matching is a
separate field. ZAP was not executed during this revision; its recipe is
primary-doc verified and the class must produce its own report.

Keep version, target identity, scope and setup errors with each observation.
Neither a missing alert nor a successful tool exit establishes full security.
