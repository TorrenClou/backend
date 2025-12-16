# Next.js Frontend Development Prompt - TorreClou Platform

## Overview
Build a complete Next.js 14+ application with NextAuth.js that replicates all features from the TorreClou backend. This is a **UI-only implementation** - no API calls or real data integration. Use mock data and TypeScript interfaces matching the backend DTOs.

## Technology Stack
- **Framework**: Next.js 14+ (App Router)
- **Authentication**: NextAuth.js v5 (Auth.js) with Google OAuth provider
- **Styling**: Tailwind CSS
- **Language**: TypeScript (strict mode)
- **State Management**: React Context API or Zustand (for client state)
- **Forms**: React Hook Form with Zod validation
- **UI Components**: Custom components built with Tailwind CSS
- **Icons**: Lucide React or Heroicons
- **Date Handling**: date-fns

## Color Scheme
Use the following color palette throughout the application:

- **Primary Teal**: `#6AECE1` - Main actions, links, highlights
- **Secondary Teal**: `#26CCC2` - Hover states, secondary buttons, accents
- **Yellow**: `#FFF57E` - Warnings, important notices, badges
- **Orange**: `#FFB76C` - Alerts, errors, destructive actions

**Color Usage Guidelines:**
- Primary Teal (`#6AECE1`): Primary buttons, active links, selected items, progress bars
- Secondary Teal (`#26CCC2`): Hover states on primary elements, secondary buttons, borders
- Yellow (`#FFF57E`): Warning badges, pending status indicators, important notices
- Orange (`#FFB76C`): Error states, failed status, destructive actions, expired items

## TypeScript Models/Interfaces

Create the following TypeScript interfaces matching the backend DTOs:

