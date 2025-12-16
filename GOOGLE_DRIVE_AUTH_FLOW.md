# Google Drive Authentication Flow Documentation

This document describes the complete OAuth 2.0 flow for connecting a user's Google Drive account to the TorreClou backend.

## Quick Reference

**Key Endpoints:**
- `GET /api/storage/google-drive/connect` - Get Google OAuth authorization URL
- `GET /api/storage/google-drive/callback` - Handle OAuth callback (backend endpoint)
- `GET /api/storage/profiles` - Get user's storage profiles

**Authentication:** All endpoints require JWT Bearer token in `Authorization` header

**Response Format:**
- Success: `{ "authorizationUrl": "https://accounts.google.com/..." }` (connect endpoint)
- Success: `{ "value": 123 }` (callback endpoint - profile ID)
- Error: `{ "code": "ERROR_CODE", "message": "Error message" }`

**⚠️ Important:** The callback endpoint currently returns JSON. Since Google redirects the browser to this endpoint, users will see JSON in their browser. See "Frontend Implementation Guide" section for recommended solutions (popup window approach is recommended).

---

## Overview

The Google Drive authentication uses OAuth 2.0 with the following flow:
1. Frontend requests an authorization URL from the backend
2. User is redirected to Google's OAuth consent screen
3. Google redirects back to the backend callback endpoint
4. Backend exchanges the authorization code for tokens
5. Backend creates/updates the user's storage profile
6. Backend should redirect to frontend with success/error status

---

## API Endpoints

### 1. Get Authorization URL

**Endpoint:** `GET /api/storage/google-drive/connect`

**Authentication:** Required (JWT Bearer token)

**Description:** Generates a Google OAuth authorization URL for the authenticated user.

**Request:**
```http
GET /api/storage/google-drive/connect
Authorization: Bearer <jwt_token>
```

**Success Response (200 OK):**
```json
{
  "authorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth?client_id=...&redirect_uri=...&response_type=code&scope=...&access_type=offline&prompt=consent&state=..."
}
```

**Error Responses:**
- `401 Unauthorized` - Missing or invalid JWT token
- `400 Bad Request` - Error generating authorization URL
  ```json
  {
    "code": "AUTH_URL_ERROR",
    "message": "Failed to generate authorization URL"
  }
  ```

**Frontend Implementation:**
```typescript
// Example: Fetch authorization URL
const response = await fetch('/api/storage/google-drive/connect', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

const data = await response.json();
// Redirect user to data.authorizationUrl
window.location.href = data.authorizationUrl;
```

---

### 2. OAuth Callback (Backend Endpoint)

**Endpoint:** `GET /api/storage/google-drive/callback`

**Authentication:** Required (JWT Bearer token)

**Description:** Handles the OAuth callback from Google. This endpoint:
- Validates the OAuth state parameter
- Exchanges the authorization code for access/refresh tokens
- Creates or updates the user's Google Drive storage profile
- Stores the tokens securely

**Request:**
```http
GET /api/storage/google-drive/callback?code=<authorization_code>&state=<state_hash>
Authorization: Bearer <jwt_token>
```

**Query Parameters:**
- `code` (required): Authorization code from Google
- `state` (required): State hash for CSRF protection

**Success Response (200 OK):**
```json
{
  "value": 123  // Profile ID of the created/updated storage profile
}
```

**Error Responses:**
- `400 Bad Request` - Missing code or state
  ```json
  {
    "error": "Missing code or state parameter"
  }
  ```
- `401 Unauthorized` - Invalid or expired state
  ```json
  {
    "code": "INVALID_STATE",
    "message": "Invalid or expired OAuth state"
  }
  ```
- `401 Unauthorized` - User ID mismatch
  ```json
  {
    "code": "USER_MISMATCH",
    "message": "User ID mismatch in OAuth state"
  }
  ```
- `400 Bad Request` - Token exchange failed
  ```json
  {
    "code": "TOKEN_EXCHANGE_FAILED",
    "message": "Failed to exchange authorization code for tokens"
  }
  ```
