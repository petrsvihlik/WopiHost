#!/usr/bin/env bash
# Run the Microsoft WOPI validator against the WopiHost.Validator sample.
# Mirrors what .github/workflows/wopi-validator.yml does, for local iteration.
#
# Usage:
#   scripts/run-validator.sh                 # All categories (default)
#   scripts/run-validator.sh OfficeOnline    # different test category
#
# The validator version is pinned in tools/wopi-validator/wopi-validator.csproj
# and bumped automatically by Dependabot.
#
# Requirements: bash, curl, dotnet (8.0 + 10.0 SDKs).

set -euo pipefail

TEST_CATEGORY="${1:-All}"
HOST_URL="${HOST_URL:-http://localhost:5000}"
WOPI_FILE_ID="${WOPI_FILE_ID:-WOPITEST}"
# Set WOPI_ACCESS_TOKEN to override; otherwise we mint one via the host's
# /_test/issue-token endpoint (test-only; only available in the Validator sample).
WOPI_ACCESS_TOKEN="${WOPI_ACCESS_TOKEN:-}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VALIDATOR_PROJECT="${REPO_ROOT}/tools/wopi-validator/wopi-validator.csproj"
CACHE_ROOT="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/wopi-validator-cache"
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

echo "==> Publishing WopiValidator from NuGet (per ${VALIDATOR_PROJECT##${REPO_ROOT}/})"
dotnet publish "${VALIDATOR_PROJECT}" -c Release -o "${VALIDATOR_BIN}"

# PackageReference doesn't auto-copy the package's content/*.xml or
# runtimeconfig.json, so pull them straight from the NuGet cache.
NUGET_PACKAGES="${NUGET_PACKAGES:-${HOME}/.nuget/packages}"
VALIDATOR_PKG="$(ls -d "${NUGET_PACKAGES}/wopivalidator/"*/ | sort -V | tail -1)"
cp "${VALIDATOR_PKG}content/"*.xml "${VALIDATOR_BIN}/"
cp "${VALIDATOR_PKG}lib/net8.0/Microsoft.Office.WopiValidator.runtimeconfig.json" "${VALIDATOR_BIN}/"
VALIDATOR_VERSION="$(basename "${VALIDATOR_PKG%/}")"
echo "==> Using WopiValidator ${VALIDATOR_VERSION}"

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

if [[ -z "${WOPI_ACCESS_TOKEN}" ]]; then
  echo "==> Minting access token via ${HOST_URL}/_test/issue-token/${WOPI_FILE_ID}"
  WOPI_ACCESS_TOKEN="$(curl -fsS "${HOST_URL}/_test/issue-token/${WOPI_FILE_ID}")"
  if [[ -z "${WOPI_ACCESS_TOKEN}" ]]; then
    echo "==> Failed to mint token. Tail of ${HOST_LOG}:"
    tail -n 50 "${HOST_LOG}"
    exit 1
  fi
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

# Known-failure baseline (issues #291, #292, #293).
# Mirrors .github/workflows/wopi-validator.yml so local runs and CI agree.
# See sample/WopiHost.Validator/Infrastructure/NoOpProofValidator.cs and
# https://github.com/microsoft/wopi-validator-core/pull/145 (rebase of #86) for context.
KNOWN_FAILURES=$(printf '%s\n' \
  "ProofKeys.CurrentInvalid.OldValidSignedWithOldKey" \
  "ProofKeys.CurrentInvalid.OldInvalid" \
  "ProofKeys.TimestampOlderThan20Min" \
  | sort -u)
ACTUAL_FAILURES=$( { grep -E '^  Fail:' "${VALIDATOR_LOG}" || true; } | sed -E 's/^  Fail:[[:space:]]*//' | sort -u)
UNEXPECTED_FAILURES=$(comm -23 <(printf '%s\n' "${ACTUAL_FAILURES}") <(printf '%s\n' "${KNOWN_FAILURES}") || true)
UNEXPECTEDLY_PASSING=$(comm -13 <(printf '%s\n' "${ACTUAL_FAILURES}") <(printf '%s\n' "${KNOWN_FAILURES}") || true)

echo
echo "==> Results: ${PASS} pass / ${FAIL} fail / ${SKIP} skipped"
echo "==> Logs: ${VALIDATOR_LOG}, ${HOST_LOG}"

EXIT_CODE=0
if [[ -n "${UNEXPECTED_FAILURES}" ]]; then
  echo
  echo "==> ❌ Unexpected failures (not in pinned baseline):"
  printf '      %s\n' "${UNEXPECTED_FAILURES}"
  EXIT_CODE=1
fi
if [[ -n "${UNEXPECTEDLY_PASSING}" ]]; then
  echo
  echo "==> ⚠️  Pinned tests that did NOT fail this run — retire the pin or update names:"
  printf '      %s\n' "${UNEXPECTEDLY_PASSING}"
  EXIT_CODE=1
fi

exit "${EXIT_CODE}"
