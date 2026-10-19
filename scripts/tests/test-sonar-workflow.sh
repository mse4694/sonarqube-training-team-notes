#!/usr/bin/env bash
# Execute the actual SonarQube workflow steps with a stub `dotnet`, without network or secrets.
#   bash scripts/tests/test-sonar-workflow.sh
# No `set -e`: every case runs and is counted; setup failures exit explicitly.
set -uo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd -P)
workflow="$root/.github/workflows/sonarqube.yml"
work=$(mktemp -d) || exit 1
trap 'rm -rf -- "$work"' EXIT
passed=0
failed=0

# The `run: |` body of a step, without its 10-space YAML indentation.
step_body() {  # $1 = line that opens the step, $2 = line that ends the body
  awk -v open="$1" -v stop="$2" '
    $0 == open { inside = 1; next }
    inside && /^        run: \|$/ { body = 1; next }
    body && $0 ~ stop { exit }
    body { sub(/^          /, ""); print }' "$workflow"
}
step_body '      - name: SonarQube begin' '^      - name: Build' > "$work/begin.sh"
step_body '      - id: check' '^  build-test-scan:' > "$work/check.sh"
[[ -s "$work/begin.sh" && -s "$work/check.sh" ]] || { echo "could not extract the workflow steps" >&2; exit 1; }

mkdir "$work/bin" || exit 1
printf '#!/bin/bash\nprintf "%%s\\n" "$@" > "$CAPTURE"\n' > "$work/bin/dotnet"
chmod 700 "$work/bin/dotnet"

check() {  # $1 = name, $2 = 0 if the assertion held
  if [[ "$2" == 0 ]]; then passed=$((passed + 1)); echo "PASS $1"; else failed=$((failed + 1)); echo "FAIL $1"; fi
}

# Run the scanner-begin step; the captured arguments are one per line in $work/args.
run_begin() {
  local out
  out=$(env PATH="$work/bin:$PATH" CAPTURE="$work/args" SONAR_PROJECT_KEY=test SONAR_HOST_URL=https://sonarcloud.io \
    SONAR_TOKEN=fake-test-token SONAR_ORGANIZATION=test-org SONAR_BRANCH_ANALYSIS=false SONAR_QUALITYGATE_WAIT=true \
    SONAR_PR_NUMBER= SONAR_PR_HEAD= SONAR_PR_BASE= GITHUB_REF_NAME=main "$@" bash -euc "$(cat "$work/begin.sh")")
  [[ "$out" != *fake-test-token* ]]  # the token never reaches the log
}
has() { grep -Fxq -- "$1" "$work/args"; }
has_prefix() { grep -q -- "^$1" "$work/args"; }

run_begin && has '/d:sonar.branch.name=main'
check "cloud main is explicit" $?
run_begin GITHUB_REF_NAME=solution && has '/d:sonar.branch.name=solution' && ! has '/d:sonar.branch.name=main'
check "dispatch on solution is not main" $?
run_begin SONAR_PR_NUMBER=1 SONAR_PR_HEAD=demo/insecure SONAR_PR_BASE=main \
  && has '/d:sonar.pullrequest.key=1' && has '/d:sonar.pullrequest.branch=demo/insecure' \
  && has '/d:sonar.pullrequest.base=main' && ! has_prefix '/d:sonar.branch.name='
check "PR scope does not mix branch scope" $?
run_begin SONAR_ORGANIZATION= SONAR_HOST_URL=http://localhost:9000 && ! has_prefix '/d:sonar.branch.name='
check "community omits unsupported scope" $?
run_begin SONAR_ORGANIZATION= SONAR_BRANCH_ANALYSIS=true GITHUB_REF_NAME=solution && has '/d:sonar.branch.name=solution'
check "server branch analysis is opt-in" $?
run_begin GITHUB_REF_NAME='feature/space;echo example' && has '/d:sonar.branch.name=feature/space;echo example'
check "branch is one literal argument" $?

# Run the `check` step; it writes enabled=true|false to GITHUB_OUTPUT.
run_check() {  # $1 event, $2 organization, $3 host, $4 branch support, $5 ref, $6 expected
  rm -f "$work/out"
  env SONAR_TOKEN=fake GITHUB_EVENT_NAME="$1" SONAR_ORGANIZATION="$2" SONAR_HOST_URL="$3" SONAR_BRANCH_ANALYSIS="$4" \
    GITHUB_REF_NAME="$5" SONAR_DEFAULT_BRANCH=main GITHUB_OUTPUT="$work/out" GITHUB_STEP_SUMMARY="$work/summary" \
    bash -euc "$(cat "$work/check.sh")" > /dev/null && [[ "$(tr -d '[:space:]' < "$work/out")" == "enabled=$6" ]]
}
run_check pull_request '' https://internal-sonar.example false feature/test false
check "PR without branch support is skipped" $?
run_check pull_request test-org https://sonarcloud.io false feature/test true
check "PR on SonarCloud runs" $?
run_check pull_request '' https://internal-sonar.example true feature/test true
check "PR with server branch support runs" $?
run_check workflow_dispatch '' https://internal-sonar.example false main true
check "community dispatch on the default branch runs" $?
run_check workflow_dispatch '' https://internal-sonar.example false solution false
check "community dispatch on another branch is skipped" $?

echo "$passed passed, $failed failed"
(( failed == 0 ))