- `400 Bad Request` - General callback error
  ```json
  {
    "code": "OAUTH_CALLBACK_ERROR",
    "message": "Failed to complete OAuth flow"
  }
  ```

---

## OAuth Flow Details

### State Parameter

The backend generates a secure state parameter for CSRF protection:
- Format: `{userId}:{nonce}` (hashed with SHA256 and Base64 encoded)
- Stored in-memory with 5-minute expiration
- Validated on callback to ensure the request originated from the same user

### Redirect URI Configuration

The redirect URI is configured in `appsettings.json`:
```json
{
  "GoogleDrive": {
    "RedirectUri": "https://localhost:7185/api/storage/google-drive/callback"
  }
}
```

**Important:** This must match exactly with the redirect URI configured in your Google Cloud Console OAuth 2.0 credentials.

### OAuth Scopes

The backend requests the following scope:
- `https://www.googleapis.com/auth/drive.file` - Access to files created by the app

### OAuth Parameters

The authorization URL includes:
- `client_id`: Google OAuth client ID
- `redirect_uri`: Backend callback endpoint (URL encoded)
- `response_type`: `code`
- `scope`: Requested permissions (URL encoded)
- `access_type`: `offline` (to receive refresh token)
- `prompt`: `consent` (to ensure refresh token is provided)
- `state`: Hashed state parameter (URL encoded)

---

## Frontend Implementation Guide

### Step 1: Initiate Connection

When the user clicks "Connect Google Drive":

```typescript
async function connectGoogleDrive() {
  try {
    // Get authorization URL from backend
    const response = await fetch('/api/storage/google-drive/connect', {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${getAuthToken()}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to get authorization URL');
    }

    const data = await response.json();
    
    // Redirect user to Google OAuth
    window.location.href = data.authorizationUrl;
  } catch (error) {
    console.error('Error connecting Google Drive:', error);
    // Show error to user
  }
}
```

### Step 2: Handle Callback

**⚠️ Important Note:** The callback endpoint (`/api/storage/google-drive/callback`) is a **backend endpoint** that Google redirects the browser to. Currently, this endpoint returns JSON, which means users will see JSON in their browser after Google redirects them.

**Current Behavior:**
- Google redirects browser to: `https://localhost:7185/api/storage/google-drive/callback?code=...&state=...`
- Backend processes OAuth and returns JSON: `{ "value": 123 }` (profile ID)
- User sees JSON in browser (not ideal UX)

**Recommended Solutions:**

#### Option A: Use Popup Window (Recommended for Better UX)

Open Google OAuth in a popup window and handle the callback there:

```typescript
async function connectGoogleDrive() {
  try {
    // Get authorization URL
    const response = await fetch('/api/storage/google-drive/connect', {
      headers: {
        'Authorization': `Bearer ${getAuthToken()}`
      }
    });
    
    const data = await response.json();
    const authUrl = data.authorizationUrl;
    
    // Open in popup window
    const popup = window.open(
      authUrl,
      'Google Drive Auth',
      'width=500,height=600,scrollbars=yes,resizable=yes'
    );
    
    // Listen for popup to close or receive message
    const checkClosed = setInterval(() => {
      if (popup?.closed) {
        clearInterval(checkClosed);
        // Check if connection was successful
        checkConnectionStatus();
      }
    }, 1000);
    
    // Alternative: Listen for postMessage from popup
    window.addEventListener('message', (event) => {
      if (event.origin !== window.location.origin) return;
      
      if (event.data.type === 'GOOGLE_DRIVE_CONNECTED') {
        popup?.close();
        showSuccess('Google Drive connected successfully!');
        refreshStorageProfiles();
      } else if (event.data.type === 'GOOGLE_DRIVE_ERROR') {
        popup?.close();
        showError(event.data.message);
      }
    });
    
  } catch (error) {
    console.error('Error:', error);
    showError('Failed to connect Google Drive');
  }
}

async function checkConnectionStatus() {
  // Poll storage profiles to see if new profile was added
  const profiles = await fetchStorageProfiles();
  // Show success/error based on result
}
```