```typescript
// Enums
enum RegionCode {
  Global = "Global",
  US = "US",
  EU = "EU",
  EG = "EG",
  SA = "SA",
  IN = "IN"
}

enum UserRole {
  User = "User",
  Admin = "Admin",
  Support = "Support"
}

enum StorageProviderType {
  GoogleDrive = "GoogleDrive",
  OneDrive = "OneDrive",
  AwsS3 = "AwsS3",
  Dropbox = "Dropbox"
}

enum TransactionType {
  DEPOSIT = "DEPOSIT",
  PAYMENT = "PAYMENT",
  REFUND = "REFUND",
  ADMIN_ADJUSTMENT = "ADMIN_ADJUSTMENT",
  BONUS = "BONUS",
  DEDUCTION = "DEDUCTION"
}

enum FileStatus {
  PENDING = "PENDING",
  DOWNLOADING = "DOWNLOADING",
  READY = "READY",
  CORRUPTED = "CORRUPTED",
  DELETED = "DELETED"
}

enum DiscountType {
  Percentage = "Percentage",
  FixedAmount = "FixedAmount"
}

enum DepositStatus {
  Pending = "Pending",
  Completed = "Completed",
  Failed = "Failed",
  Expired = "Expired"
}

enum JobStatus {
  QUEUED = "QUEUED",
  PROCESSING = "PROCESSING",
  UPLOADING = "UPLOADING",
  COMPLETED = "COMPLETED",
  FAILED = "FAILED",
  CANCELLED = "CANCELLED"
}

enum JobType {
  Torrent = "Torrent",
  Other = "Other"
}

enum ViolationType {
  Spam = "Spam",
  Abuse = "Abuse",
  TermsViolation = "TermsViolation",
  CopyrightInfringement = "CopyrightInfringement",
  Other = "Other"
}

// User Models
interface User {
  id: number;
  email: string;
  fullName: string;
  oauthProvider: string;
  phoneNumber: string;
  isPhoneNumberVerified: boolean;
  region: RegionCode;
  role: UserRole;
  currentBalance: number;
}

interface AuthResponse {
  accessToken: string;
  email: string;
  fullName: string;
  currentBalance: number;
  role: string;
}

// Wallet Models
interface WalletBalance {
  balance: number;
  currency: string;
}

interface WalletTransaction {
  id: number;
  amount: number;
  type: TransactionType;
  referenceId?: string;
  description: string;
  createdAt: string;
}

// Deposit Models
interface Deposit {
  id: number;
  amount: number;
  currency: string;
  paymentProvider: string;
  paymentUrl?: string;
  status: DepositStatus;
  createdAt: string;
  updatedAt?: string;
}

interface CryptoDepositRequest {
  amount: number;
  currency: string;
}

interface StablecoinMinAmount {
  currency: string;
  minAmount: number;
  fiatEquivalent: string;
}

// Torrent Models
interface TorrentFile {
  index: number;
  path: string;
  size: number;
}

interface ScrapeAggregationResult {
  seeders: number;
  leechers: number;
  completed: number;
  trackersSuccess: number;
  trackersTotal: number;
}

interface TorrentHealthMeasurements {
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

interface TorrentInfo {
  infoHash: string;
  name: string;
  totalSize: number;
  files: TorrentFile[];
  trackers: string[];
  scrapeResult: ScrapeAggregationResult;
}

interface TorrentAnalysis {
  infoHash: string;
  name: string;
  totalSize: number;
  files: TorrentFile[];
  trackers: string[];
  scrapeResult: ScrapeAggregationResult;
}

interface PricingSnapshot {
  totalSizeInBytes: number;
  totalSizeInGb: number;
  selectedFiles: number[];
  baseRatePerGb: number;
  userRegion: string;
  regionMultiplier: number;
  healthMultiplier: number;
  isCacheHit: boolean;
  cacheDiscountAmount: number;
  finalPrice: number;
  calculatedAt: string;
}

interface QuoteRequest {
  selectedFileIndices: number[];
  torrentFile: File;
  voucherCode?: string;
}

interface QuoteResponse {
  isReadyToDownload: boolean;
  originalAmountInUSD: number;
  finalAmountInUSD: number;
  finalAmountInNCurrency: number;
  torrentHealth: TorrentHealthMeasurements;
  fileName: string;
  sizeInBytes: number;
  isCached: boolean;
  infoHash: string;
  message?: string;
  pricingDetails: PricingSnapshot;
  invoiceId: number;
}

// Invoice Models
interface Invoice {
  id: number;
  userId: number;
  jobId?: number;
  originalAmountInUSD: number;
  finalAmountInUSD: number;
  finalAmountInNCurrency: number;
  exchangeRate: number;
  cancelledAt?: string;
  paidAt?: string;
  refundedAt?: string;
  walletTransactionId?: number;
  voucherId?: number;
  torrentFileId: number;
  expiresAt: string;
  isExpired: boolean;
}

interface InvoicePaymentResult {
  walletTransaction: number;
  invoiceId: number;
  jobId: number;
  totalAmountInNCurrency: number;
  hasStorageProfileWarning: boolean;
  storageProfileWarningMessage?: string;
}

// Storage Profile Models
interface StorageProfile {
  id: number;
  profileName: string;
  providerType: StorageProviderType;
  isDefault: boolean;
  isActive: boolean;
  createdAt: string;
}

interface GoogleDriveAuthResponse {
  authorizationUrl: string;
}

// Job Models
interface UserJob {
  id: number;
  userId: number;
  storageProfileId: number;
  status: JobStatus;
  type: JobType;
  requestFileId: number;
  errorMessage?: string;
  currentState?: string;
  startedAt?: string;
  completedAt?: string;
  lastHeartbeat?: string;
  bytesDownloaded: number;
  totalBytes: number;
  selectedFileIndices: number[];
  progress: number; // Calculated: bytesDownloaded / totalBytes * 100
}

interface JobCreationResult {
  jobId: number;
  invoiceId: number;
  storageProfileId?: number;
  hasStorageProfileWarning: boolean;
  storageProfileWarningMessage?: string;
}

// Voucher Models
interface Voucher {
  id: number;
  code: string;
  type: DiscountType;
  value: number;
  maxUsesTotal?: number;
  maxUsesPerUser: number;
  expiresAt?: string;
  isActive: boolean;
}

// Admin Models
interface AdminDeposit {
  id: number;
  userId: number;
  userEmail: string;
  userFullName: string;
  amount: number;
  currency: string;
  paymentProvider: string;
  gatewayTransactionId: string;
  status: DepositStatus;
  createdAt: string;
  updatedAt?: string;
}

interface AdminWallet {
  userId: number;
  userEmail: string;
  userFullName: string;
  balance: number;
  transactionCount: number;
  lastTransactionDate?: string;
}

interface AdminAdjustBalanceRequest {
  amount: number;
  description: string;
}

interface ChartDataPoint {
  label: string;
  amount: number;
  count: number;
}

interface AdminDashboard {
  totalDepositsAmount: number;
  totalDepositsCount: number;
  pendingDepositsCount: number;
  completedDepositsCount: number;
  failedDepositsCount: number;
  totalWalletBalance: number;
  totalUsersWithBalance: number;
  dailyDeposits: ChartDataPoint[];
  weeklyDeposits: ChartDataPoint[];
  monthlyDeposits: ChartDataPoint[];
}

// Pagination Models
interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
```

## Application Structure

```
app/
  (auth)/
    login/
      page.tsx
    callback/
      page.tsx
  (dashboard)/
    layout.tsx
    page.tsx (User Dashboard)
    torrents/
      upload/
        page.tsx
      analyze/
        page.tsx
      quote/
        page.tsx
      jobs/
        page.tsx
        [id]/
          page.tsx
    wallet/
      page.tsx
      transactions/
        page.tsx
        [id]/
          page.tsx
      deposits/
        page.tsx
        new/
          page.tsx
        [id]/
          page.tsx
    storage/
      page.tsx
      connect/
        page.tsx
    invoices/
      page.tsx
      [id]/
        page.tsx
  (admin)/
    layout.tsx
    dashboard/
      page.tsx
    payments/
      deposits/
        page.tsx
      wallets/
        page.tsx
      transactions/
        page.tsx
      analytics/
        page.tsx
  api/
    auth/
      [...nextauth]/
        route.ts
  components/
    ui/
      Button.tsx
      Input.tsx
      Select.tsx
      Card.tsx
      Modal.tsx
      Table.tsx
      Pagination.tsx
      Badge.tsx
      ProgressBar.tsx
      FileUpload.tsx
      StatusIndicator.tsx
      Chart.tsx
    layout/
      Header.tsx
      Sidebar.tsx
      Footer.tsx
      Navigation.tsx
    auth/
      LoginForm.tsx
      GoogleLoginButton.tsx
    torrents/
      TorrentUpload.tsx
      TorrentAnalysis.tsx
      FileSelector.tsx
      TorrentHealthIndicator.tsx
      QuoteSummary.tsx
      VoucherInput.tsx
    wallet/
      BalanceCard.tsx
      TransactionList.tsx
      DepositForm.tsx
      PaymentUrlModal.tsx
    storage/
      StorageProfileList.tsx
      StorageProfileCard.tsx
      GoogleDriveConnect.tsx
    jobs/
      JobList.tsx
      JobCard.tsx
      JobProgress.tsx
      JobStatusBadge.tsx
    admin/
      AdminStats.tsx
      AdminTable.tsx
      BalanceAdjustmentModal.tsx
      AnalyticsChart.tsx
  lib/
    types/
      index.ts (all TypeScript interfaces)
    utils/
      formatters.ts (currency, date, file size)
      validators.ts (Zod schemas)
      mockData.ts (mock data generators)
    constants/
      colors.ts
      routes.ts
  hooks/
    useAuth.ts
    useWallet.ts
    useTorrents.ts
    useJobs.ts
    useStorage.ts
```

