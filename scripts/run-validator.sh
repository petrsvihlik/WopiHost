#!/usr/bin/env bash
# Run the Microsoft WOPI validator against the WopiHost.Validator sample.
# Mirrors what .github/workflows/wopi-validator.yml does, for local iteration.
#
# Usage:
#   scripts/run-validator.sh                         # WopiCore tests, validator main branch
#   scripts/run-validator.sh OfficeOnline            # different test category
#   VALIDATOR_REF=v2.0.5 scripts/run-validator.sh    # pin validator version
#
# Requirements: bash, curl, git, dotnet (8.0 + 10.0 SDKs).

set -euo pipefail

TEST_CATEGORY="${1:-WopiCore}"
VALIDATOR_REPO="${VALIDATOR_REPO:-microsoft/wopi-validator-core}"
VALIDATOR_REF="${VALIDATOR_REF:-main}"
HOST_URL="${HOST_URL:-http://localhost:5000}"
WOPI_FILE_ID="${WOPI_FILE_ID:-WOPITEST}"
WOPI_ACCESS_TOKEN="${WOPI_ACCESS_TOKEN:-Anonymous}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Cache and validator source live outside the repo so the host's
# Directory.Packages.props (Central Package Management) doesn't apply
# to the validator's restore.
CACHE_ROOT="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/wopi-validator-cache"
VALIDATOR_SRC="${CACHE_ROOT}/wopi-validator-core"
VALIDATOR_BIN="${CACHE_ROOT}/bin"
HOST_LOG="${REPO_ROOT}/host.log"
VALIDATOR_LOG="${REPO_ROOT}/validator.log"
HOST_PID=""

cleanup() {
  if [[ -n "${HOST_PID}" ]] && kill -0 "${HOST_PID}" 2>/dev/null; then
    echo "==> Stopping host (PID ${HOST_PID})"
    kill "${HOST_PID}" 2>/dev/null || true
    wait "${HOST_PID}" 2>/dev/null || true
  fi
}
trap cleanup EXIT

mkdir -p "${CACHE_ROOT}"

echo "==> Resolving ${VALIDATOR_REPO}@${VALIDATOR_REF}"
RESOLVED_SHA="$(git ls-remote "https://github.com/${VALIDATOR_REPO}" "${VALIDATOR_REF}" | cut -f1)"
RESOLVED_SHA="${RESOLVED_SHA:-${VALIDATOR_REF}}"
SHA_FILE="${VALIDATOR_BIN}/.sha"

if [[ -f "${SHA_FILE}" ]] && [[ "$(cat "${SHA_FILE}")" == "${RESOLVED_SHA}" ]] && [[ -f "${VALIDATOR_BIN}/Microsoft.Office.WopiValidator.dll" ]]; then
  echo "==> Using cached validator at ${VALIDATOR_BIN} (sha ${RESOLVED_SHA})"
else
  echo "==> Building validator from source (sha ${RESOLVED_SHA})"
  rm -rf "${VALIDATOR_SRC}" "${VALIDATOR_BIN}"
  git clone "https://github.com/${VALIDATOR_REPO}" "${VALIDATOR_SRC}"
  git -C "${VALIDATOR_SRC}" checkout "${RESOLVED_SHA}"
  dotnet publish "${VALIDATOR_SRC}/src/WopiValidator" -c Release -f net8.0 -o "${VALIDATOR_BIN}"
  echo "${RESOLVED_SHA}" > "${SHA_FILE}"
fi

echo "==> Building WopiHost.Validator"
dotnet build "${REPO_ROOT}/sample/WopiHost.Validator" -c Release

echo "==> Starting host on ${HOST_URL}"
dotnet run --no-build --project "${REPO_ROOT}/sample/WopiHost.Validator" -c Release -- --urls "${HOST_URL}" > "${HOST_LOG}" 2>&1 &
HOST_PID=$!

for i in $(seq 1 60); do
  if curl -fsS "${HOST_URL}/health" >/dev/null 2>&1; then
    echo "==> Host healthy after ${i}s (PID ${HOST_PID})"
    break
  fi
  if ! kill -0 "${HOST_PID}" 2>/dev/null; then
    echo "==> Host process exited before becoming healthy. Tail of ${HOST_LOG}:"
    tail -n 50 "${HOST_LOG}"
    exit 1
  fi
  sleep 1
done

if ! curl -fsS "${HOST_URL}/health" >/dev/null 2>&1; then
  echo "==> Host failed to become healthy within 60s. Tail of ${HOST_LOG}:"
  tail -n 50 "${HOST_LOG}"
  exit 1
fi

echo "==> Running validator (--testcategory ${TEST_CATEGORY})"
set +e
dotnet "${VALIDATOR_BIN}/Microsoft.Office.WopiValidator.dll" \
  --wopisrc "${HOST_URL}/wopi/files/${WOPI_FILE_ID}" \
  --token "${WOPI_ACCESS_TOKEN}" \
  --token_ttl 0 \
  --config "${VALIDATOR_BIN}/TestCases.xml" \
  --testcategory "${TEST_CATEGORY}" \
  2>&1 | tee "${VALIDATOR_LOG}"
set -e

PASS=$(grep -cE '^  Pass:' "${VALIDATOR_LOG}" || true)
FAIL=$(grep -cE '^  Fail:' "${VALIDATOR_LOG}" || true)
SKIP=$(grep -cE '^  Skipped:' "${VALIDATOR_LOG}" || true)

echo
echo "==> Results: ${PASS} pass / ${FAIL} fail / ${SKIP} skipped"
echo "==> Logs: ${VALIDATOR_LOG}, ${HOST_LOG}"
[ "${FAIL}" -eq 0 ]