#### Option B: Backend Returns HTML Redirect Page

**Note:** This requires backend modification. The backend callback should return an HTML page that redirects to the frontend:

```csharp
// Backend modification needed
return View("OAuthCallback", new { 
  success = true, 
  profileId = result.Value,
  frontendUrl = "https://your-frontend.com/storage/callback"
});
```

Or return a redirect response:
```csharp
return Redirect($"https://your-frontend.com/storage/callback?success=true&profileId={result.Value}");
```

Then create a frontend callback page:
```typescript
// pages/storage/callback.tsx or app/storage/callback/page.tsx
export default function StorageCallback() {
  const router = useRouter();
  const { success, profileId, error } = router.query;

  useEffect(() => {
    if (success === 'true') {
      showSuccess(`Google Drive connected! Profile ID: ${profileId}`);
      router.push('/storage/profiles');
    } else if (error) {
      showError(decodeURIComponent(error as string));
      router.push('/storage');
    }
  }, [success, profileId, error, router]);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-teal-600 mx-auto"></div>
        <p className="mt-4 text-gray-600">Completing connection...</p>
      </div>
    </div>
  );
}
```

#### Option C: Current Implementation (JSON Response)

If keeping the current JSON response, you can:
1. Instruct users to close the callback tab after seeing the JSON
2. Have the frontend poll the storage profiles endpoint
3. Show a notification when a new profile is detected

```typescript
// After user initiates connection, start polling
let pollCount = 0;
const maxPolls = 30; // 30 seconds

const pollInterval = setInterval(async () => {
  pollCount++;
  
  if (pollCount > maxPolls) {
    clearInterval(pollInterval);
    showError('Connection timeout. Please try again.');
    return;
  }
  
  const profiles = await fetchStorageProfiles();
  const googleDriveProfile = profiles.find(p => p.providerType === 'GoogleDrive');
  
  if (googleDriveProfile) {
    clearInterval(pollInterval);
    showSuccess('Google Drive connected successfully!');
  }
}, 1000);
```

### Step 3: Verify Connection

After successful connection, fetch the user's storage profiles:

```typescript
async function getStorageProfiles() {
  const response = await fetch('/api/storage/profiles', {
    headers: {
      'Authorization': `Bearer ${getAuthToken()}`
    }
  });

  const profiles = await response.json();
  // profiles will include the newly connected Google Drive profile
}
```

---

## Complete Flow Diagram

### Current Implementation Flow

```
┌─────────┐                    ┌──────────┐                    ┌──────────┐
│Frontend │                    │ Backend  │                    │  Google  │
└────┬────┘                    └────┬─────┘                    └────┬─────┘
     │                               │                               │
     │ 1. GET /api/storage/          │                               │
     │    google-drive/connect       │                               │
     ├──────────────────────────────>│                               │
     │                               │                               │
     │ 2. { authorizationUrl: "..." }│                               │
     │<──────────────────────────────┤                               │
     │                               │                               │
     │ 3. Redirect browser to Google │                               │
     ├──────────────────────────────────────────────────────────────>│
     │                               │                               │
     │                               │                               │
     │                               │ 4. User authorizes            │
     │                               │                               │
     │                               │ 5. Browser redirected with    │
     │                               │    code & state                │
     │                               │<───────────────────────────────┤
     │                               │                               │
     │                               │ 6. Exchange code for tokens   │
     │                               │    Create/update profile      │
     │                               │                               │
     │                               │ 7. Return JSON: {value: 123} │
     │                               │    (User sees JSON in browser)│
     │                               │                               │
     │ 8. Frontend detects completion│                               │
     │    (via polling or popup msg) │                               │
     │    Fetch updated profiles     │                               │
     │                               │                               │
```

**Note:** Step 7 currently shows JSON to the user. For better UX, use the popup window approach (see Frontend Implementation Guide).

---

## Error Handling

### Common Errors

1. **AUTH_URL_ERROR**
   - **Cause:** Backend error generating authorization URL
   - **Action:** Retry the request or contact support