## Detailed Screen Specifications

### 1. Authentication Flow

#### 1.1 Login Page (`/login`)

**Layout:**
- Centered card on gradient background (Primary Teal to Secondary Teal gradient)
- Logo/App name at top
- "Welcome to TorreClou" heading
- Google OAuth button (large, prominent)
- Footer with terms/privacy links

**Components:**
- `GoogleLoginButton`: Large button with Google icon, uses NextAuth signIn
- Loading state: Button shows spinner when authentication in progress
- Error state: Display error message below button if authentication fails

**Edge Cases:**
- If user is already authenticated, redirect to dashboard
- Handle OAuth callback errors (display user-friendly message)
- Show "Signing in..." state during authentication
- Handle network errors gracefully

#### 1.2 OAuth Callback Page (`/callback`)

**Behavior:**
- Automatically handles OAuth callback
- Shows loading spinner with "Completing sign in..."
- Redirects to dashboard on success
- Shows error message and "Retry" button on failure
- Redirects to login if callback fails

**Edge Cases:**
- Invalid state parameter
- Missing authorization code
- Token exchange failure
- User account creation failure

### 2. User Dashboard (`/`)

**Layout:**
- Header with user info, balance, notifications
- Sidebar navigation
- Main content area with cards/widgets

**Dashboard Cards:**
1. **Balance Card** (Primary Teal background)
   - Current balance (large, bold)
   - Currency (USD)
   - "Add Funds" button (Secondary Teal)
   - Quick link to wallet

2. **Active Jobs Card** (White background, border)
   - Count of active jobs (QUEUED, PROCESSING, UPLOADING)
   - List of 3 most recent active jobs
   - "View All Jobs" link
   - Empty state: "No active jobs" message

3. **Recent Transactions Card**
   - Last 5 transactions
   - Type, amount, date
   - "View All" link
   - Empty state message

4. **Quick Actions Card**
   - "Upload Torrent" button (Primary Teal)
   - "View Wallet" button (outline)
   - "Manage Storage" button (outline)

**Edge Cases:**
- No balance (show $0.00)
- No active jobs (show empty state)
- No transactions (show empty state)
- Loading state for all cards (skeleton loaders)
- Error state per card (retry button)

### 3. Torrent Upload & Analysis Flow

#### 3.1 Upload Torrent Page (`/torrents/upload`)

**Layout:**
- Page title: "Upload Torrent File"
- File upload area (drag & drop + file picker)
- Instructions below upload area

**File Upload Component:**
- Large drop zone (dashed border, Primary Teal on hover)
- "Choose File" button
- Drag & drop support
- File validation:
  - Only .torrent files
  - Max size: 10MB
  - Show error for invalid files
- Upload progress bar (if needed)
- Selected file preview:
  - File name
  - File size
  - Remove button

**Edge Cases:**
- Invalid file type (show error: "Please upload a .torrent file")
- File too large (show error: "File size must be less than 10MB")
- No file selected (disable "Analyze" button)
- Network error during upload (show retry button)
- Duplicate file upload (show warning)

**After Upload:**
- Auto-redirect to analysis page with file data
- Or show "Analyzing..." state

#### 3.2 Torrent Analysis Page (`/torrents/analyze`)

**Layout:**
- Back button to upload
- Torrent file info card
- Analysis results
- "Get Quote" button

**Torrent Info Display:**
- File name (large, bold)
- Total size (formatted: GB, MB)
- Info hash (monospace, copy button)
- Number of files
- Number of trackers

**File List:**
- Expandable/collapsible list
- Each file shows:
  - Index number
  - File path
  - File size
  - Checkbox for selection
- "Select All" / "Deselect All" buttons
- Total selected size indicator

**Tracker List:**
- List of tracker URLs
- Status indicators (if available)

**Analysis Status:**
- Loading: Spinner with "Analyzing torrent..."
- Success: Show all info
- Error: Error message with "Retry" button

**Edge Cases:**
- Analysis timeout (show timeout error)
- Invalid torrent file (show error message)
- No files in torrent (show warning)
- No trackers (show warning)
- Very large file list (virtualize or paginate)

