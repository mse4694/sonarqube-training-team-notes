#!/usr/bin/env bash
# Usage: bash run-tls-fixture.sh /path/to/testssl-v3.2.4 /path/to/output
# Creates private temporary keys outside the repository. Own local server only.
# Requires: bash, OpenSSL 3 (req/s_server) and curl for the readiness check.
set -euo pipefail
ssl_tool_dir=${1:?provide pinned testssl.sh folder}
result_dir=${2:?provide output directory}
umask 077
mkdir -p "$result_dir"
result_dir=$(cd "$result_dir" && pwd)
ssl_tool_dir=$(cd "$ssl_tool_dir" && pwd)
umask 077
tls_fixture_dir=$(mktemp -d /tmp/training-tls.XXXXXX)
tls_fixture_pid=''
cleanup() {
  if [ -n "$tls_fixture_pid" ]; then
    kill "$tls_fixture_pid" 2>/dev/null || true
    wait "$tls_fixture_pid" 2>/dev/null || true
  fi
  rm -rf "$tls_fixture_dir"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
openssl version | tee "$result_dir/openssl-version.txt"
openssl req -x509 -newkey rsa:2048 -nodes -days 1 \
  -subj '/CN=localhost' \
  -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1' \
  -keyout "$tls_fixture_dir/key.pem" -out "$tls_fixture_dir/cert.pem" \
  >"$tls_fixture_dir/cert-generation.log" 2>&1
openssl s_server -accept 127.0.0.1:19443 -tls1_2 -www \
  -cert "$tls_fixture_dir/cert.pem" -key "$tls_fixture_dir/key.pem" \
  >"$tls_fixture_dir/server.log" 2>&1 &
tls_fixture_pid=$!
ready=0
for attempt in 1 2 3 4 5; do
  if ! kill -0 "$tls_fixture_pid" 2>/dev/null; then
    echo 'Own TLS fixture failed to bind; do not scan a pre-existing listener.' >&2
    exit 1
  fi
  if curl --silent --cacert "$tls_fixture_dir/cert.pem" --max-time 2 'https://127.0.0.1:19443/' >/dev/null; then
    ready=1
    break
  fi
  sleep 1
done
if [ "$ready" != 1 ]; then
  echo 'Own fixture not ready.' >&2
  exit 1
fi
# Target is the disposable server above, never an existing app/production target.
set +e
bash "$ssl_tool_dir/testssl.sh" --warnings batch --color 0 \
  --nodns none --ip 127.0.0.1 --protocols --server-defaults \
  --jsonfile-pretty "$result_dir/tls.json" 'localhost:19443' \
  >"$result_dir/testssl-console.txt" 2>&1
scan_status=$?
set -e
printf '%s\n' "$scan_status" > "$result_dir/testssl-exit.txt"
echo "testssl exit=$scan_status; inspect selected-check JSON and console separately"
exit "$scan_status"
