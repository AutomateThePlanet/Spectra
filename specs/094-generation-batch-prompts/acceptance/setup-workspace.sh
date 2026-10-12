#!/usr/bin/env bash
# Spec 094 acceptance (quickstart.md "Before implementing" and "Manual acceptance"): builds a scratch workspace for
# comparing generation in 093 count mode with 094 section batches, on the same docs and criteria.
#
#   setup-workspace.sh <empty-target-dir> [spectra command]
#
# The spectra command defaults to `spectra`; pass e.g. "dotnet /path/to/Spectra.CLI.dll" to use a local build. Works
# with the 093 build (55b541f) and the 094 build.
# Result: three documents (storage, sharing, retention) with 4-6 sections each, indexed; acceptance criteria for each
# document with `source_section` set, ingested without a model (spectra ai ingest-criteria); an empty suite `storage`.
set -euo pipefail

TARGET="${1:?usage: setup-workspace.sh <empty-target-dir> [spectra command]}"
SPECTRA="${2:-spectra}"
sp() { $SPECTRA "$@"; }

mkdir -p "$TARGET"
cd "$TARGET"
if [ -n "$(ls -A .)" ]; then echo "target dir is not empty: $TARGET" >&2; exit 1; fi

sp init >/dev/null
mkdir -p docs test-cases/storage .spectra/acceptance

cat > docs/storage.md <<'EOF'
# Storage

## Quotas

Each user gets **30 GB** of storage. An upload that would exceed the quota MUST be refused with the message
"Storage quota exceeded". Uploads up to exactly 30 GB MUST succeed. Administrators MAY raise a user's quota to at
most 1 TB.

### Quota display

The current usage and the quota MUST be shown in the account settings, rounded to one decimal. At 90 % usage a
yellow warning banner MUST appear; at 100 % the banner MUST turn red and name the quota.

## Uploads