#### 3.3 Quote Generation Page (`/torrents/quote`)

**Layout:**
- Torrent summary at top
- Selected files summary
- Voucher code input (optional)
- Quote details card
- "Pay Invoice" button

**Torrent Summary:**
- File name
- Total size
- Selected files count
- Total selected size

**Voucher Input:**
- Text input with "Apply Voucher" button
- Loading state when validating
- Success: Show discount applied
- Error: Show error message (invalid, expired, etc.)
- Applied voucher display:
  - Code
  - Discount amount/percentage
  - Remove button

**Quote Details Card:**
- **Pricing Breakdown:**
  - Base rate per GB
  - Total size in GB
  - Base price
  - Region multiplier (if applicable)
  - Health multiplier (if applicable)
  - Cache discount (if cached, show in Yellow)
  - Voucher discount (if applied)
  - **Final Price** (large, bold, Primary Teal)
- **Torrent Health:**
  - Health score (0-100)
  - Visual indicator (progress bar, color-coded)
  - Seeders count
  - Leechers count
  - Completed count
  - Health status badges:
    - Healthy (green)
    - Weak (yellow)
    - Dead (orange)
    - Complete (Primary Teal)
- **Invoice Info:**
  - Invoice ID
  - Expires at (countdown timer)
  - Status (if already paid)

**Edge Cases:**
- Insufficient balance (show warning, disable pay button)
- Quote expired (show error, "Get New Quote" button)
- Invalid voucher code (show error message)
- Voucher already used (show error)
- Voucher expired (show error)
- Torrent not ready (show message, disable pay)
- Cache hit (highlight in Yellow)
- Very unhealthy torrent (show warning in Orange)

**Payment Flow:**
- Click "Pay Invoice"
- Check balance
- If insufficient: Show "Add Funds" modal
- If sufficient: Show confirmation modal
- Process payment (mock)
- Show success/error
- Redirect to job creation or invoice page

### 4. Invoice Management

#### 4.1 Invoices List (`/invoices`)

**Layout:**
- Page title: "My Invoices"
- Filter tabs: All, Pending, Paid, Expired
- Invoice cards/table

**Invoice Display:**
- Card or table row per invoice:
  - Invoice ID
  - Torrent file name
  - Amount (Final amount, currency)
  - Status badge:
    - Pending (Yellow)
    - Paid (Primary Teal)
    - Expired (Orange)
    - Cancelled (gray)
  - Created date
  - Expires at (if pending)
  - Actions:
    - View details
    - Pay (if pending)
    - View job (if paid)

**Pagination:**
- Page size selector (10, 25, 50)
- Previous/Next buttons
- Page numbers
- Total count display

**Edge Cases:**
- No invoices (empty state with "Upload your first torrent" message)
- Expired invoices (show expired badge, gray out)
- Very old invoices (group by month/year)
- Loading state (skeleton cards)
- Error loading (retry button)

#### 4.2 Invoice Details (`/invoices/[id]`)

**Layout:**
- Back button
- Invoice header with ID and status
- Invoice details card
- Payment section (if unpaid)
- Related job link (if paid)

**Invoice Details:**
- **Basic Info:**
  - Invoice ID
  - Status badge
  - Created date
  - Expires at (with countdown if pending)
  - Paid at (if paid)
- **Torrent Info:**
  - File name
  - Info hash
  - Total size
  - Selected files
- **Pricing Breakdown:**
  - Same as quote page
  - All multipliers
  - Discounts
  - Final amount
- **Payment Info:**
  - Wallet transaction ID (if paid)
  - Payment method
  - Payment date

**Payment Section (if unpaid):**
- Current balance display
- Invoice amount
- Balance after payment
- "Pay Now" button
- Warning if insufficient balance
- Expiration countdown

**Edge Cases:**
- Invoice not found (404 page)
- Invoice expired (disable pay, show expired message)
- Invoice already paid (show payment details, hide pay button)
- Insufficient balance (show warning, "Add Funds" button)
- Invoice belongs to different user (403 error)

### 5. Wallet Management

#### 5.1 Wallet Overview (`/wallet`)

**Layout:**
- Balance card (large, prominent)
- Quick actions
- Recent transactions preview
- Deposit history preview

**Balance Card:**
- Current balance (very large number, Primary Teal)
- Currency (USD)
- "Add Funds" button (Secondary Teal, large)
- Balance change indicator (if recent transaction)

**Quick Actions:**
- "Add Funds" button
- "View Transactions" link
- "View Deposits" link

**Recent Transactions:**
- Last 5 transactions
- Type, amount, date
- "View All" link

**Recent Deposits:**
- Last 3 deposits
- Status, amount, date
- "View All" link

**Edge Cases:**
- Zero balance (show $0.00, emphasize "Add Funds")
- Negative balance (show in Orange, if possible)
- No transactions (empty state)
- No deposits (empty state)
- Loading state (skeleton)

#### 5.2 Transactions List (`/wallet/transactions`)

**Layout:**
- Page title: "Transaction History"
- Filter dropdown: All Types, Deposits, Payments, Refunds, etc.
- Date range picker
- Transactions table

**Transaction Table:**
- Columns:
  - Date/Time
  - Type (with badge/icon)
  - Description
  - Amount (positive = green/Primary Teal, negative = red/Orange)
  - Reference ID (if available)
  - Actions (View Details)
