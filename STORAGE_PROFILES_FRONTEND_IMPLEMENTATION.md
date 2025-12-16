# Storage Profiles Frontend Implementation Guide

This document provides comprehensive instructions for implementing the Storage Profiles feature in the frontend, including Google Drive OAuth connection, profile management, and UI components.

## Table of Contents

1. [Overview](#overview)
2. [API Endpoints Reference](#api-endpoints-reference)
3. [TypeScript Types & Interfaces](#typescript-types--interfaces)
4. [OAuth Flow Implementation](#oauth-flow-implementation)
5. [Profile Management](#profile-management)
6. [UI Components](#ui-components)
7. [Error Handling](#error-handling)
8. [State Management](#state-management)
9. [Complete Implementation Example](#complete-implementation-example)

---

## Overview

The Storage Profiles feature allows users to:
- Connect their Google Drive account via OAuth 2.0
- View all connected storage profiles
- Set a default storage profile
- Manage storage connections

**Key Points:**
- The OAuth callback endpoint (`/api/storage/google-drive/callback`) is **unauthenticated** (no JWT required) - this was recently fixed
- The callback endpoint returns JSON (not an HTML redirect)
- Use a popup window approach for the best user experience
- All other endpoints require JWT authentication via NextAuth
- OAuth state is stored in Redis (works across multiple backend instances)
- State expires after 5 minutes for security

**Quick Start:**
1. Create TypeScript types (see section 3)
2. Implement `StorageService` class (see section 4)
3. Create `useStorageProfiles` hook (see section 4)
4. Build UI components (see section 6)
5. Add error handling (see section 7)

---

## API Endpoints Reference

### Base URL
```
Development: http://localhost:7185 (or your API URL)
Production: https://your-api-domain.com
```

### Authentication
All endpoints except the callback require JWT Bearer token:
```
Authorization: Bearer <nextauth_session_token>
```

### 1. Get Authorization URL

**Endpoint:** `GET /api/storage/google-drive/connect`

**Authentication:** Required (JWT Bearer token)

**Description:** Retrieves the Google OAuth authorization URL for the authenticated user.

**Request:**
```typescript
GET /api/storage/google-drive/connect
Headers: {
  Authorization: `Bearer ${session.accessToken}`
}
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

---

### 2. OAuth Callback (Backend Endpoint)

**Endpoint:** `GET /api/storage/google-drive/callback`

**Authentication:** NOT REQUIRED (AllowAnonymous)

**Description:** Handles the OAuth callback from Google. This endpoint:
- Validates the OAuth state parameter (stored in Redis)
- Extracts userId from the validated state
- Exchanges the authorization code for access/refresh tokens
- Creates or updates the user's Google Drive storage profile
- Returns the profile ID

**Request:**
```typescript
GET /api/storage/google-drive/callback?code=<authorization_code>&state=<state_hash>
// No Authorization header needed
```

**Success Response (200 OK):**
```json
{
  "value": 123  // Profile ID of the created/updated storage profile
}
```

**Error Responses:**
- `400 Bad Request` - Missing code or state parameter
  ```json
  {
    "error": "Missing code or state parameter"
  }
  ```
- `400 Bad Request` - Invalid or expired state
  ```json
  {
    "code": "INVALID_STATE",
    "message": "Invalid or expired OAuth state"
  }
  ```
- `400 Bad Request` - Token exchange failed
  ```json
  {
    "code": "TOKEN_EXCHANGE_FAILED",
    "message": "Failed to exchange authorization code for tokens"
  }
  ```
- `500 Internal Server Error` - Redis error
  ```json
  {
    "code": "REDIS_ERROR",
    "message": "Failed to validate OAuth state"
  }
  ```

**Important Notes:**
- This endpoint is called by Google's redirect (not directly by your frontend)
- The endpoint returns JSON, not HTML
- Use a popup window to handle the OAuth flow (see implementation below)

---

### 3. Get Storage Profiles

**Endpoint:** `GET /api/storage/profiles`

**Authentication:** Required (JWT Bearer token)

**Description:** Retrieves all active storage profiles for the authenticated user.

**Request:**
```typescript
GET /api/storage/profiles
Headers: {
  Authorization: `Bearer ${session.accessToken}`
}
```

**Success Response (200 OK):**
```json
[
  {
    "id": 1,
    "profileName": "My Google Drive",
    "providerType": "GoogleDrive",
    "isDefault": true,
    "isActive": true,
    "createdAt": "2024-01-15T10:30:00Z"
  },
  {
    "id": 2,
    "profileName": "My Google Drive",
    "providerType": "GoogleDrive",
    "isDefault": false,
    "isActive": true,
    "createdAt": "2024-01-20T14:45:00Z"
  }
]
```

**Response Order:**
- Default profiles first (`isDefault: true`)
- Then by creation date (`createdAt`)

**Error Responses:**
- `401 Unauthorized` - Missing or invalid JWT token

---

### 4. Set Default Profile

**Endpoint:** `POST /api/storage/profiles/{id}/set-default`

**Authentication:** Required (JWT Bearer token)

**Description:** Sets a storage profile as the default. Automatically unsets all other default profiles for the user.

**Request:**
```typescript
POST /api/storage/profiles/123/set-default
Headers: {
  Authorization: `Bearer ${session.accessToken}`
}
```

**Success Response (200 OK):**
```json
null  // Empty response on success
```

**Error Responses:**
- `401 Unauthorized` - Missing or invalid JWT token
- `400 Bad Request` - Profile not found
  ```json
  {
    "code": "PROFILE_NOT_FOUND",
    "message": "Storage profile not found"
  }
  ```

---

## TypeScript Types & Interfaces

```typescript
// Storage Profile Types
export interface StorageProfile {
  id: number;
  profileName: string;
  providerType: 'GoogleDrive' | string; // Currently only GoogleDrive
  isDefault: boolean;
  isActive: boolean;
  createdAt: string; // ISO 8601 date string
}

// API Response Types
export interface GoogleDriveAuthResponse {
  authorizationUrl: string;
}

export interface ApiError {
  code: string;
  message: string;
}

export interface ApiSuccess<T> {
  value: T;
}

// OAuth Callback Response
export type OAuthCallbackResponse = ApiSuccess<number> | ApiError;

// Profile List Response
export type ProfilesResponse = StorageProfile[];
```

---

## OAuth Flow Implementation

### Recommended Approach: Popup Window

The popup window approach provides the best user experience because:
1. User stays on your page
2. No full-page redirects
3. Easy to handle success/error states
4. Clean UX with loading states

### Implementation Steps

#### Step 1: Create OAuth Service/Utility

```typescript
// lib/api/storage.ts or services/storageService.ts
import { getSession } from 'next-auth/react';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:7185';

export class StorageService {
  private async getAuthHeaders(): Promise<HeadersInit> {
    const session = await getSession();
    if (!session?.accessToken) {
      throw new Error('Not authenticated');
    }
    return {
      'Authorization': `Bearer ${session.accessToken}`,
      'Content-Type': 'application/json',
    };
  }

  /**
   * Gets the Google OAuth authorization URL
   */
  async getGoogleDriveAuthUrl(): Promise<string> {
    const headers = await this.getAuthHeaders();
    const response = await fetch(`${API_BASE_URL}/api/storage/google-drive/connect`, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      const error: ApiError = await response.json();
      throw new Error(error.message || 'Failed to get authorization URL');
    }

    const data: GoogleDriveAuthResponse = await response.json();
    return data.authorizationUrl;
  }

  /**
   * Opens Google OAuth in a popup window and handles the callback
   * 
   * Since the backend callback returns JSON, we need to:
   * 1. Open popup with Google OAuth
   * 2. Poll for popup to navigate to callback URL
   * 3. Extract code and state from URL
   * 4. Call the callback endpoint from the main window (not popup)
   * 5. Handle the response and close popup
   */
  async connectGoogleDrive(): Promise<number> {
    return new Promise((resolve, reject) => {
      // Get authorization URL
      this.getGoogleDriveAuthUrl()
        .then((authUrl) => {
          // Open popup window
          const width = 500;
          const height = 600;
          const left = window.screenX + (window.outerWidth - width) / 2;
          const top = window.screenY + (window.outerHeight - height) / 2;

          const popup = window.open(
            authUrl,
            'Google Drive Authorization',
            `width=${width},height=${height},left=${left},top=${top},resizable=yes,scrollbars=yes`
          );

          if (!popup) {
            reject(new Error('Popup blocked. Please allow popups for this site.'));
            return;
          }

          // Poll for popup to close (user cancelled)
          const pollTimer = setInterval(() => {
            if (popup.closed) {
              clearInterval(pollTimer);
              clearInterval(checkCallback);
              reject(new Error('Authorization cancelled'));
            }
          }, 1000);

          // Poll for callback URL
          const checkCallback = setInterval(() => {
            try {
              // Check if popup is on callback URL
              // Note: This will throw cross-origin error if popup is still on Google's domain
              const popupUrl = popup.location.href;
              
              if (popupUrl.includes('/api/storage/google-drive/callback')) {
                clearInterval(checkCallback);
                clearInterval(pollTimer);

                // Extract code and state from URL
                const url = new URL(popupUrl);
                const code = url.searchParams.get('code');
                const state = url.searchParams.get('state');

                if (code && state) {
                  // Close popup immediately (it will show JSON, which is not user-friendly)
                  popup.close();

                  // Call the callback endpoint from main window
                  // This ensures we can read the JSON response properly
                  fetch(`${API_BASE_URL}/api/storage/google-drive/callback?code=${encodeURIComponent(code)}&state=${encodeURIComponent(state)}`)
                    .then(async (res) => {
                      if (res.ok) {
                        const data: OAuthCallbackResponse = await res.json();
                        if ('value' in data) {
                          resolve(data.value);
                        } else {
                          reject(new Error(data.message || 'OAuth failed'));
                        }
                      } else {
                        const error: ApiError = await res.json();
                        reject(new Error(error.message || 'OAuth callback failed'));
                      }
                    })
                    .catch((err) => {
                      reject(err);
                    });
                } else {
                  popup.close();
                  reject(new Error('Missing code or state in callback'));
                }
              }
            } catch (e) {
              // Cross-origin error - popup is still on Google's domain or callback page
              // This is expected, continue polling
              // The error occurs because we can't read popup.location.href when it's on a different origin
            }
          }, 500);

          // Timeout after 5 minutes
          setTimeout(() => {
            if (!popup.closed) {
              popup.close();
            }
            clearInterval(checkCallback);
            clearInterval(pollTimer);
            reject(new Error('Authorization timeout'));
          }, 5 * 60 * 1000);
        })
        .catch(reject);
    });
  }

  /**
   * Gets all storage profiles for the current user
   */
  async getProfiles(): Promise<StorageProfile[]> {
    const headers = await this.getAuthHeaders();
    const response = await fetch(`${API_BASE_URL}/api/storage/profiles`, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      const error: ApiError = await response.json();
      throw new Error(error.message || 'Failed to fetch profiles');
    }

    const profiles: ProfilesResponse = await response.json();
    return profiles;
  }

  /**
   * Sets a profile as default
   */
  async setDefaultProfile(profileId: number): Promise<void> {
    const headers = await this.getAuthHeaders();
    const response = await fetch(`${API_BASE_URL}/api/storage/profiles/${profileId}/set-default`, {
      method: 'POST',
      headers,
    });

    if (!response.ok) {
      const error: ApiError = await response.json();
      throw new Error(error.message || 'Failed to set default profile');
    }
  }
}

export const storageService = new StorageService();
```

#### Step 2: Create React Hook for Storage Profiles

```typescript
// hooks/useStorageProfiles.ts
import { useState, useEffect, useCallback } from 'react';
import { storageService, StorageProfile } from '@/lib/api/storage';
import { useSession } from 'next-auth/react';

export function useStorageProfiles() {
  const { data: session, status } = useSession();
  const [profiles, setProfiles] = useState<StorageProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchProfiles = useCallback(async () => {
    if (status !== 'authenticated') {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await storageService.getProfiles();
      setProfiles(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load profiles');
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => {
    fetchProfiles();
  }, [fetchProfiles]);

  const connectGoogleDrive = useCallback(async (): Promise<number> => {
    try {
      setError(null);
      const profileId = await storageService.connectGoogleDrive();
      // Refresh profiles after successful connection
      await fetchProfiles();
      return profileId;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to connect Google Drive';
      setError(errorMessage);
      throw err;
    }
  }, [fetchProfiles]);

  const setDefault = useCallback(async (profileId: number) => {
    try {
      setError(null);
      await storageService.setDefaultProfile(profileId);
      // Refresh profiles to get updated default status
      await fetchProfiles();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to set default profile';
      setError(errorMessage);
      throw err;
    }
  }, [fetchProfiles]);

  return {
    profiles,
    loading,
    error,
    connectGoogleDrive,
    setDefault,
    refresh: fetchProfiles,
  };
}
```

---

## Profile Management

### Loading Profiles

```typescript
// Example: Load profiles on page mount
import { useStorageProfiles } from '@/hooks/useStorageProfiles';

function StorageProfilesPage() {
  const { profiles, loading, error } = useStorageProfiles();

  if (loading) {
    return <div>Loading profiles...</div>;
  }

  if (error) {
    return <div>Error: {error}</div>;
  }

  return (
    <div>
      {profiles.map((profile) => (
        <ProfileCard key={profile.id} profile={profile} />
      ))}
    </div>
  );
}
```

### Connecting Google Drive

```typescript
// Example: Connect button handler
function ConnectGoogleDriveButton() {
  const { connectGoogleDrive, loading, error } = useStorageProfiles();
  const [connecting, setConnecting] = useState(false);

  const handleConnect = async () => {
    try {
      setConnecting(true);
      const profileId = await connectGoogleDrive();
      // Show success message
      toast.success('Google Drive connected successfully!');
    } catch (err) {
      // Error is already set in the hook
      toast.error(err instanceof Error ? err.message : 'Failed to connect');
    } finally {
      setConnecting(false);
    }
  };

  return (
    <button
      onClick={handleConnect}
      disabled={connecting || loading}
    >
      {connecting ? 'Connecting...' : 'Connect Google Drive'}
    </button>
  );
}
```

### Setting Default Profile

```typescript
// Example: Set default profile
function ProfileCard({ profile }: { profile: StorageProfile }) {
  const { setDefault } = useStorageProfiles();
  const [updating, setUpdating] = useState(false);

  const handleSetDefault = async () => {
    try {
      setUpdating(true);
      await setDefault(profile.id);
      toast.success('Default profile updated');
    } catch (err) {
      toast.error('Failed to set default profile');
    } finally {
      setUpdating(false);
    }
  };

  return (
    <div>
      <h3>{profile.profileName}</h3>
      {profile.isDefault && <span>Default</span>}
      {!profile.isDefault && (
        <button onClick={handleSetDefault} disabled={updating}>
          {updating ? 'Updating...' : 'Set as Default'}
        </button>
      )}
    </div>
  );
}
```

---

## UI Components

### 1. Storage Profiles List Component

```typescript
// components/StorageProfilesList.tsx
'use client';

import { useStorageProfiles } from '@/hooks/useStorageProfiles';
import { StorageProfileCard } from './StorageProfileCard';
import { ConnectGoogleDriveButton } from './ConnectGoogleDriveButton';

export function StorageProfilesList() {
  const { profiles, loading, error, refresh } = useStorageProfiles();

  if (loading) {
    return (
      <div className="flex items-center justify-center p-8">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        <span className="ml-2">Loading profiles...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <p className="text-red-800">Error: {error}</p>
        <button
          onClick={refresh}
          className="mt-2 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
        >
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold">Storage Profiles</h2>
        <ConnectGoogleDriveButton />
      </div>

      {profiles.length === 0 ? (
        <div className="text-center py-12 border-2 border-dashed rounded-lg">
          <p className="text-gray-500 mb-4">No storage profiles connected</p>
          <ConnectGoogleDriveButton />
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {profiles.map((profile) => (
            <StorageProfileCard key={profile.id} profile={profile} />
          ))}
        </div>
      )}
    </div>
  );
}
```

### 2. Storage Profile Card Component

```typescript
// components/StorageProfileCard.tsx
'use client';

import { StorageProfile } from '@/lib/api/storage';
import { useStorageProfiles } from '@/hooks/useStorageProfiles';
import { useState } from 'react';

interface StorageProfileCardProps {
  profile: StorageProfile;
}

export function StorageProfileCard({ profile }: StorageProfileCardProps) {
  const { setDefault } = useStorageProfiles();
  const [updating, setUpdating] = useState(false);

  const handleSetDefault = async () => {
    if (profile.isDefault) return;

    try {
      setUpdating(true);
      await setDefault(profile.id);
    } catch (err) {
      console.error('Failed to set default:', err);
    } finally {
      setUpdating(false);
    }
  };

  const getProviderIcon = (provider: string) => {
    switch (provider) {
      case 'GoogleDrive':
        return '📁'; // Or use an icon library
      default:
        return '💾';
    }
  };

  return (
    <div className="border rounded-lg p-6 shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          <span className="text-2xl">{getProviderIcon(profile.providerType)}</span>
          <div>
            <h3 className="font-semibold text-lg">{profile.profileName}</h3>
            <p className="text-sm text-gray-500 capitalize">{profile.providerType}</p>
          </div>
        </div>
        {profile.isDefault && (
          <span className="px-2 py-1 text-xs font-medium bg-blue-100 text-blue-800 rounded">
            Default
          </span>
        )}
      </div>

      <div className="text-sm text-gray-600 mb-4">
        <p>Connected: {new Date(profile.createdAt).toLocaleDateString()}</p>
      </div>

      {!profile.isDefault && (
        <button
          onClick={handleSetDefault}
          disabled={updating}
          className="w-full px-4 py-2 bg-gray-100 hover:bg-gray-200 rounded transition-colors disabled:opacity-50"
        >
          {updating ? 'Updating...' : 'Set as Default'}
        </button>
      )}
    </div>
  );
}
```

### 3. Connect Google Drive Button Component

```typescript
// components/ConnectGoogleDriveButton.tsx
'use client';

import { useStorageProfiles } from '@/hooks/useStorageProfiles';
import { useState } from 'react';
import { toast } from 'sonner'; // or your toast library

export function ConnectGoogleDriveButton() {
  const { connectGoogleDrive } = useStorageProfiles();
  const [connecting, setConnecting] = useState(false);

  const handleConnect = async () => {
    try {
      setConnecting(true);
      const profileId = await connectGoogleDrive();
      toast.success('Google Drive connected successfully!', {
        description: `Profile ID: ${profileId}`,
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to connect Google Drive';
      toast.error('Connection Failed', {
        description: errorMessage,
      });
    } finally {
      setConnecting(false);
    }
  };

  return (
    <button
      onClick={handleConnect}
      disabled={connecting}
      className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
    >
      {connecting ? (
        <>
          <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
          <span>Connecting...</span>
        </>
      ) : (
        <>
          <span>📁</span>
          <span>Connect Google Drive</span>
        </>
      )}
    </button>
  );
}
```

---

## Error Handling

### Error Types

```typescript
// lib/errors/storageErrors.ts
export enum StorageErrorCode {
  AUTH_URL_ERROR = 'AUTH_URL_ERROR',
  INVALID_STATE = 'INVALID_STATE',
  TOKEN_EXCHANGE_FAILED = 'TOKEN_EXCHANGE_FAILED',
  OAUTH_CALLBACK_ERROR = 'OAUTH_CALLBACK_ERROR',
  REDIS_ERROR = 'REDIS_ERROR',
  PROFILE_NOT_FOUND = 'PROFILE_NOT_FOUND',
  NOT_AUTHENTICATED = 'NOT_AUTHENTICATED',
  NETWORK_ERROR = 'NETWORK_ERROR',
}

export function getErrorMessage(code: string): string {
  const messages: Record<string, string> = {
    [StorageErrorCode.AUTH_URL_ERROR]: 'Failed to generate authorization URL. Please try again.',
    [StorageErrorCode.INVALID_STATE]: 'The authorization request has expired or is invalid. Please try connecting again.',
    [StorageErrorCode.TOKEN_EXCHANGE_FAILED]: 'Failed to complete authorization. Please try again.',
    [StorageErrorCode.OAUTH_CALLBACK_ERROR]: 'An error occurred during authorization. Please try again.',
    [StorageErrorCode.REDIS_ERROR]: 'Service temporarily unavailable. Please try again later.',
    [StorageErrorCode.PROFILE_NOT_FOUND]: 'Storage profile not found.',
    [StorageErrorCode.NOT_AUTHENTICATED]: 'Please sign in to continue.',
    [StorageErrorCode.NETWORK_ERROR]: 'Network error. Please check your connection.',
  };

  return messages[code] || 'An unexpected error occurred.';
}
```

### Error Handling in Components

```typescript
// Example: Comprehensive error handling
function StorageProfilesPage() {
  const { profiles, loading, error, connectGoogleDrive } = useStorageProfiles();
  const [connectionError, setConnectionError] = useState<string | null>(null);

  const handleConnect = async () => {
    try {
      setConnectionError(null);
      await connectGoogleDrive();
    } catch (err) {
      if (err instanceof Error) {
        setConnectionError(getErrorMessage(err.message));
      } else {
        setConnectionError('Failed to connect Google Drive');
      }
    }
  };

  return (
    <div>
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4">
          <p className="text-red-800">{error}</p>
        </div>
      )}

      {connectionError && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-4">
          <p className="text-yellow-800">{connectionError}</p>
        </div>
      )}

      {/* Rest of component */}
    </div>
  );
}
```

---

## State Management

### Option 1: React Context (Recommended for small apps)

```typescript
// contexts/StorageProfilesContext.tsx
'use client';

import React, { createContext, useContext, ReactNode } from 'react';
import { useStorageProfiles } from '@/hooks/useStorageProfiles';
import { StorageProfile } from '@/lib/api/storage';

interface StorageProfilesContextType {
  profiles: StorageProfile[];
  loading: boolean;
  error: string | null;
  connectGoogleDrive: () => Promise<number>;
  setDefault: (profileId: number) => Promise<void>;
  refresh: () => Promise<void>;
}

const StorageProfilesContext = createContext<StorageProfilesContextType | undefined>(undefined);

export function StorageProfilesProvider({ children }: { children: ReactNode }) {
  const storageProfiles = useStorageProfiles();

  return (
    <StorageProfilesContext.Provider value={storageProfiles}>
      {children}
    </StorageProfilesContext.Provider>
  );
}

export function useStorageProfilesContext() {
  const context = useContext(StorageProfilesContext);
  if (context === undefined) {
    throw new Error('useStorageProfilesContext must be used within StorageProfilesProvider');
  }
  return context;
}
```

### Option 2: Zustand Store (Recommended for larger apps)

```typescript
// stores/storageProfilesStore.ts
import { create } from 'zustand';
import { storageService, StorageProfile } from '@/lib/api/storage';

interface StorageProfilesState {
  profiles: StorageProfile[];
  loading: boolean;
  error: string | null;
  fetchProfiles: () => Promise<void>;
  connectGoogleDrive: () => Promise<number>;
  setDefaultProfile: (profileId: number) => Promise<void>;
}

export const useStorageProfilesStore = create<StorageProfilesState>((set, get) => ({
  profiles: [],
  loading: false,
  error: null,

  fetchProfiles: async () => {
    set({ loading: true, error: null });
    try {
      const profiles = await storageService.getProfiles();
      set({ profiles, loading: false });
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load profiles',
        loading: false,
      });
    }
  },

  connectGoogleDrive: async () => {
    set({ error: null });
    try {
      const profileId = await storageService.connectGoogleDrive();
      // Refresh profiles after connection
      await get().fetchProfiles();
      return profileId;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to connect';
      set({ error: errorMessage });
      throw err;
    }
  },

  setDefaultProfile: async (profileId: number) => {
    set({ error: null });
    try {
      await storageService.setDefaultProfile(profileId);
      // Refresh profiles to get updated default status
      await get().fetchProfiles();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to set default';
      set({ error: errorMessage });
      throw err;
    }
  },
}));
```

---

## Complete Implementation Example

### Full Page Component

```typescript
// app/storage/page.tsx or pages/storage.tsx
'use client';

import { useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import { StorageProfilesList } from '@/components/StorageProfilesList';

export default function StoragePage() {
  const { data: session, status } = useSession();
  const router = useRouter();

  useEffect(() => {
    if (status === 'unauthenticated') {
      router.push('/auth/signin');
    }
  }, [status, router]);

  if (status === 'loading') {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (status === 'unauthenticated') {
    return null; // Will redirect
  }

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold mb-8">Storage Profiles</h1>
      <StorageProfilesList />
    </div>
  );
}
```

---

## OAuth Flow Diagram

```
┌─────────────┐
│   Frontend   │
│   (Next.js)  │
└──────┬───────┘
       │
       │ 1. GET /api/storage/google-drive/connect
       │    (with JWT token)
       │
       ▼
┌─────────────┐
│    Backend  │
│     API     │
└──────┬──────┘
       │
       │ 2. Generate state, store in Redis (5min TTL)
       │    Return authorizationUrl
       │
       ▼
┌─────────────┐
│   Frontend  │
│   (Popup)   │
└──────┬───────┘
       │
       │ 3. Redirect to Google OAuth
       │
       ▼
┌─────────────┐
│   Google    │
│   OAuth     │
└──────┬──────┘
       │
       │ 4. User authorizes
       │
       │ 5. Redirect to /api/storage/google-drive/callback
       │    ?code=...&state=...
       │
       ▼
┌─────────────┐
│    Backend  │
│     API     │
└──────┬──────┘
       │
       │ 6. Validate state (from Redis - atomic get+delete)
       │    Extract userId from state
       │    Exchange code for tokens
       │    Create/update profile
       │    Return profile ID (JSON)
       │
       ▼
┌─────────────┐
│   Frontend  │
│   (Popup)   │
└──────┬───────┘
       │
       │ 7. Frontend detects callback URL in popup
       │    Extracts code & state
       │    Closes popup
       │    Calls callback endpoint from main window
       │
       ▼
┌─────────────┐
│    Backend  │
│     API     │
└──────┬──────┘
       │
       │ 8. Returns profile ID (JSON)
       │
       ▼
┌─────────────┐
│   Frontend  │
│  (Main App) │
└──────┬───────┘
       │
       │ 9. Refresh profiles list
       │    Show success message
       │
       ▼
┌─────────────┐
│    User     │
│   Sees UI   │
└─────────────┘
```

## Alternative: Callback Page Approach

If the popup polling approach has issues, you can create a dedicated callback page:

### Create Callback Page

```typescript
// app/storage/google-drive/callback/page.tsx
'use client';

import { useEffect } from 'react';
import { useSearchParams } from 'next/navigation';

export default function GoogleDriveCallbackPage() {
  const searchParams = useSearchParams();
  const code = searchParams.get('code');
  const state = searchParams.get('state');

  useEffect(() => {
    if (code && state && window.opener) {
      // We're in a popup, send message to parent
      const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:7185';
      
      fetch(`${API_BASE_URL}/api/storage/google-drive/callback?code=${encodeURIComponent(code)}&state=${encodeURIComponent(state)}`)
        .then(async (res) => {
          const data = await res.json();
          
          if (res.ok && 'value' in data) {
            window.opener.postMessage({
              type: 'GOOGLE_DRIVE_OAUTH_SUCCESS',
              profileId: data.value,
            }, window.location.origin);
          } else {
            window.opener.postMessage({
              type: 'GOOGLE_DRIVE_OAUTH_ERROR',
              error: data.message || 'OAuth failed',
            }, window.location.origin);
          }
          
          window.close();
        })
        .catch((err) => {
          window.opener.postMessage({
            type: 'GOOGLE_DRIVE_OAUTH_ERROR',
            error: err.message || 'Network error',
          }, window.location.origin);
          window.close();
        });
    }
  }, [code, state]);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
        <p>Completing authorization...</p>
      </div>
    </div>
  );
}
```

### Update Backend Redirect URI (For Callback Page Approach)

**Important:** If using the callback page approach, you need to update the backend's `GoogleDrive:RedirectUri` in `appsettings.json` to point to your frontend callback page instead of the backend API:

**Current (Backend API):**
```json
{
  "GoogleDrive": {
    "RedirectUri": "https://localhost:7185/api/storage/google-drive/callback"
  }
}
```

**Updated (Frontend Callback Page):**
```json
{
  "GoogleDrive": {
    "RedirectUri": "http://localhost:3000/storage/google-drive/callback"
  }
}
```

**Also update in Google Cloud Console:**
- Go to Google Cloud Console → APIs & Services → Credentials
- Edit your OAuth 2.0 Client ID
- Add `http://localhost:3000/storage/google-drive/callback` to Authorized redirect URIs
- Save changes

**Note:** The popup polling approach (first implementation) works with the current backend redirect URI and doesn't require backend changes.

Then update the OAuth service to use postMessage:

```typescript
async connectGoogleDrive(): Promise<number> {
  return new Promise((resolve, reject) => {
    this.getGoogleDriveAuthUrl()
      .then((authUrl) => {
        const popup = window.open(authUrl, 'Google Drive Authorization', 'width=500,height=600');
        
        if (!popup) {
          reject(new Error('Popup blocked'));
          return;
        }

        const messageListener = (event: MessageEvent) => {
          if (event.origin !== window.location.origin) return;

          if (event.data.type === 'GOOGLE_DRIVE_OAUTH_SUCCESS') {
            window.removeEventListener('message', messageListener);
            resolve(event.data.profileId);
          } else if (event.data.type === 'GOOGLE_DRIVE_OAUTH_ERROR') {
            window.removeEventListener('message', messageListener);
            reject(new Error(event.data.error));
          }
        };

        window.addEventListener('message', messageListener);

        const pollTimer = setInterval(() => {
          if (popup.closed) {
            clearInterval(pollTimer);
            window.removeEventListener('message', messageListener);
            reject(new Error('Authorization cancelled'));
          }
        }, 1000);
      })
      .catch(reject);
  });
}
```

---

## Testing Checklist

- [ ] User can view storage profiles list
- [ ] User can click "Connect Google Drive" button
- [ ] Popup window opens with Google OAuth
- [ ] User can authorize Google Drive access
- [ ] Popup closes after successful authorization
- [ ] Profiles list refreshes automatically
- [ ] New profile appears in the list
- [ ] User can set a profile as default
- [ ] Only one profile can be default at a time
- [ ] Error messages display correctly
- [ ] Loading states work properly
- [ ] Works with NextAuth session management
- [ ] Handles expired/invalid OAuth states
- [ ] Handles network errors gracefully
- [ ] Handles popup blockers

---

## Environment Variables

Add to your `.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:7185
# Or your production API URL
```

---

## Security Considerations

1. **Always verify the origin** when using postMessage for OAuth callbacks
2. **Never store OAuth tokens** in localStorage or client-side state
3. **Use HTTPS in production** for all API calls
4. **Validate session** before making authenticated API calls
5. **Handle token refresh** if your NextAuth session expires

---

## Common Issues & Solutions

### Issue: Popup Blocked
**Solution:** Show a message asking user to allow popups, or fall back to full-page redirect

### Issue: Callback Returns JSON Instead of HTML
**Solution:** Use popup window approach and parse the JSON response, or implement a callback page that reads the response and posts a message to the parent window

### Issue: State Expired
**Solution:** Show user-friendly error message and allow them to retry the connection

### Issue: CORS Errors
**Solution:** Ensure backend CORS is configured for your frontend domain (already configured for development)

---

## Implementation Checklist for Frontend AI Agent

### Phase 1: Setup & Types
- [ ] Create TypeScript type definitions (`StorageProfile`, `GoogleDriveAuthResponse`, `ApiError`, etc.)
- [ ] Set up environment variable `NEXT_PUBLIC_API_URL`
- [ ] Create API service class structure

### Phase 2: Core Functionality
- [ ] Implement `StorageService.getGoogleDriveAuthUrl()` method
- [ ] Implement `StorageService.connectGoogleDrive()` method (popup approach)
- [ ] Implement `StorageService.getProfiles()` method
- [ ] Implement `StorageService.setDefaultProfile()` method
- [ ] Add proper error handling for all API calls

### Phase 3: React Integration
- [ ] Create `useStorageProfiles` custom hook
- [ ] Add loading states
- [ ] Add error states
- [ ] Implement profile refresh functionality

### Phase 4: UI Components
- [ ] Create `StorageProfilesList` component
- [ ] Create `StorageProfileCard` component
- [ ] Create `ConnectGoogleDriveButton` component
- [ ] Add loading spinners and skeletons
- [ ] Add empty state (no profiles)
- [ ] Style components to match design system

### Phase 5: OAuth Flow
- [ ] Test popup window opening
- [ ] Test OAuth authorization flow
- [ ] Test callback handling
- [ ] Test popup closing on success/error
- [ ] Test profile list refresh after connection

### Phase 6: Error Handling
- [ ] Handle popup blocked errors
- [ ] Handle OAuth cancellation
- [ ] Handle expired state errors
- [ ] Handle network errors
- [ ] Display user-friendly error messages
- [ ] Add retry functionality

### Phase 7: Polish
- [ ] Add toast notifications for success/error
- [ ] Add loading states during operations
- [ ] Add confirmation dialogs (if needed)
- [ ] Test with NextAuth session management
- [ ] Test across different browsers
- [ ] Test on mobile devices (if applicable)

## Next Steps

1. Implement the TypeScript types
2. Create the storage service/API client
3. Create the React hook
4. Build the UI components
5. Add error handling and loading states
6. Test the complete OAuth flow
7. Add toast notifications for user feedback
8. Style the components to match your design system

---

## Additional Resources

- [NextAuth.js Documentation](https://next-auth.js.org/)
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [React Popup Window Best Practices](https://developer.mozilla.org/en-US/docs/Web/API/Window/open)