A single file MUST NOT be larger than **5 GB**. File names MUST be 1 to 255 characters long and MUST NOT contain
`/` or `\`. Uploading a file with an existing name MUST ask whether to replace it or keep both; "keep both" appends
" (1)", " (2)", … to the new name.

### Resumable uploads

An interrupted upload MUST be resumable for 24 hours. After 24 hours the partial upload MUST be discarded and the
user MUST start again. At most 3 uploads MAY run in parallel; a fourth MUST wait in a queue.

## Folders

Folders MAY be nested at most **20** levels deep. Moving a folder into one of its own subfolders MUST be refused.
An empty folder MAY be deleted at once; a non-empty folder MUST ask for confirmation and show the number of files
it contains.
EOF

cat > docs/sharing.md <<'EOF'
# Sharing

## Share links

Any file or folder can be shared by link. A share link MUST expire after **7 days** by default; the owner MAY set
1 to 90 days. An expired link MUST show "This link has expired".

## Link passwords

A link MAY be protected by a password of at least 8 characters. After 5 wrong attempts the link MUST be locked for
15 minutes.

## Sharing with users

Files can be shared with registered users as Viewer or Editor. A Viewer MUST NOT change or delete the file. An Editor
MAY change the file but MUST NOT share it further.

## Revoking

The owner can revoke a share at any time; revoking MUST take effect immediately for open sessions.
EOF

cat > docs/retention.md <<'EOF'
# Retention

## Trash

Deleted files stay in the trash for **30 days**, then they are removed permanently. Emptying the trash MUST ask for
confirmation and cannot be undone.

## Restoring

Restoring a file from the trash MUST put it back in its original folder; if that folder no longer exists, it MUST be
restored to the root. Restoring keeps its sharing settings.

## Versions

Each file keeps its last **10 versions**. Restoring an older version MUST create a new version, never overwrite.

## Pruning

Versions older than 180 days MAY be pruned, but the newest version MUST always be kept.
EOF

cat > .spectra/acceptance/storage.json <<'EOF'
[
  {"text": "An upload that would exceed the 30 GB quota MUST be refused with \"Storage quota exceeded\"", "rfc2119": "MUST", "source_section": "Quotas", "priority": "high"},
  {"text": "Uploads up to exactly 30 GB MUST succeed", "rfc2119": "MUST", "source_section": "Quotas", "priority": "high"},
  {"text": "Usage and quota MUST be shown in the account settings, rounded to one decimal", "rfc2119": "MUST", "source_section": "Quota display", "priority": "medium"},
  {"text": "A yellow banner MUST appear at 90 % usage and turn red at 100 %", "rfc2119": "MUST", "source_section": "Quota display", "priority": "medium"},
  {"text": "A single file MUST NOT be larger than 5 GB", "rfc2119": "MUST", "source_section": "Uploads", "priority": "high"},
  {"text": "File names MUST be 1 to 255 characters and MUST NOT contain / or \\", "rfc2119": "MUST", "source_section": "Uploads", "priority": "high"},
  {"text": "Uploading an existing name MUST offer replace or keep both", "rfc2119": "MUST", "source_section": "Uploads", "priority": "medium"},
  {"text": "An interrupted upload MUST be resumable for 24 hours", "rfc2119": "MUST", "source_section": "Resumable uploads", "priority": "medium"},
  {"text": "A fourth parallel upload MUST wait in a queue", "rfc2119": "MUST", "source_section": "Resumable uploads", "priority": "low"},
  {"text": "Folders MAY be nested at most 20 levels deep", "rfc2119": "MAY", "source_section": "Folders", "priority": "medium"},
  {"text": "Moving a folder into its own subfolder MUST be refused", "rfc2119": "MUST", "source_section": "Folders", "priority": "high"},
  {"text": "Deleting a non-empty folder MUST ask for confirmation and show the file count", "rfc2119": "MUST", "source_section": "Folders", "priority": "medium"}
]
EOF

cat > .spectra/acceptance/sharing.json <<'EOF'
[
  {"text": "A share link MUST expire after 7 days by default; the owner MAY set 1 to 90 days", "rfc2119": "MUST", "source_section": "Share links", "priority": "high"},
  {"text": "An expired link MUST show \"This link has expired\"", "rfc2119": "MUST", "source_section": "Share links", "priority": "medium"},
  {"text": "A link password MUST have at least 8 characters", "rfc2119": "MUST", "source_section": "Link passwords", "priority": "medium"},
  {"text": "After 5 wrong attempts the link MUST be locked for 15 minutes", "rfc2119": "MUST", "source_section": "Link passwords", "priority": "high"},
  {"text": "A Viewer MUST NOT change or delete the file", "rfc2119": "MUST", "source_section": "Sharing with users", "priority": "high"},
  {"text": "An Editor MUST NOT share the file further", "rfc2119": "MUST", "source_section": "Sharing with users", "priority": "medium"},
  {"text": "Revoking a share MUST take effect immediately for open sessions", "rfc2119": "MUST", "source_section": "Revoking", "priority": "high"}
]
EOF

cat > .spectra/acceptance/retention.json <<'EOF'
[
  {"text": "Deleted files MUST be removed permanently after 30 days in the trash", "rfc2119": "MUST", "source_section": "Trash", "priority": "high"},
  {"text": "Emptying the trash MUST ask for confirmation", "rfc2119": "MUST", "source_section": "Trash", "priority": "medium"},
  {"text": "A restored file MUST return to its original folder, or the root if it no longer exists", "rfc2119": "MUST", "source_section": "Restoring", "priority": "high"},
  {"text": "Each file MUST keep its last 10 versions", "rfc2119": "MUST", "source_section": "Versions", "priority": "medium"},
  {"text": "Restoring an older version MUST create a new version", "rfc2119": "MUST", "source_section": "Versions", "priority": "high"},
  {"text": "The newest version MUST always be kept when pruning", "rfc2119": "MUST", "source_section": "Pruning", "priority": "medium"}
]
EOF

sp docs index --skip-criteria >/dev/null 2>&1 || sp docs index >/dev/null 2>&1 || true

# The criteria all belong to the `storage` component so the 093 count-mode suite match finds them for suite `storage`.
for doc in storage sharing retention; do
  sp ai ingest-criteria --doc "docs/$doc.md" --component storage --from ".spectra/acceptance/$doc.json" \
    --output-format json >/dev/null 2>&1
done

echo "== docs and sections"
for doc in docs/storage.md docs/sharing.md docs/retention.md; do
  echo "$doc: $(grep -c '^##' "$doc") sections"
done
echo "== criteria files"
find docs/criteria -name '*.criteria.yaml' | sort
echo
echo "Workspace ready: $TARGET"
echo "Ask Claude Code: \"generate tests for the storage area\" (suite storage), approve, and let it finish."