- Sortable columns
- Pagination

**Transaction Type Badges:**
- DEPOSIT: Primary Teal
- PAYMENT: Orange
- REFUND: Secondary Teal
- ADMIN_ADJUSTMENT: Yellow
- BONUS: Yellow
- DEDUCTION: Orange

**Edge Cases:**
- No transactions (empty state)
- Filter returns no results (show "No transactions match your filters")
- Very long description (truncate with tooltip)
- Large amounts (format with commas)
- Loading state (skeleton rows)
- Error loading (retry button)

#### 5.3 Transaction Details (`/wallet/transactions/[id]`)

**Layout:**
- Back button
- Transaction header
- Details card
- Related invoice link (if applicable)

**Transaction Details:**
- Transaction ID
- Type badge
- Amount (large, color-coded)
- Description
- Date/Time
- Reference ID (if available)
- Related invoice ID (if payment)
- Related deposit ID (if deposit)

**Edge Cases:**
- Transaction not found (404)
- Transaction belongs to different user (403)

#### 5.4 Create Deposit (`/wallet/deposits/new`)

**Layout:**
- Page title: "Add Funds"
- Deposit form
- Supported currencies info
- Minimum amounts info

**Deposit Form:**
- Amount input (number, min = minimum for selected currency)
- Currency selector (dropdown):
  - USDT
  - USDC
  - BUSD
  - Other stablecoins
- Currency info card:
  - Minimum amount (from API, mock data)
  - Fiat equivalent
  - Network info (if applicable)
- "Continue" button

**Form Validation:**
- Amount required
- Amount >= minimum for currency
- Currency required
- Show validation errors inline

**After Submit:**
- Show loading: "Creating deposit..."
- On success: Redirect to deposit details page
- On error: Show error message

**Edge Cases:**
- Amount below minimum (show error with minimum amount)
- Invalid currency (should not happen with dropdown)
- Network error (show retry button)
- Very large amount (show confirmation)

#### 5.5 Deposit Details (`/wallet/deposits/[id]`)

**Layout:**
- Back button
- Deposit header with status
- Deposit details
- Payment URL section (if pending)
- QR code (if available)

**Deposit Details:**
- Deposit ID
- Amount
- Currency
- Status badge:
  - Pending (Yellow)
  - Completed (Primary Teal)
  - Failed (Orange)
  - Expired (Orange)
- Created date
- Updated date (if completed/failed)
- Payment provider
- Gateway transaction ID (if available)

**Payment URL Section (if Pending):**
- Payment URL (copyable)
- "Open Payment Page" button (opens in new tab)
- QR code display (if available)
- Instructions:
  - "Send exactly [amount] [currency]"
  - "Payment will be processed automatically"
  - "This link expires in [time]"
- Expiration countdown

**Status-Specific UI:**
- **Pending:**
  - Show payment URL
  - Show countdown
  - "Refresh Status" button
  - Auto-refresh every 30 seconds
- **Completed:**
  - Success message
  - Transaction link
  - Balance updated notification
- **Failed:**
  - Error message
  - "Try Again" button
- **Expired:**
  - Expired message
  - "Create New Deposit" button

**Edge Cases:**
- Deposit not found (404)
- Deposit belongs to different user (403)
- Payment URL expired (show expired message)
- Network error refreshing status (show retry)

#### 5.6 Deposits List (`/wallet/deposits`)

**Layout:**
- Page title: "Deposit History"
- Filter tabs: All, Pending, Completed, Failed, Expired
- Deposits table/cards

**Deposit Display:**
- Deposit ID
- Amount (with currency)
- Status badge
- Created date
- Actions: View Details

**Pagination:**
- Same as transactions

**Edge Cases:**
- No deposits (empty state)
- Filter returns no results
- Loading state
- Error loading

### 6. Storage Profiles Management

#### 6.1 Storage Profiles List (`/storage`)

**Layout:**
- Page title: "Storage Profiles"
- "Connect Storage" button (Primary Teal)
- Storage profiles grid/list

**Storage Profile Cards:**
- Provider icon/logo
- Profile name
- Provider type
- Default badge (if default)
- Status (Active/Inactive)
- Created date
- Actions:
  - Set as Default (if not default)
  - Disconnect (if active)
  - View Details

**Empty State:**
- Message: "No storage profiles connected"
- "Connect Your First Storage" button (large, Primary Teal)
- Instructions on why storage is needed

**Edge Cases:**
- No profiles (empty state)
- Only one profile (auto-set as default, hide set default button)
- Inactive profile (gray out, show reactivate option)
- Loading state
- Error loading

#### 6.2 Connect Google Drive (`/storage/connect`)

**Layout:**
- Page title: "Connect Google Drive"
- Instructions card
- "Connect Google Drive" button
- OAuth flow handling

**Instructions:**
- Why connect Google Drive
- What permissions are needed
- How to disconnect later

**Connect Flow:**
1. Click "Connect Google Drive"
2. Show loading: "Redirecting to Google..."
3. Redirect to Google OAuth (mock: show modal with OAuth URL)
4. After callback: Show success message
5. Redirect to storage profiles list

