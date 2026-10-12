#!/usr/bin/env bash
# Spec 093 manual acceptance (quickstart.md "Manual acceptance"): builds a scratch workspace for measuring the main
# session's context during a generation run with Claude Code's /context.
#
#   setup-workspace.sh <empty-target-dir> [spectra command]
#
# The spectra command defaults to `spectra`; pass e.g. "dotnet /path/to/Spectra.CLI.dll" to use a local build.
# Result: three documents (storage, sharing, retention) with enough rules, limits and numbers for >= 24 tests in >= 3
# batches, indexed, and an empty suite `storage` to generate into.
set -euo pipefail

TARGET="${1:?usage: setup-workspace.sh <empty-target-dir> [spectra command]}"
SPECTRA="${2:-spectra}"
sp() { $SPECTRA "$@"; }

mkdir -p "$TARGET"
cd "$TARGET"
if [ -n "$(ls -A .)" ]; then echo "target dir is not empty: $TARGET" >&2; exit 1; fi

sp init >/dev/null
mkdir -p docs test-cases/storage

cat > docs/storage.md <<'EOF'
# Storage

## Quotas

Each user gets **30 GB** of storage. An upload that would exceed the quota MUST be refused with the message
"Storage quota exceeded". Uploads up to exactly 30 GB MUST succeed. Administrators MAY raise a user's quota to at
most 1 TB. The current usage and the quota MUST be shown in the account settings, rounded to one decimal.

## Uploads

A single file MUST NOT be larger than **5 GB**. File names MUST be 1 to 255 characters long and MUST NOT contain
`/` or `\`. Uploading a file with an existing name MUST ask whether to replace it or keep both; "keep both" appends
" (1)", " (2)", … to the new name. An interrupted upload MUST be resumable for 24 hours.

## Folders

Folders MAY be nested at most **20** levels deep. Moving a folder into one of its own subfolders MUST be refused.
An empty folder MAY be deleted at once; a non-empty folder MUST ask for confirmation and show the number of files
it contains.
EOF

cat > docs/sharing.md <<'EOF'
# Sharing

## Share links

Any file or folder can be shared by link. A share link MUST expire after **7 days** by default; the owner MAY set
1 to 90 days. An expired link MUST show "This link has expired". A link MAY be protected by a password of at least
8 characters; after 5 wrong attempts the link MUST be locked for 15 minutes.

## Sharing with users

Files can be shared with registered users as Viewer or Editor. A Viewer MUST NOT change or delete the file. An Editor
MAY change the file but MUST NOT share it further. The owner can revoke a share at any time; revoking MUST take effect
immediately for open sessions.
EOF

cat > docs/retention.md <<'EOF'
# Retention

## Trash

Deleted files stay in the trash for **30 days**, then they are removed permanently. Restoring a file from the trash
MUST put it back in its original folder; if that folder no longer exists, it MUST be restored to the root. Restoring
keeps its sharing settings. Emptying the trash MUST ask for confirmation and cannot be undone.

## Versions

Each file keeps its last **10 versions**. Restoring an older version MUST create a new version, never overwrite.
Versions older than 180 days MAY be pruned, but the newest version MUST always be kept.
EOF

sp docs index >/dev/null 2>&1 || true

echo "== doc suites"
sp docs list-suites --output-format json 2>/dev/null | head -c 800
echo
echo "Workspace ready: $TARGET"
echo "1. Open Claude Code here; run /context and note the total."
echo "2. Ask: \"generate tests for the storage suite from the storage, sharing and retention docs\"; approve the plan;"
echo "   let it finish (critic, repairs). Then /context again; check the report lists every test."
echo "3. For SC-002, run the same request on the 092 build (same model) and compare the grounded share."
