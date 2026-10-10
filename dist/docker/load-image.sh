#!/bin/sh
# Join dist/docker split parts and docker load newlife-iot-platform:1.0.3
set -eu
cd "$(dirname "$0")"

out="newlife-iot-platform-1.0.3.tar.gz"
cat "${out}.part0"? > "${out}"
if command -v sha256sum >/dev/null 2>&1; then
    sha256sum -c "${out}.sha256"
elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 -c "${out}.sha256"
fi

docker load -i "${out}"
echo "Loaded newlife-iot-platform:1.0.3 (and :latest)."
echo "Run: docker compose up"
echo "  or: docker run --rm -p 5288:5288 -p 1883:1883 -v iot-data:/app/Data -v iot-logs:/app/Log newlife-iot-platform:1.0.3"
