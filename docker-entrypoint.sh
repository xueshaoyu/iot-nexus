#!/bin/sh
# Ensure volume mounts exist and are writable, then drop root if we started as root.
set -eu

mkdir -p /app/Data /app/Data/Config /app/Data/Backup /app/Data/Plugins /app/Log

# NewLife writes Config/*.config via BasePath + "Config" (/app/Config).
# Keep those files on the data volume without moving PathHelper.BasePath
# (that would turn BasePath+"Log" into /app/DataLog).
if [ ! -e /app/Config ]; then
    ln -s /app/Data/Config /app/Config
elif [ -d /app/Config ] && [ ! -L /app/Config ]; then
    # Image layer leftover: move into the volume once, then replace with a link.
    cp -a /app/Config/. /app/Data/Config/ 2>/dev/null || true
    rm -rf /app/Config
    ln -s /app/Data/Config /app/Config
fi

if [ ! -f /app/Data/Config/Core.config ]; then
    cat > /app/Data/Config/Core.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Core>
  <LogPath>/app/Log</LogPath>
  <DataPath>/app/Data</DataPath>
  <BackupPath>/app/Data/Backup</BackupPath>
  <PluginPath>/app/Data/Plugins</PluginPath>
</Core>
EOF
fi

uid="${APP_UID:-1654}"
if [ "$(id -u)" = "0" ]; then
    chown -R "${uid}:${uid}" /app/Data /app/Log || true
    chown -h "${uid}:${uid}" /app/Config || true
    if command -v setpriv >/dev/null 2>&1; then
        exec setpriv --reuid="${uid}" --regid="${uid}" --init-groups -- "$@"
    fi
fi

exec "$@"
