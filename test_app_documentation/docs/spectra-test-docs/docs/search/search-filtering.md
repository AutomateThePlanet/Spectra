# Search and Filtering

## Overview

The system provides a unified search experience for finding applications, citizens, documents, and internal records. Search is available to all authenticated users, with results filtered by the user's permissions.

## Global Search

A search bar is present in the top navigation on every page. It supports:

### Quick Search
- Typing 3 or more characters triggers autocomplete suggestions after a 300ms debounce
- Suggestions are grouped by category: Applications, Citizens, Documents
- Maximum 5 suggestions per category (15 total)
- Each suggestion shows: title/name, category icon, and a brief context line (e.g., application status, citizen EGN last 4 digits)
- Pressing Enter or clicking "See all results" opens the full search results page
- Pressing Escape or clicking outside closes the autocomplete dropdown

### Search Query Syntax

Plain text searches across all indexed fields. Additionally:

| Syntax | Example | Meaning |
|---|---|---|
| Exact phrase | `"building permit"` | Matches the exact phrase |
| Field-specific | `status:pending` | Filters by specific field |
| Date range | `date:2026-01-01..2026-03-31` | Submitted within date range |
| Numeric range | `amount:100..500` | Fee amount between 100 and 500 BGN |
| Exclusion | `-rejected` | Excludes results containing "rejected" |
| Wildcard | `cert*` | Matches "certificate", "certification", etc. |
| OR operator | `sofia OR plovdiv` | Matches either term |

Field-specific filters available: `status`, `service`, `region`, `clerk`, `date`, `amount`, `egn`, `ref` (reference number).

If the query contains only digits and is 10 characters long, it is automatically treated as an EGN search. If it matches the pattern XX-YYYY-NNNNNN, it is treated as a reference number search.

## Search Results Page

Results are displayed in a tabbed layout:
- **All** (default): Mixed results, ranked by relevance
- **Applications**: Only applications
- **Citizens**: Only citizen records
- **Documents**: Only documents

Each tab shows the result count. Tabs with zero results are grayed out but still clickable.

### Result Cards

**Application result:**
- Reference number (linked)
- Service name
- Citizen name (if user has `citizens.view` permission; otherwise masked)
- Status badge (color-coded)
- Submission date
- Assigned clerk

**Citizen result:**
- Full name (linked to citizen profile)
- EGN (masked: ******XXXX)
- Email
- Registration date
- Account status (Active/Pending/Deactivated)

**Document result:**
- Document name (linked)
- Type
- Related application reference
- Upload/generation date
- Status (Draft/Signed/Delivered)

### Sorting

Results can be sorted by:
- Relevance (default for text search)
- Date (newest first / oldest first)
- Status
- Name (A-Z / Z-A)

### Pagination

- 20 results per page
- Infinite scroll on mobile, traditional pagination on desktop
- Total result count shown: "Showing 1-20 of 342 results"
- If total exceeds 10,000, displayed as "10,000+ results" (exact count not computed for performance)

## Advanced Filters Panel

A collapsible filter panel on the left side of search results:

- **Status**: Multi-select checkboxes (Pending, In Processing, Completed, Rejected, Cancelled)
- **Service Type**: Searchable multi-select dropdown
- **Date Range**: Two date pickers (from/to) with presets (today, this week, this month, last 3 months, last year)
- **Region**: Multi-select based on department hierarchy
- **Assigned To**: Searchable dropdown (for managers/directors only)
- **Payment Status**: Paid / Unpaid / Refunded / Waived

Filters combine with AND logic. Within each filter, options combine with OR logic.

Active filters are shown as chips above the results. Each chip has an "x" to remove that filter. A "Clear all filters" link resets everything.

Changing any filter immediately updates results (no "Apply" button needed). URL query parameters are updated to reflect current filters, enabling bookmarking and sharing.

## Saved Searches

Authenticated users can save search queries:

- Click "Save this search" on the results page
- Enter a name for the saved search (max 50 characters)
- Maximum 20 saved searches per user
- Saved searches appear in the search bar dropdown before typing
- Saved searches can be edited (rename, update filters) or deleted
- Saved searches are private — not shared between users

## Search Indexing

- New records are indexed within 5 seconds of creation (near real-time)
- Updated records are re-indexed within 5 seconds
- Deleted/deactivated records are removed from the index within 1 minute
- Full re-index can be triggered by System Admin (takes 5-15 minutes depending on data volume)
- Index includes Bulgarian and English content with language-specific stemming
- Cyrillic and Latin transliteration: searching "Sofia" also matches "София" and vice versa

## Permission-Based Filtering

Search results are automatically filtered by the user's permissions:

- Clerks: Only see applications assigned to them or in their department queue
- Managers: See all applications in their department and sub-departments
- Directors: See all applications in their directorate
- Citizens (portal search): See only their own applications and documents

This filtering happens at the query level (not post-query), so result counts are accurate and performance is not affected by permission complexity.

## Performance Requirements

- Autocomplete suggestions: < 200ms response time
- Search results (first page): < 500ms for queries matching up to 100,000 documents
- Advanced filter changes: < 300ms to update results
- Search index size: supports up to 10 million documents