**Edge Cases:**
- OAuth error (show error, retry button)
- User cancels OAuth (show message)
- Already connected (show message, redirect to list)
- Network error (retry button)

**After Connection:**
- Show success message
- New profile appears in list
- Auto-set as default if first profile

#### 6.3 Set Default Profile

**Action:**
- Click "Set as Default" on any profile
- Show confirmation modal
- Update profile
- Show success message
- Update UI (move default badge)

**Edge Cases:**
- Network error (show error, retry)
- Profile not found (show error)
- Already default (should not show button)

### 7. Jobs Management

#### 7.1 Jobs List (`/torrents/jobs`)

**Layout:**
- Page title: "My Jobs"
- Filter tabs: All, Queued, Processing, Uploading, Completed, Failed
- Jobs grid/list

**Job Cards:**
- Job ID
- Torrent file name
- Status badge:
  - QUEUED (Yellow)
  - PROCESSING (Primary Teal)
  - UPLOADING (Secondary Teal)
  - COMPLETED (green)
  - FAILED (Orange)
  - CANCELLED (gray)
- Progress bar (if processing/uploading)
- Storage profile name
- Created date
- Actions: View Details

**Progress Bar:**
- Show percentage
- Show bytes downloaded / total bytes
- Animated during active states
- Color: Primary Teal

**Empty State:**
- "No jobs yet" message
- "Upload Your First Torrent" button

**Edge Cases:**
- No jobs (empty state)
- Filter returns no results
- Very long file names (truncate)
- Loading state
- Error loading

#### 7.2 Job Details (`/torrents/jobs/[id]`)

**Layout:**
- Back button
- Job header with status
- Job details card
- Progress section (if active)
- Error section (if failed)
- Storage info

**Job Details:**
- Job ID
- Status badge
- Torrent file name
- Info hash
- Storage profile name
- Created date
- Started date (if started)
- Completed date (if completed)
- Selected files count
- Total size

**Progress Section (if QUEUED, PROCESSING, UPLOADING):**
- Progress bar (animated)
- Percentage
- Bytes downloaded / total bytes
- Speed (if available)
- Estimated time remaining (if available)
- Current state text (e.g., "Downloading files...")
- Auto-refresh every 5 seconds

**Error Section (if FAILED):**
- Error message (red/Orange)
- Error details (if available)
- "Retry Job" button (if applicable)
- "Contact Support" link

**Completed Section (if COMPLETED):**
- Success message
- Completion date
- Link to storage location (if available)
- "Download Files" button (if applicable)

**Edge Cases:**
- Job not found (404)
- Job belongs to different user (403)
- Job stuck (show warning, contact support)
- Very slow progress (show message)
- Network error refreshing (show retry)

### 8. Admin Dashboard

#### 8.1 Admin Layout

**Layout:**
- Admin-specific sidebar
- Admin header
- Restricted routes (Admin role only)

**Sidebar Navigation:**
- Dashboard
- Payments
  - Deposits
  - Wallets
  - Transactions
  - Analytics
- Users (if needed)
- Settings (if needed)

**Access Control:**
- Redirect non-admin users
- Show 403 if accessed without permission

#### 8.2 Admin Dashboard (`/admin/dashboard`)

**Layout:**
- Page title: "Admin Dashboard"
- Stats cards row
- Charts section
- Recent activity

**Stats Cards:**
1. **Total Deposits**
   - Amount (large, Primary Teal)
   - Count
   - Change indicator (vs previous period)

2. **Pending Deposits**
   - Count (Yellow badge)
   - Amount
   - Link to deposits list

3. **Completed Deposits**
   - Count (Primary Teal badge)
   - Amount
   - Link to deposits list

4. **Failed Deposits**
   - Count (Orange badge)
   - Amount
   - Link to deposits list

5. **Total Wallet Balance**
   - Amount (large, Secondary Teal)
   - Users with balance count

6. **Users with Balance**
   - Count
   - Link to wallets list

**Charts Section:**
- **Daily Deposits Chart**
  - Line or bar chart
  - X-axis: Days
  - Y-axis: Amount
  - Tooltip on hover
- **Weekly Deposits Chart**
  - Same format
- **Monthly Deposits Chart**
  - Same format

**Date Range Filter:**
- Date from picker
- Date to picker
- "Apply" button
- "Reset" button

**Recent Activity:**
- Last 10 deposits
- Last 10 transactions
- Last 10 wallet adjustments

**Edge Cases:**
- No data (empty charts, zero stats)
- Date range with no data (show message)
- Loading state (skeleton cards)
- Error loading (retry button)
- Invalid date range (show validation error)

#### 8.3 Admin Deposits (`/admin/payments/deposits`)

**Layout:**
- Page title: "All Deposits"
- Filter dropdown: All Status, Pending, Completed, Failed, Expired
- Date range filter
- Deposits table
- Pagination

**Deposits Table:**
- Columns:
  - Deposit ID
  - User (Email, Full Name)
  - Amount (with currency)
  - Status badge
  - Payment Provider
  - Gateway Transaction ID
  - Created Date
  - Updated Date
  - Actions (View Details)
- Sortable columns
- Export button (CSV, if needed)

**Edge Cases:**
- No deposits (empty state)
- Filter returns no results
- Very long user names (truncate)
- Loading state
- Error loading

#### 8.4 Admin Wallets (`/admin/payments/wallets`)

