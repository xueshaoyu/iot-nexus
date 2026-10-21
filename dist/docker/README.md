# Prebuilt Docker image

Current image: `newlife-iot-platform:1.0.4` (`linux/amd64`, also tagged `:latest`). Non-root uid 1654; data in `/app/Data`, logs in `/app/Log`. SQLite uses WAL / `synchronous=NORMAL` / 30s busy timeout. MQTT UI settings persist at `/app/Data/Config/mqtt-ui.json` on the `iot-data` volume.

The **1.0.4** archive is not committed (git rejects 100 MB+ files). Assemble the 8 MB pieces `newlife-iot-platform-1.0.4.tar.gz.part-aa` … then:

```bash
cat newlife-iot-platform-1.0.4.tar.gz.part-* > newlife-iot-platform-1.0.4.tar.gz
sha256sum -c SHA256SUMS   # or: sha256sum -c newlife-iot-platform-1.0.4.tar.gz.sha256
docker load -i newlife-iot-platform-1.0.4.tar.gz
docker compose up
```

SHA-256 of the assembled **1.0.4** archive:

```
a96a57f7c482ba186685de107d863008ab7f4fdb4a2162ea68d0d548b0998f5a
```

In-repo **1.0.3** 40 MB parts remain for `./load-image.sh` (SHA-256 `39792b9a06d0b845c84cf1aa726a9ddc40f602bec3345715849d9dc0c7bacd90`).
