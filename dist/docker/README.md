# Prebuilt Docker image

`newlife-iot-platform:1.0.3` (`linux/amd64`) is stored here as 40 MB parts so Git will accept the push.

| File | Role |
|------|------|
| `newlife-iot-platform-1.0.3.tar.gz.part00` … | Split archive |
| `newlife-iot-platform-1.0.3.tar.gz.sha256` | Checksum of the assembled `.tar.gz` |
| `load-image.sh` / `load-image.cmd` | Join, verify, `docker load` |

```bash
./dist/docker/load-image.sh
docker compose up
```

SHA-256 of the assembled archive:

```
39792b9a06d0b845c84cf1aa726a9ddc40f602bec3345715849d9dc0c7bacd90
```