**Layout:**
- Page title: "User Wallets"
- Search input (by email/name)
- Wallets table
- Pagination

**Wallets Table:**
- Columns:
  - User ID
  - Email
  - Full Name
  - Balance
  - Transaction Count
  - Last Transaction Date
  - Actions:
    - View Transactions
    - Adjust Balance
- Sortable columns

**Edge Cases:**
- No wallets (empty state)
- Search returns no results
- Zero balance users (show $0.00)
- Loading state
- Error loading

#### 8.5 Adjust User Balance (`/admin/payments/wallets/[userId]/adjust`)

**Layout:**
- Back button
- User info card
- Current balance display
- Adjustment form

**User Info:**
- User ID
- Email
- Full Name
- Current Balance

**Adjustment Form:**
- Amount input (positive = add, negative = deduct)
- Description input (required, textarea)
- Preview:
  - Current balance
  - Adjustment amount
  - New balance
- "Apply Adjustment" button

**Form Validation:**
- Amount cannot be zero
- Description required (min 10 characters)
- Show validation errors

**Confirmation Modal:**
- Show adjustment details
- Confirm before applying
- "Cancel" and "Confirm" buttons

**After Submit:**
- Show loading
- On success: Show success message, redirect to wallet
- On error: Show error message

**Edge Cases:**
- Zero amount (show error)
- Empty description (show error)
- Very large adjustment (show warning)
- Negative balance result (show warning, allow if confirmed)
- Network error (retry button)
- User not found (404)

#### 8.6 Admin Transactions (`/admin/payments/transactions`)

**Layout:**
- Page title: "All Transactions"
- Filter dropdown: All Types, Deposits, Payments, etc.
- Date range filter
- Transactions table
- Pagination

**Transactions Table:**
- Same as user transactions, plus:
  - User column (Email, Full Name)
- All other columns same

**Edge Cases:**
- Same as user transactions list

#### 8.7 Admin Analytics (`/admin/payments/analytics`)

**Layout:**
- Page title: "Payment Analytics"
- Date range filter
- Summary cards
- Charts section

**Summary Cards:**
- Total Deposits (amount, count)
- Average Deposit Amount
- Success Rate (%)
- Total Wallet Balance
- Active Users (users with transactions in period)

**Charts:**
- Daily Deposits (line/bar)
- Weekly Deposits (line/bar)
- Monthly Deposits (line/bar)
- Deposit Status Distribution (pie chart)
- Transaction Types Distribution (pie chart)

**Export Options:**
- Export chart data (CSV)
- Export report (PDF, if needed)

**Edge Cases:**
- No data in date range (empty charts)
- Invalid date range
- Loading state
- Error loading

## UI Components Specifications

### Button Component

**Variants:**
- Primary: Primary Teal background, white text
- Secondary: Secondary Teal background, white text
- Outline: Transparent, Primary Teal border and text
- Ghost: Transparent, Primary Teal text on hover
- Danger: Orange background, white text

**Sizes:**
- Small: py-1.5 px-3 text-sm
- Medium: py-2 px-4 text-base (default)
- Large: py-3 px-6 text-lg

**States:**
- Default
- Hover (darker shade)
- Active (pressed)
- Disabled (gray, no interaction)
- Loading (spinner, disabled)

### Input Component

**Variants:**
- Default: White background, Primary Teal border
- Error: Orange border, error message below
- Success: Secondary Teal border

**Types:**
- Text
- Number
- Email
- Password (with show/hide toggle)
- File (custom styled)

**States:**
- Default
- Focus (Primary Teal border, ring)
- Error (Orange border, error message)
- Disabled (gray background)

### Badge Component

**Variants:**
- Primary: Primary Teal background
- Secondary: Secondary Teal background
- Warning: Yellow background
- Error: Orange background
- Success: Green background
- Neutral: Gray background

**Sizes:**
- Small: text-xs px-2 py-0.5
- Medium: text-sm px-2.5 py-1 (default)

### Card Component

**Variants:**
- Default: White background, shadow
- Bordered: White background, border
- Elevated: White background, larger shadow

**Padding:**
- Small: p-4
- Medium: p-6 (default)
- Large: p-8

### Modal Component

**Features:**
- Backdrop (semi-transparent, dark)
- Centered card
- Close button (X)
- Close on backdrop click (optional)
- Close on Escape key
- Focus trap
- Animation (fade in/out)

**Sizes:**
- Small: max-w-sm
- Medium: max-w-md (default)
- Large: max-w-lg
- Extra Large: max-w-2xl

### Table Component

**Features:**
- Striped rows (alternating background)
- Hover effect on rows
- Sortable columns (arrows)
- Responsive (scroll on mobile)
- Empty state
- Loading state (skeleton rows)

### Pagination Component

**Features:**
- Previous/Next buttons
- Page numbers (show max 7, with ellipsis)
- Page size selector
- Total count display
- Disabled states for first/last page

### Progress Bar Component

**Features:**
- Animated fill (Primary Teal)
- Percentage display
- Optional text label
- Indeterminate state (for loading)

**Variants:**
- Default: Primary Teal
- Success: Green
- Warning: Yellow
- Error: Orange

### File Upload Component