2. **INVALID_STATE**
   - **Cause:** OAuth state expired (5 minutes) or invalid
   - **Action:** Restart the connection flow

3. **USER_MISMATCH**
   - **Cause:** User ID in state doesn't match authenticated user
   - **Action:** Ensure user is properly authenticated

4. **TOKEN_EXCHANGE_FAILED**
   - **Cause:** Google rejected the authorization code
   - **Action:** Restart the connection flow

5. **OAUTH_CALLBACK_ERROR**
   - **Cause:** General error during callback processing
   - **Action:** Check logs, retry connection

### Frontend Error Handling Example

```typescript
async function handleGoogleDriveCallback(code: string, state: string) {
  try {
    const response = await fetch(
      `/api/storage/google-drive/callback?code=${code}&state=${state}`,
      {
        headers: {
          'Authorization': `Bearer ${getAuthToken()}`
        }
      }
    );

    if (!response.ok) {
      const error = await response.json();
      
      // Map error codes to user-friendly messages
      const errorMessages: Record<string, string> = {
        'INVALID_STATE': 'The connection request has expired. Please try again.',
        'USER_MISMATCH': 'Authentication error. Please log in again.',
        'TOKEN_EXCHANGE_FAILED': 'Failed to complete connection. Please try again.',
        'OAUTH_CALLBACK_ERROR': 'An error occurred. Please try again later.'
      };

      const message = errorMessages[error.code] || error.message;
      showError(message);
      return;
    }

    const result = await response.json();
    showSuccess(`Google Drive connected! Profile ID: ${result.value}`);
    
    // Refresh storage profiles list
    await refreshStorageProfiles();
    
  } catch (error) {
    console.error('Callback error:', error);
    showError('An unexpected error occurred');
  }
}
```

---

## Storage Profile Structure

After successful connection, a `UserStorageProfile` is created/updated with:

- `Id`: Unique profile identifier
- `UserId`: Associated user ID
- `ProfileName`: "My Google Drive" (default)
- `ProviderType`: `GoogleDrive`
- `IsDefault`: `true` if first profile, otherwise `false`
- `IsActive`: `true`
- `CredentialsJson`: Encrypted JSON containing:
  ```json
  {
    "access_token": "...",
    "refresh_token": "...",
    "expires_at": "2024-01-01T00:00:00Z",
    "token_type": "Bearer"
  }
  ```

---

## Security Considerations

1. **State Parameter:** The state parameter prevents CSRF attacks by ensuring the callback corresponds to the original request.

2. **State Expiration:** States expire after 5 minutes to limit the window for potential attacks.

3. **User Validation:** The callback validates that the user ID in the state matches the authenticated user.

4. **Token Storage:** Tokens are stored securely in the database, not exposed to the frontend.

5. **HTTPS Required:** OAuth requires HTTPS in production (localhost exception for development).

---

## Testing

### Development Setup

1. Ensure `RedirectUri` in `appsettings.json` matches your Google Cloud Console configuration
2. For local development, use `https://localhost:7185/api/storage/google-drive/callback`
3. Ensure your frontend can handle the redirect from the backend callback

### Test Flow

1. Authenticate user and get JWT token
2. Call `GET /api/storage/google-drive/connect`
3. Verify `authorizationUrl` is returned
4. Open the URL in browser
5. Complete Google OAuth consent
6. Verify redirect to backend callback
7. Check that storage profile is created/updated
8. Verify frontend receives success response

---

## API Summary

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/storage/google-drive/connect` | GET | Required | Get Google OAuth authorization URL |
| `/api/storage/google-drive/callback` | GET | Required | Handle OAuth callback from Google |
| `/api/storage/profiles` | GET | Required | Get user's storage profiles |

---

## Questions or Issues?

If you encounter issues:
1. Check that the redirect URI matches exactly in Google Cloud Console
2. Verify JWT token is valid and user is authenticated
3. Check backend logs for detailed error messages
4. Ensure OAuth consent screen is properly configured in Google Cloud Console
