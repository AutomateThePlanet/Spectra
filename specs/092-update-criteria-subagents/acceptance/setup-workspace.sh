#!/usr/bin/env bash
# Spec 092 manual acceptance (quickstart.md "Manual acceptance"): builds a scratch workspace for measuring the main
# session's context with Claude Code's /context.
#
#   setup-workspace.sh <empty-target-dir> [spectra command]
#
# The spectra command defaults to `spectra`; pass e.g. "dotnet /path/to/Spectra.CLI.dll" to use a local build.
# Result: suite `storage` with 33 tests over three doc sections, a baseline, then a doc change that makes every test
# LOAD (≥ 3 triage groups at the default group size of 10), plus 5 new docs for criteria extraction.
set -euo pipefail

TARGET="${1:?usage: setup-workspace.sh <empty-target-dir> [spectra command]}"
SPECTRA="${2:-spectra}"
sp() { $SPECTRA "$@"; }

mkdir -p "$TARGET"
cd "$TARGET"
if [ -n "$(ls -A .)" ]; then echo "target dir is not empty: $TARGET" >&2; exit 1; fi

sp init >/dev/null
mkdir -p docs

write_storage_doc() {   # $1 quota GB, $2 share days, $3 retention days
  cat > docs/storage.md <<EOF
# Storage

## Quotas

Each user gets **$1 GB** of storage. Uploads that would exceed the quota MUST be refused with a clear message.
The quota is shown in the account settings.

## Sharing

Files can be shared with any registered user. A share link MUST expire after **$2 days**.
The owner can revoke a share at any time.

## Retention

Deleted files stay in the trash for **$3 days**, then they are removed permanently.
Restoring a file from the trash keeps its sharing settings.
EOF
}

write_storage_doc 30 7 30

# 33 tests, 11 per section.
{
  echo "["
  n=100
  first=1
  for section in Quotas Sharing Retention; do
    for i in $(seq 1 11); do
      n=$((n + 1))
      [ $first -eq 1 ] || echo ","
      first=0
      printf '{"id":"TC-%d","title":"%s rule %d","priority":"high","steps":["Open the %s page","Check rule %d"],"expected_result":"The %s rule %d holds as documented","source_refs":["docs/storage.md#%s"]}' \
        "$n" "$section" "$i" "$section" "$i" "$section" "$i" "$section"
    done
  done
  echo "]"
} > payload.json
sp ai ingest-tests storage --from payload.json --output-format json >/dev/null
rm payload.json

# The doc change: a number in every section → every test is LOAD.
write_storage_doc 45 14 60

# Five new docs for criteria extraction.
for name in billing login profile search export; do
  cat > "docs/$name.md" <<EOF
# ${name^}

## Rules

The $name feature MUST log every change. Users SHOULD get a confirmation within 5 seconds.
Administrators MAY disable the $name feature for a team. The $name page MUST NOT show other users' data.
EOF
done

echo "== triage (dry run)"
sp ai update-triage --suite storage --dry-run --output-format json 2>/dev/null
echo
echo "== changed docs"
sp docs changed --output-format json 2>/dev/null | head -c 600
echo
echo "Workspace ready: $TARGET"
echo "1. Open Claude Code here; run /context and note the total."
echo "2. Ask: update the storage suite (choose LOAD, accept re-verification). Then /context again."
echo "3. Ask: extract acceptance criteria. Check the transcript holds no document text or compiled prompt."
