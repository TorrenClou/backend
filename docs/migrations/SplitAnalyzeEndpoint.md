# Frontend Migration: Split Analyze Endpoint

**Date:** 2026-02-12  
**Breaking Change:** Yes

## Summary

The torrent workflow has been split into two distinct steps:

1. **Analyze** — Upload a `.torrent` file to get metadata (file list, health, size). No storage profile or file selection needed.
2. **Create Job** — Submit the torrent file ID, selected files, and storage profile to start downloading.

This lets the frontend show a file picker and storage selector **after** analyzing the torrent.

---

## Step 1: Analyze Torrent (CHANGED)

### `POST /api/torrents/analyze`

**Before (old):**
```
Content-Type: multipart/form-data

- TorrentFile: <file>          (required)
- StorageProfileId: <int>      (required)   ← REMOVED
- SelectedFilePaths: <string[]> (optional)  ← REMOVED
```

**After (new):**
```
Content-Type: multipart/form-data

- TorrentFile: <file>          (required)
```

Only the `.torrent` file is needed. No storage profile, no file selection at this stage.

### Response (CHANGED)

**Before — `QuoteResponseDto`:**
```json
{
  "fileName": "ubuntu-24.04.iso",
  "sizeInBytes": 4700000000,
  "infoHash": "abc123...",
  "torrentHealth": { ... },
  "torrentFileId": 42,
  "selectedFiles": ["file1.mkv"],
  "message": null
}
```

**After — `TorrentAnalysisResponseDto`:**
```json
{
  "torrentFileId": 42,
  "fileName": "ubuntu-24.04.iso",
  "infoHash": "abc123...",
  "totalSizeInBytes": 4700000000,
  "files": [
    { "index": 0, "path": "ubuntu-24.04.iso", "size": 4700000000 },
    { "index": 1, "path": "README.txt", "size": 1024 }
  ],
  "torrentHealth": {
    "seeders": 150,
    "leechers": 20,
    "completed": 5000,
    "seederRatio": 7.5,
    "isComplete": true,
    "isDead": false,
    "isWeak": false,
    "isHealthy": true,
    "healthScore": 92.5
  }
}
```

**Key changes:**
| Field | Status | Notes |
|-------|--------|-------|
| `torrentFileId` | Kept | Same — use this in Step 2 |
| `fileName` | Kept | Torrent name |
| `infoHash` | Kept | Same |
| `totalSizeInBytes` | Renamed | Was `sizeInBytes` — now always the **total** torrent size |
| `files` | **NEW** | Full file list with `index`, `path`, `size` — use for file picker UI |
| `torrentHealth` | Kept | Same health metrics |
| `selectedFiles` | **REMOVED** | No longer returned — selection happens in Step 2 |
| `message` | **REMOVED** | No longer returned |

---

## Step 2: Create Job (CHANGED)

### `POST /api/torrents/create-job`

**Before:**
```json
{
  "torrentFileId": 42,
  "selectedFilePaths": ["file1.mkv"],    // optional
  "storageProfileId": 5                  // optional (used default)
}
```

**After:**
```json
{
  "torrentFileId": 42,
  "selectedFilePaths": ["file1.mkv"],    // optional (null = download all)
  "storageProfileId": 5                  // REQUIRED (was optional)
}
```

**Key change:** `storageProfileId` is now **required** (was optional, previously fell back to user's default profile). The frontend must explicitly send the storage profile ID.

### Response (UNCHANGED)
```json
{
  "jobId": 101,
  "storageProfileId": 5
}
```

---

## New Frontend Flow

```
┌─────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  Upload      │     │  File Selection   │     │  Start Job       │
│  .torrent    │────▶│  + Storage Pick   │────▶│  Confirm & Go    │
│  file        │     │                   │     │                  │
└─────────────┘     └──────────────────┘     └──────────────────┘
      │                      │                        │
  POST /analyze         (frontend only)        POST /create-job
  Body: file only       Show file picker       Body: {
                        from response.files      torrentFileId,
                        Show storage dropdown    selectedFilePaths,
                        (fetch from               storageProfileId
                         /api/storage/profiles)  }
```

### Implementation Steps

1. **Upload screen** — Only send the `.torrent` file via `multipart/form-data`. Remove `StorageProfileId` and `SelectedFilePaths` from the form.

2. **Analysis response handler** — Parse the new `TorrentAnalysisResponseDto`:
   - Store `torrentFileId` for Step 2
   - Render `files[]` array as a selectable file tree/list (use `path` for display, `size` for file size labels)
   - Display `torrentHealth` info (seeders, leechers, health score)
   - Show `totalSizeInBytes` as the total torrent size
   - Calculate selected size client-side by summing `size` of selected files

3. **Storage profile selector** — Fetch user's storage profiles from `GET /api/storage/profiles` and show a dropdown/selector. This was previously done implicitly during analyze.

4. **Create job** — On confirm, send `POST /api/torrents/create-job` with:
   ```json
   {
     "torrentFileId": <from step 1>,
     "selectedFilePaths": ["path/to/file1.mkv", "path/to/file2.mkv"],
     "storageProfileId": <from storage selector>
   }
   ```
   - If user selects all files, send `selectedFilePaths: null` (or omit it)
   - `storageProfileId` is **required** — don't submit without it

### Error Handling

- **Analyze errors** — `400` for invalid file (not .torrent), `422` for unparseable torrent
- **Create job errors**:
  - `400` — Invalid storage profile (wrong owner, inactive)
  - `404` — Torrent file ID not found
  - `409` — Active job already exists for this torrent

---

## TypeScript Types (Reference)

```typescript
// Step 1: Analyze
interface AnalyzeRequest {
  torrentFile: File;  // .torrent file only
}

interface TorrentAnalysisResponse {
  torrentFileId: number;
  fileName: string;
  infoHash: string;
  totalSizeInBytes: number;
  files: TorrentFile[];
  torrentHealth: TorrentHealth;
}

interface TorrentFile {
  index: number;
  path: string;
  size: number;
}

interface TorrentHealth {
  seeders: number;
  leechers: number;
  completed: number;
  seederRatio: number;
  isComplete: boolean;
  isDead: boolean;
  isWeak: boolean;
  isHealthy: boolean;
  healthScore: number;
}

// Step 2: Create Job
interface CreateJobRequest {
  torrentFileId: number;
  selectedFilePaths?: string[] | null; // null = all files
  storageProfileId: number;           // REQUIRED
}

interface JobCreationResponse {
  jobId: number;
  storageProfileId: number;
}
```