**Features:**
- Drag & drop zone
- File picker button
- File preview (name, size, remove)
- Progress indicator
- Error display
- File type validation
- File size validation

### Status Indicator Component

**Features:**
- Colored dot/circle
- Status text
- Optional icon

**Status Colors:**
- Pending: Yellow
- Processing: Primary Teal
- Completed: Green
- Failed: Orange
- Cancelled: Gray

## Responsive Design

**Breakpoints:**
- Mobile: < 640px
- Tablet: 640px - 1024px
- Desktop: > 1024px

**Mobile Adaptations:**
- Sidebar becomes drawer/menu
- Tables become cards
- Stack form fields vertically
- Full-width buttons
- Simplified navigation

## Loading States

**Skeleton Loaders:**
- Cards: Animated gray boxes
- Tables: Animated rows
- Lists: Animated list items
- Forms: Animated input fields

**Spinners:**
- Use Primary Teal color
- Centered in containers
- Size appropriate to context

## Error States

**Error Display:**
- Error message in Orange/red
- Icon (X or Alert)
- Retry button (if applicable)
- User-friendly messages (no technical jargon)

**Error Types:**
- Network errors: "Unable to connect. Please check your internet."
- Not found: "The requested resource was not found."
- Unauthorized: "You don't have permission to access this."
- Validation errors: Show inline with fields
- Server errors: "Something went wrong. Please try again."

## Empty States

**Empty State Components:**
- Icon (relevant to context)
- Heading (e.g., "No transactions yet")
- Description (helpful message)
- Action button (if applicable)

**Examples:**
- No jobs: "Upload your first torrent to get started"
- No transactions: "Your transaction history will appear here"
- No deposits: "Add funds to your wallet"
- No storage: "Connect a storage provider to upload files"

## Accessibility

**Requirements:**
- Semantic HTML
- ARIA labels where needed
- Keyboard navigation
- Focus indicators (Primary Teal ring)
- Alt text for images
- Color contrast (WCAG AA minimum)
- Screen reader support

## Mock Data

Create comprehensive mock data generators for:
- Users (with different roles)
- Transactions (all types)
- Deposits (all statuses)
- Jobs (all statuses)
- Invoices (all statuses)
- Storage profiles
- Torrent files and analysis
- Quotes and pricing
- Admin analytics data

**Mock Data Guidelines:**
- Realistic values
- Various states and statuses
- Edge cases included
- Pagination support
- Date ranges
- Filtering support

## Implementation Notes

1. **No API Calls**: All data should be mocked. Use React state or context to manage data.
2. **TypeScript Strict**: Use strict TypeScript, define all types.
3. **Component Reusability**: Create reusable components, avoid duplication.
4. **Form Validation**: Use Zod schemas for all form validation.
5. **Error Handling**: Handle all error cases gracefully.
6. **Loading States**: Show loading states for all async operations (even if mocked).
7. **Responsive**: Ensure all screens work on mobile, tablet, and desktop.
8. **Consistent Styling**: Use Tailwind classes consistently, create utility classes if needed.
9. **Color Usage**: Stick to the provided color scheme.
10. **Modern UI**: Use modern design patterns (cards, shadows, rounded corners, smooth animations).

## Testing Considerations (for future)

While not implementing tests, structure code to be testable:
- Separate logic from UI
- Use custom hooks for data fetching
- Keep components focused and small
- Use TypeScript for type safety

## File Structure Best Practices

- Group related components in folders
- Use index.ts for clean imports
- Keep components under 300 lines (split if needed)
- Use meaningful file and folder names
- Separate types, utils, and components

## Additional Features to Consider

1. **Notifications**: Toast notifications for success/error (use a library like react-hot-toast)
2. **Search**: Global search (if needed)
3. **Filters**: Advanced filtering on list pages
4. **Sorting**: Sortable columns in tables
5. **Export**: Export data to CSV (admin pages)
6. **Print**: Print-friendly views (invoices, receipts)
7. **Dark Mode**: Optional dark mode (if requested later)

## Edge Cases Summary

**Authentication:**
- Already authenticated user
- OAuth callback errors
- Token expiration
- Network errors

**Torrents:**
- Invalid file types
- File too large
- Empty torrent
- No trackers
- Analysis timeout
- Very large file lists

**Quotes:**
- Expired quotes
- Insufficient balance
- Invalid vouchers
- Expired vouchers
- Already used vouchers
- Unhealthy torrents

**Payments:**
- Insufficient balance
- Payment timeout
- Duplicate payments
- Expired invoices

**Deposits:**
- Amount below minimum
- Expired payment URLs
- Failed deposits
- Network errors

**Jobs:**
- Stuck jobs
- Failed jobs
- Very slow progress
- Missing storage profile

**Storage:**
- OAuth errors
- Already connected
- No default profile
- Inactive profiles

**Admin:**
- Unauthorized access
- No data in date range
- Invalid filters
- Large datasets

**General:**
- Network errors
- Loading states
- Empty states
- Not found (404)
- Unauthorized (403)
- Server errors (500)
- Validation errors
- Very long text (truncate)
- Very large numbers (format)
- Date/time display (timezone aware)
- Pagination edge cases (first page, last page, empty)

This prompt provides comprehensive specifications for building the complete Next.js frontend. Follow it meticulously to ensure all features, edge cases, and UI states are properly implemented.
