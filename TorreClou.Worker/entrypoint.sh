#!/bin/bash
set -e

# Configure rclone for sync operations (no mount needed - downloads go to block storage)
if [ -n "$BACKBLAZE_KEY_ID" ] && [ -n "$BACKBLAZE_APP_KEY" ]; then
    echo "[ENTRYPOINT] Configuring rclone for Backblaze B2 sync..."
    
    # Configure rclone (needed for rclone copy command after download)
    mkdir -p /root/.config/rclone
    cat > /root/.config/rclone/rclone.conf << EOF
[backblaze]
type = b2
account = ${BACKBLAZE_KEY_ID}
key = ${BACKBLAZE_APP_KEY}
EOF
    
    echo "[ENTRYPOINT] Rclone configured for sync operations"
    echo "[ENTRYPOINT] Downloads will go to block storage, then sync to B2 after completion"
else
    echo "[ENTRYPOINT] Backblaze credentials not provided"
    echo "[ENTRYPOINT] Set BACKBLAZE_KEY_ID and BACKBLAZE_APP_KEY to enable B2 sync"
fi

# Start the .NET worker
echo "[ENTRYPOINT] Starting TorreClou.Worker..."
exec dotnet TorreClou.Worker.dll
