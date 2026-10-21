# NewLife IoT Platform

A self-contained IoT device management platform built on the **NewLife** .NET ecosystem:

- **NewLife.XCode** handles the data layer (entities generated from `Model.xml`, tables created automatically).
- **NewLife.MQTT** provides the built-in broker.
- **NewLife.IoT** supplies the thing model (`ThingSpec`) and the protocol-driver framework (`IDriver`, `DriverFactory`).
- **NewLife.Modbus** provides the Modbus TCP driver and a Modbus slave.
- **ASP.NET Core** hosts the REST API (Swagger) and a **Blazor Server** admin UI.

A single `dotnet run` starts the MQTT broker, the management API and UI, the rule engine and the driver host. It also seeds demo products and devices whose data streams live.

![stack](https://img.shields.io/badge/.NET-8.0-512BD4) ![NewLife](https://img.shields.io/badge/NewLife-XCode%20%7C%20MQTT%20%7C%20IoT%20%7C%20Modbus-2ea44f)

---

## Features

| # | Area | What you get |
|---|------|--------------|
| 1 | **Product definition (thing model)** | Properties, events and services aligned with **Aliyun TSL**: data types `int/float/double/text/date/bool/enum/struct/array`, specs (min/max/step/unit/unitName/length/enum map/array item), access mode `r/w/rw`, and optional function blocks. Import and export Aliyun TSL JSON. The NewLife.IoT `ThingSpec` API remains for drivers. |
| 2 | **Device management** | Register a single device or a batch; bind to a product and a group; edit; enable/disable (disabling kicks live sessions); reset the secret; delete. The device detail page has a live shadow, history charts, events, a command log, a lifecycle timeline, and connection instructions. |
| 3 | **Protocol drivers** | Three access modes. **MQTT** uses the built-in broker and accepts both the original `iot/...` topics and Aliyun Alink `/sys/.../thing/...` topics. **HTTP** uses a REST access API with command polling. **Driver** means the platform polls the device through any NewLife.IoT `IDriver`. ModbusTcp, ModbusUdp and ModbusRtuOverTcp/Udp come from NewLife.Modbus, and a built-in `Simulator` driver is included. Payload codecs: `Json` and Aliyun `Alink`. |
| 4 | **Online status** | Online on MQTT CONNECT, first HTTP report, or first successful driver poll. Heartbeat from PINGREQ, any uplink, or HTTP heartbeat. Offline on disconnect, 3 consecutive driver read failures, or a per-product/global timeout sweep. Stale-session protection prevents a reconnect race. Tracks online sessions, login count, cumulative online time, and history. |
| 5 | **Device grouping** | A tree of groups with direct and rolled-up device and online counts. Move devices in bulk; delete re-parents children. Rules can be scoped to a group including its sub-groups. |
| 6 | **Rules / policies** | Triggers: Property, Event, Online, Offline. Scope by product, group or device. Conditions (`> >= < <= == != contains changed`) combined with All/Any. A silence period per device. Actions: `alarm`, `set-property`, `invoke-service` (device linkage), `webhook`, `mqtt-publish`, `log`. Templated parameters such as `{device.name}` and `{temperature}`, dry-run/execute test, execution logs, and alarm management with acknowledge/close. |
| 7 | **Device statistics** | Real-time overview: devices, online rate, active/new today, messages today, messages in the last minute, downlinks, and alarms. Per-product stats. Hourly and daily trend buckets in the `DeviceStat` table, flushed every minute. History retention cleanup. |
| 8 | **MQTT server** | Built-in NewLife.MQTT broker **and** named external brokers (Mosquitto/EMQX/HiveMQ) can run at the same time. Each MQTT device (or its product) picks a profile; unset falls back to the platform default (`Mode` / `ActiveBroker`). Operators can view and edit these settings on **系统 → MQTT 配置** (`/settings/mqtt`); environment variables still win. See [MQTT server](#mqtt-server-built-in-or-external). The built-in broker serves TCP and WebSocket (`/mqtt`), with device authentication (username = device code, password = secret). Downlink is published on the broker assigned to that device. |
| 9 | **Device control** | Per-device control panel (typed property writes, service calls, live command status) and **batch control by product, group (with sub-groups) or selected devices**, with preview, per-device results and persisted jobs. |
| 10 | **Multi-language UI** | Simplified Chinese (default) and English with a sidebar switcher. Uses standard ASP.NET Core localization (`IStringLocalizer`, `.resx`); see [Languages](#languages-i18n). |

---

## 物模型（阿里云 TSL）与设备接入

物模型按阿里云物联网平台的 TSL（Thing Specification Language）组织，仍落在原来的 `ThingProperty` / `ThingEvent` / `ThingService` 表上。XCode 启动时自动加列；已有数据的模块标识为空，表示默认模块。

### 三类功能

| 功能 | 说明 |
| --- | --- |
| 属性 Properties | `accessMode` 为 `r`（只读）、`w`（只写）或 `rw`（读写）。读写类型之外的下发会被拒绝。 |
| 服务 Services | `callType` 为 `async` 或 `sync`。同步服务未指定等待时间时，平台默认等待 8 秒。 |
| 事件 Events | `type` 为 `info`、`alert` 或 `error`，可带输出参数。标识符 `property` 保留给属性上报，不能当作自定义事件。 |

标识符规则与阿里云一致：字母或下划线开头，之后只能是字母、数字、下划线，最长 50 个字符。名称最长 30 个字符。

### 数据类型与 specs

标准类型：`int`、`float`、`double`、`text`、`date`、`bool`、`enum`、`struct`、`array`。

- 数值：`min` / `max` / `step` / `unit` / `unitName`。最小值和最大值都是 0 时不校验范围，用来兼容以前没填范围的属性。
- `text`：`length`，最大 10240。长度为 0 时按 10240 校验。
- `date`：UTC 毫秒时间戳（也接受秒和可解析的时间字符串）。
- `bool` / `enum`：规格是键值映射，界面沿用 `0=关,1=开`。
- `array`：`size`（1–512）和 `item.type`。
- `struct`：`specs` 为子属性数组。规则条件可以写 `geo.longitude` 这种子字段。

历史上的 `string` 会当成 `text`。`short` / `long` 等仍可保存，供驱动点位使用，导出 TSL 时映射为 `int`。

### 模块

默认模块的 `FunctionBlockId` 为空。自定义模块（functionBlock）的标识写在同一列，通信和规则里使用 `模块标识:功能标识`，例如 `batteryPack:health`。

阿里云控制台导出的默认模块 JSON，以及带 `functionBlockId` 的自定义模块 JSON，都可以直接粘贴导入。本平台另有一份「全部模块」JSON，把各模块功能嵌在 `functionBlocks` 里，方便在本平台往返；这份多出来的嵌套字段是平台扩展，阿里云导入时不一定接受。

```bash
curl localhost:5288/api/products/1/tsl                 # 默认模块，阿里云兼容
curl localhost:5288/api/products/1/tsl?block=batteryPack
curl localhost:5288/api/products/1/tsl?full=true       # 含自定义模块，平台往返
curl -X POST localhost:5288/api/products/1/tsl \
     -H 'Content-Type: application/json' --data-binary @model.json
curl localhost:5288/api/products/1/thing-spec          # 仍可导出 NewLife.IoT ThingSpec，给驱动使用
```

导入按「模块 + 标识符」覆盖，不会清空驱动点位地址、原始类型和缩放系数。

### 设备接入

原有主题保持不变，已接入设备不用改固件：

```text
iot/{productKey}/{deviceName}/property/post
iot/{productKey}/{deviceName}/event/post
iot/{productKey}/{deviceName}/service/reply
iot/{productKey}/{deviceName}/heartbeat
iot/{productKey}/{deviceName}/property/set        # 下行
iot/{productKey}/{deviceName}/service/invoke      # 下行
```

同时接受阿里云 Alink JSON 的 Topic（前导斜杠可有可无，下发使用带斜杠的形式）。自定义模块把功能标识写成 `模块:标识`：

```text
/sys/{productKey}/{deviceName}/thing/event/property/post
/sys/{productKey}/{deviceName}/thing/event/property/post_reply
/sys/{productKey}/{deviceName}/thing/service/property/set
/sys/{productKey}/{deviceName}/thing/service/property/set_reply
/sys/{productKey}/{deviceName}/thing/event/{identifier}/post
/sys/{productKey}/{deviceName}/thing/event/{identifier}/post_reply
/sys/{productKey}/{deviceName}/thing/service/{identifier}
/sys/{productKey}/{deviceName}/thing/service/{identifier}_reply
```

属性上报报文：

```json
{"id":"123","version":"1.0","sys":{"ack":1},"params":{"temperature":{"value":26.5,"time":1524448722000}},"method":"thing.event.property.post"}
```

`sys.ack` 为 0 时云端不回复。属性值会按物模型做类型和范围校验；Alink 主题上未定义的属性会拒绝。原来的 `iot/...` 主题仍接受物模型之外的字段，已定义的字段同样校验，避免把旧设备拦掉。

下行时，内置 MQTT 服务器看设备订的是哪套主题就发到哪套。外部服务器看不到订阅，产品 Codec 为 `Alink` 时走 `/sys/...`，否则仍走 `iot/...`。

演示产品「温湿度传感器」（`env-sensor`）带有一份覆盖上述类型的 TSL 示例，并包含自定义模块 `batteryPack`。模拟器仍只上报 `temperature` / `humidity` / `battery`，因此原有规则和在线演示不受影响。

### 规则

属性条件、事件名和 `set-property` 都使用物模型标识。默认模块直接写 `temperature`；自定义模块写 `batteryPack:health`；结构体子字段写 `geo.longitude`。事件触发可以引用该事件的输出参数。

### MQTT 配置（管理界面）

侧栏 **系统 → MQTT 配置**（`/settings/mqtt`）可查看和修改内置 Broker（启用、端口、匿名、管理账号/密码）以及外部 MQTT 服务器（增删改、启用/禁用、测试连接），并显示内置客户端数和每台外部服务器的连接状态。密码只写不读，页面上显示为 `••••`。整个管理界面按管理员使用设计，没有单独的登录页；生产环境请把 UI 放在 SSO / 反代认证之后，并为 `/api` 设置 `IoT:ApiKey`。

优先级（页面上有说明）：

1. 环境变量 `IoT__Mqtt__*`（或 `IoT:Mqtt:*`）——锁定对应字段，界面只读。设置了 `IoT__Mqtt__Brokers__*` 或 `IoT__Mqtt__External__*` 时，整个外部服务器列表只读。
2. 界面保存的 `{DataPath}/Config/mqtt-ui.json`（Docker 中即 `/app/Data/Config/mqtt-ui.json`）。
3. `appsettings.json` 默认值。

保存空密码表示保持原密码。修改启用状态、端口、模式或管理端口会立即热重启内置 Broker（已连接设备会短暂断开）；管理账号/密码和外部服务器变更立即生效，外部连接按配置指纹重连。

### 访问记录

管理端页面和 `/api` 请求会记入「访问记录」（侧栏路径 `/access-logs`，查询接口 `GET /api/access-logs` 与 `GET /api/access-logs/summary`）。静态资源、`/_framework`、`/_blazor`、`/_content` 以及配置的扩展名默认不记录。设备 MQTT 连接不在这里。

真实客户端 IP 只在直连方是本机回环，或落在 `IoT:AccessLog:TrustedProxies`（支持 IP 和 CIDR）时，才采信转发头，优先级为 `CF-Connecting-IP`、`X-Real-IP`、`X-Forwarded-For`（从右向左跳过受信任地址）。国家或地区使用 `CF-IPCountry`（`XX` 视为未知），不附带离线 IP 库。User-Agent 会解析出浏览器、操作系统和是否爬虫。

写入走 XCode 异步队列，和设备数据一样批量落库，不挡住请求。`IoT:AccessLog:RetentionDays` 默认 30 天，后台每小时清理更早的记录；设为 0 则不清理。已有 SQLite 库在启动时由 XCode 自动建 `AccessLog` 表，原有数据保留。

```json
"AccessLog": {
  "Enable": true,
  "RetentionDays": 30,
  "TrustedProxies": [ "127.0.0.1", "::1" ],
  "ExcludePrefixes": [ "/_framework", "/_blazor", "/_content", "/css", "/js", "/lib", "/favicon.ico" ]
}
```

---

## Quick start

Requirements: **.NET 8 SDK**. No database server is needed; SQLite is used by default. To run without an SDK, see [Run with Docker](#run-with-docker).

```bash
git clone <this repo> && cd <repo>
dotnet build
dotnet run --project src/IoTPlatform.Web
```

| Endpoint | URL |
|----------|-----|
| Admin UI | http://localhost:5288 |
| REST API (Swagger) | http://localhost:5288/swagger |
| MQTT broker | `tcp://localhost:1883` and `ws://localhost:1883/mqtt` |
| Demo Modbus TCP slave | `127.0.0.1:1502` (started by the demo simulator) |

On first start the platform creates the SQLite database. XCode resolves relative paths against the app base directory, so the file is at `src/IoTPlatform.Web/bin/Debug/net8.0/Data/iot.db`. It then seeds demo data and starts an **in-process simulator**, so within a few seconds you will see:

| Product | Protocol | Devices | Data source |
|---------|----------|---------|-------------|
| 温湿度传感器 / Environment Sensor (`env-sensor`) | MQTT / Json | th-001 … th-005 | MQTT simulator (th-005 intentionally **offline**) |
| 智能灯 / Smart Light (`smart-light`) | MQTT / Json | light-001 … light-003 | MQTT simulator, supports `switch`/`brightness` writes |
| 电力仪表 / Power Meter (`power-meter`) | Driver / **ModbusTcp** | meter-001 | Polled from the demo Modbus slave (`4x0`–`4x6`) |
| 工业锅炉 / Industrial Boiler (`boiler`) | Driver / **Simulator** | boiler-001, boiler-002 | NewLife.IoT virtual driver |
| HTTP 网关 / HTTP Gateway (`http-gateway`) | HTTP / Json | http-gw-001 | HTTP simulator (REST + command polling) |

Demo device secrets follow `<code>-secret`, for example `th-001-secret`. Five demo rules are included: high temperature, overheat event, boiler over-temperature with an automatic burner shut-off, device offline notice, and meter high load.

To start with an empty platform, set `IoT__Demo__SeedData=false` and `IoT__Demo__EnableSimulator=false`, or edit `appsettings.json`.

---

## Run with Docker

Operators do not need a local .NET SDK for day-to-day runs. The image is a multi-stage build of `IoTPlatform.Web` on the official ASP.NET 8 runtime. HTTP already listens on `0.0.0.0:5288`; the Embedded MQTT broker already binds `0.0.0.0:1883`.

### Build and start (Embedded + zh-CN demo)

```bash
docker compose up --build
```

| Endpoint | URL |
|----------|-----|
| Admin UI | http://localhost:5288 |
| Health | http://localhost:5288/health → `{"status":"ok",…}` |
| REST API (Swagger) | http://localhost:5288/swagger |
| MQTT (Embedded) | `tcp://localhost:1883` and `ws://localhost:1883/mqtt` |

SQLite lives in the `iot-data` **named volume** at `/app/Data/iot.db`. NewLife logs go to `iot-logs` (`/app/Log`). First start seeds the same demo products and in-process simulator as `dotnet run`.

**Do not bind-mount a host folder over `/app/Data` on Docker Desktop for Windows or macOS.** Those bind mounts go through a VM file-sharing layer (osxfs / virtiofs / WSL2 9p) that is slow for SQLite and routinely produces `SQLite Error 5: database is locked` plus 慢SQL of several seconds. Use the Compose named volume (`iot-data`) instead. Bind-mounting a Linux host directory is fine.

Image `1.0.3` turns on WAL (`journal_mode=WAL`), `synchronous=NORMAL`, and a 30s busy timeout on the Microsoft.Data.Sqlite connection (`Cache=Shared;Default Timeout=30`), and batches the hot device/property/group/history writes through XCode `SaveAsync`.

```bash
curl -fsS http://localhost:5288/health
```

Image-only (no Compose):

```bash
docker build -t newlife-iot-platform .
docker run --rm -p 5288:5288 -p 1883:1883 \
  -v iot-data:/app/Data -v iot-logs:/app/Log \
  newlife-iot-platform
```

### Load a prebuilt image (no SDK, no rebuild)

The `linux/amd64` image is in the repo under [`dist/docker/`](dist/docker/) as 40 MB split parts (Git rejects a single file over 100 MB). After `git pull`:

```bash
# Linux / macOS / WSL
./dist/docker/load-image.sh

# Windows cmd
dist\docker\load-image.cmd
```

That joins the parts, checks SHA-256 `39792b9a06d0b845c84cf1aa726a9ddc40f602bec3345715849d9dc0c7bacd90`, and runs `docker load`. Then:

```bash
docker run --rm -p 5288:5288 -p 1883:1883 \
  -v iot-data:/app/Data -v iot-logs:/app/Log \
  newlife-iot-platform:1.0.3
```

Or skip `--build` — Compose already uses `image: newlife-iot-platform:latest`:

```bash
docker compose up
```

### 1.0.1 workaround (`Access to the path '/Data' is denied`)

`1.0.1` drops to uid 1654 and only chowns `/app/Data` and `/app/Log`. NewLife sees assembly name `IoTPlatform.Web` and sets `DataPath=../Data` → `/Data` (plus `/appLog`, `/Backup`, `/Plugins`, `/app/Config`). Those directories are not writable, so XCode `DataCache.Save` throws.

`user: "0"` alone is not enough: the 1.0.1 entrypoint still drops to uid 1654. Skip the entrypoint and run as root:

```bash
docker compose run --rm --service-ports --user 0 --entrypoint dotnet iot IoTPlatform.Web.dll
```

or in `docker-compose.yml`:

```yaml
services:
  iot:
    user: "0"
    entrypoint: ["dotnet", "IoTPlatform.Web.dll"]
```

`1.0.2` pins NewLife/XCode paths under `/app/Data` and `/app/Log` and keeps the non-root user.

### Embedded vs External

Default Compose is `IoT__Mqtt__Mode=Embedded`. To bring up the optional Mosquitto service and use it as the platform default:

```bash
docker compose --profile external-mqtt \
  -f docker-compose.yml -f docker-compose.external-mqtt.yml up --build
```

Mosquitto is published on the host as **1884**. Inside the Compose network the platform uses `tcp://mosquitto:1883`. The overlay is demo-only (`allow_anonymous true`); do not use `deploy/mosquitto/mosquitto.conf` in production.

To point at a broker you already run on the host:

```bash
IoT__Mqtt__Mode=External \
IoT__Mqtt__ActiveBroker=local \
IoT__Mqtt__Brokers__0__Id=local \
IoT__Mqtt__Brokers__0__Server=tcp://host.docker.internal:1884 \
docker compose up --build
```

Device-level assignment is unchanged: pick `embedded` or a `Brokers[]` profile on the product or device.

### Production hardening

Do not bake real secrets into the image. Override them at run time and turn the demo off:

```bash
IoT__Demo__SeedData=false \
IoT__Demo__EnableSimulator=false \
IoT__ApiKey=your-api-key \
IoT__Mqtt__AdminPassword=your-mqtt-admin-password \
docker compose up --build -d
```

The Compose file still uses the existing demo MQTT admin password (`admin` / `admin123` from `appsettings.json`) unless you set `IoT__Mqtt__AdminPassword`. Change it, set `IoT__ApiKey`, and put the Blazor UI behind your own SSO or reverse-proxy auth. See [Production notes](#production-notes).

---

## Solution layout

```
IoTPlatform.sln
├─ Dockerfile / docker-compose.yml / .dockerignore    # container image + Compose (Embedded by default)
├─ docker-compose.external-mqtt.yml / deploy/mosquitto/  # optional Mosquitto profile
├─ Directory.Build.props / Directory.Packages.props   # net8.0, central package versions
├─ src/
│  ├─ IoTPlatform.Data/          # Persistence + domain entities (NewLife.XCode)
│  │  ├─ Model.xml               # ← single source of truth for the schema
│  │  ├─ Entity/*.cs             # generated by `xcode Model.xml` (do not edit)
│  │  ├─ Entity/*.Biz.cs         # business partials (validation, helpers) – hand-edited
│  │  └─ Enums.cs, IoTConstants.cs
│  ├─ IoTPlatform.Core/          # Application services
│  │  ├─ Services/               # Product, Device, Group, Online, DataIngest, DeviceCommand, Stat, Alarm
│  │  ├─ Rules/                  # RuleEngine, RuleEvaluator, RuleTemplate, RuleService, IRuleAction
│  │  │  └─ Actions/             # alarm, webhook, set-property, invoke-service, log
│  │  └─ IoTOptions.cs           # "IoT" configuration section
│  ├─ IoTPlatform.Protocols/     # Protocol / driver layer
│  │  ├─ Codecs/                 # IPayloadCodec, Json, Alink
│  │  ├─ Drivers/                # DriverHostService (polling + downlink), SimulatorDriver, PlatformDevice (IDevice)
│  │  └─ Topics.cs               # MQTT topic conventions
│  ├─ IoTPlatform.Mqtt/          # MQTT ingress (NewLife.MQTT)
│  │  ├─ MqttServerHost.cs       # broker hosting, MQTT downlink channel, session kicker
│  │  ├─ IoTMqttHandler.cs       # per-connection handler → online/offline + ingest
│  │  ├─ DeviceMqttAuthenticator.cs  # device auth + topic ACL
│  │  └─ MqttPublishAction.cs    # rule action
│  ├─ IoTPlatform.Simulator/     # Reusable simulators (MQTT devices, HTTP devices, Modbus slave)
│  └─ IoTPlatform.Web/           # Host: REST controllers, Blazor UI, background workers, seeding
│     ├─ Controllers/            # /api/products, devices, groups, rules, alarms, stats, protocols, device-access
│     ├─ Components/             # Blazor Server admin UI (Pages + Shared components, no JS deps)
│     └─ Infrastructure/         # workers, demo seeder, JSON contract, API key middleware
├─ tools/IoTPlatform.DeviceSimulator/   # Stand-alone simulator CLI
└─ tests/IoTPlatform.Tests/             # Unit + end-to-end integration tests (MQTT, Simulator, Modbus)
```

### Data flow

```
 MQTT device ──► NewLife.MQTT broker ──► IoTMqttHandler ─┐
 HTTP device ──► /api/device-access  ──► codec ──────────┼─► DataIngestService ─► shadow (DeviceProperty)
 Modbus/…    ◄── DriverHostService (NewLife.IoT IDriver) ┘        │              ─► history (DeviceData, async batch)
                                                                  │              ─► DeviceOnlineService (heartbeat)
                                                                  │              ─► MessageCounter ─► StatWorker ─► DeviceStat
                                                                  └────────────► RuleEngine (queue) ─► IRuleAction[]
 Admin UI / REST ─► DeviceCommandService ─► IDeviceCommandChannel: MQTT | Driver | HTTP polling ─► device
                                             ▲ replies (service/reply, HTTP reply) ─ CommandTracker
```

### Data model

Tables are created and migrated automatically by XCode. The data dictionary is `src/IoTPlatform.Data/IoT.htm`.

`Product`, `ThingProperty`, `ThingEvent`, `ThingService`, `DeviceGroup`, `Device`, `DeviceOnline`, `DeviceHistory`, `DeviceProperty` (shadow), `DeviceData` (time series, snowflake ids), `DeviceEvent`, `ServiceRecord`, `Rule`, `RuleLog`, `Alarm`, `DeviceStat`.

To change the schema, edit `Model.xml`, then regenerate:

```bash
dotnet tool install -g xcodetool
cd src/IoTPlatform.Data && xcode "$PWD/Model.xml"   # regenerates Entity/*.cs, keeps *.Biz.cs
```

---

## Configuration (`src/IoTPlatform.Web/appsettings.json`)

```jsonc
{
  "Urls": "http://0.0.0.0:5288",
  "ConnectionStrings": { "IoT": "Data Source=Data/iot.db;Cache=Shared;Default Timeout=30", "IoTProvider": "SQLite" },
  "XCode": { "ShowSQL": false },
  "IoT": {
    "OfflineTimeout": 90,        // seconds without heartbeat → offline (products can override)
    "DataRetentionDays": 7,      // DeviceData / events / history / rule logs cleanup
    "DefaultPollInterval": 5000, // driver polling default (ms)
    "ApiKey": "",                // when set, /api/* requires header X-Api-Key (device-access excluded)
    "Mqtt": {
      "Enable": true,
      "Mode": "Embedded",          // Embedded = built-in NewLife.MQTT broker, External = connect to Mosquitto/EMQX/HiveMQ…
      "TopicRoot": "iot",          // device topics are {TopicRoot}/{productKey}/{deviceCode}/…, may be multi-level
      "ActiveBroker": "local",     // platform default when Mode=External and the device/product leave the broker unset
      "Brokers": [                 // named external broker profiles
        { "Id": "local", "Name": "Local Mosquitto", "Server": "tcp://127.0.0.1:1884", "ClientId": "newlife-iot-platform" },
        { "Id": "emqx", "Name": "EMQX (TLS)", "Server": "tcp://emqx.example.com:8883", "UseTls": true, "UserName": "platform", "Password": "" }
      ],
      "Port": 1883, "AdminUser": "admin", "AdminPassword": "admin123", "AllowAnonymous": false   // built-in broker only
    },
    "Demo": { "SeedData": true, "Language": "zh-CN", "EnableSimulator": true, "ModbusPort": 1502, "ReportInterval": 3000 }
  }
}
```

Every key can be overridden with environment variables, for example `IoT__Mqtt__Port=18830`.

**Other databases.** XCode supports SQL Server, MySQL, PostgreSQL and others. Set `ConnectionStrings:IoTProvider` (e.g. `SqlServer`, `MySql`), change the connection string, and add the matching ADO.NET package to `IoTPlatform.Data` (e.g. `Microsoft.Data.SqlClient`, `MySqlConnector`).

---

## Try each feature

The examples use `curl` against `http://localhost:5288`. Every call is also available in Swagger. IDs match the demo seed.

### 1. Product & thing model
- UI: **产品 Products → 电力仪表 (power-meter)**. Tabs show properties (with Modbus point addresses and scaling), events and services. **ThingSpec JSON** exports and imports the NewLife.IoT format.
- API:
  ```bash
  curl localhost:5288/api/products
  curl localhost:5288/api/products/1/thing-model
  curl localhost:5288/api/products/3/thing-spec            # NewLife.IoT ThingSpec
  curl -X POST localhost:5288/api/products -H 'Content-Type: application/json' \
       -d '{"name":"Door Sensor","code":"door","protocol":"MQTT","codec":"Json"}'
  curl -X POST localhost:5288/api/products/6/properties -H 'Content-Type: application/json' \
       -d '{"identifier":"open","name":"Open","dataType":"bool"}'
  ```

### 2. Devices
- UI: **Devices** has filters, register, batch register, enable/disable, and move to group. Open **大堂灯 (light-001)** and use **设置 / Set** on the *开关 / Switch* tile, or **服务与指令 / Services & commands → Toggle**.
- API:
  ```bash
  curl -X POST localhost:5288/api/devices -H 'Content-Type: application/json' -d '{"productId":6,"code":"door-01"}'
  curl -X POST localhost:5288/api/devices/batch -H 'Content-Type: application/json' -d '{"productId":6,"prefix":"door","count":5}'
  curl localhost:5288/api/devices/6/properties                                  # shadow
  curl -X POST localhost:5288/api/devices/6/properties/set -H 'Content-Type: application/json' \
       -d '{"values":{"brightness":30},"waitSeconds":5}'                         # MQTT downlink + reply
  curl -X POST localhost:5288/api/devices/6/services/invoke -H 'Content-Type: application/json' \
       -d '{"service":"toggle","waitSeconds":5}'
  curl -X POST localhost:5288/api/devices/1/disable                               # kicks the MQTT session
  ```

### 3. Protocol drivers
- **Driver (Modbus TCP).** `meter-001` is polled through `ModbusTcpDriver` from NewLife.Modbus. Writing the rw property `relay` goes back to holding register 6, and the current drops to 0 on the next poll:
  ```bash
  curl -X POST localhost:5288/api/devices/9/properties/set -H 'Content-Type: application/json' -d '{"values":{"relay":0}}'
  ```
- **Driver (Simulator).** The boilers use the built-in `SimulatorDriver` (`DriverBase<Node, SimulatorParameter>`).
- **HTTP.**
  ```bash
  curl -X POST localhost:5288/api/device-access/http-gw-001/properties \
       -H 'X-Device-Secret: http-gw-001-secret' -H 'Content-Type: application/json' -d '{"temperature":21.5}'
  curl localhost:5288/api/device-access/http-gw-001/commands -H 'X-Device-Secret: http-gw-001-secret'
  ```
- UI: **Protocols & MQTT → Drivers** lists the registered drivers with their default parameter templates and polling worker health.

### 4. Online status
- Start the stand-alone simulator for the spare sensor. `th-005` turns **online** within a second; press Ctrl+C and it goes **offline** (MQTT disconnect):
  ```bash
  dotnet run --project tools/IoTPlatform.DeviceSimulator
  ```
- Invoke `reboot` on `th-001`. The simulated device disconnects and reconnects, and the timeline shows Offline → Online:
  ```bash
  curl -X POST localhost:5288/api/devices/1/services/invoke -H 'Content-Type: application/json' -d '{"service":"reboot","waitSeconds":5}'
  curl localhost:5288/api/devices/1/history
  ```
- Timeout path: an HTTP device that stops reporting goes offline after its product's `OnlineTimeout` (60 s for the demo gateway).

### 5. Groups
```bash
curl localhost:5288/api/groups                                  # tree with rolled-up counts
curl -X POST localhost:5288/api/groups -H 'Content-Type: application/json' -d '{"name":"Floor 3","parentId":2}'
curl -X POST localhost:5288/api/groups/assign -H 'Content-Type: application/json' -d '{"deviceIds":[1,2],"groupId":9}'
curl "localhost:5288/api/devices?groupId=2&includeSubGroups=true"
```

### 6. Rules
- UI: **规则 Rules → 高温告警 (High temperature alarm)**. Use **测试规则 / Test rule** for a dry run or to execute the actions. **Alarms** lets you acknowledge or close.
- The simulator occasionally spikes temperatures above 35 °C, which fires the threshold and `overheat` rules. When the boiler water exceeds 93 °C, `set-property` switches its burner off automatically.
- API:
  ```bash
  curl localhost:5288/api/rules/metadata                          # triggers, operators, action parameter schemas
  curl -X POST localhost:5288/api/rules -H 'Content-Type: application/json' -d '{
    "name":"Light on when dark","triggerType":"Property","productId":1,
    "conditions":[{"property":"humidity","operator":">","value":"70"}],
    "actions":[{"type":"set-property","parameters":{"device":"light-001","property":"switch","value":"true"}}],
    "silenceSeconds":60}'
  curl -X POST localhost:5288/api/rules/1/test -H 'Content-Type: application/json' \
       -d '{"deviceId":1,"values":{"temperature":40},"execute":false}'
  curl "localhost:5288/api/alarms?status=Active"
  ```
- Watch `mqtt-publish` output with the admin account: `mosquitto_sub -h localhost -u admin -P admin123 -t 'alerts/#' -v`.

### 7. Statistics
```bash
curl localhost:5288/api/stats/overview
curl localhost:5288/api/stats/products
curl "localhost:5288/api/stats/trend?level=Hour&count=24"
```
UI: **Dashboard** refreshes every 3 s, and **Statistics** offers per-product filters and 24 h / 72 h / 7 d / 30 d ranges.

### 8. MQTT server
Use any MQTT client, for example mosquitto:
```bash
# publish as a device
mosquitto_pub -h localhost -p 1883 -i th-005 -u th-005 -P th-005-secret \
  -t iot/env-sensor/th-005/property/post -m '{"temperature":36.4,"humidity":50}'
# observe everything as admin
mosquitto_sub -h localhost -p 1883 -u admin -P admin123 -t 'iot/#' -v
```

| Direction | Topic | Payload (Json codec) |
|-----------|-------|----------------------|
| up | `iot/{productKey}/{deviceCode}/property/post` | `{"temperature":25.1}` · `{"ts":…,"params":{…}}` · `{"items":[{"name","value","time"}]}` |
| up | `iot/{pk}/{dc}/event/post` | `{"type":"alert","name":"overheat","remark":"…","data":{…}}` |
| up | `iot/{pk}/{dc}/service/reply` | `{"id":123,"status":2,"data":"ok"}` (NewLife `ServiceStatus`: 2 = done, 4 = error) |
| up | `iot/{pk}/{dc}/heartbeat` | anything (PINGREQ also counts in Embedded mode) |
| up | `iot/{pk}/{dc}/status` | `online` / `offline`: retained, use `offline` as last will (required for online tracking in External mode) |
| down | `iot/{pk}/{dc}/service/invoke` | `{"id":123,"name":"reboot","params":{…}}` |
| down | `iot/{pk}/{dc}/property/set` | `{"id":124,"params":{"switch":true}}` |

Status: `curl localhost:5288/api/protocols/mqtt`, or **Protocols & MQTT** in the UI. Edit broker settings on **系统 → MQTT 配置** (`/settings/mqtt`).

---

### 9. Device control (single device and batch)

**Single device.** Open a device → **设备控制 / Control** (also the primary button in the page header):
- **Writable properties** (`rw` in the thing model) get typed controls. Booleans use an on/off switch that sends immediately, enum mappings use a select, and numbers get an input bounded by min/max/step.
- **Services** are rendered from the thing model with typed parameter inputs. Arguments are converted to the declared parameter types (e.g. `"5"` becomes `5` for `int`).
- Every control shows its **latest command status** inline: pending (queued), sent (waiting for the device reply), success, failed or timeout. A live **command log** refreshes every 2 s, and replies that arrive later update the status automatically.

The API is unchanged: `POST /api/devices/{id}/properties/set` and `POST /api/devices/{id}/services/invoke` (`waitSeconds` 0–60).

**Batch control by product, group or selection.** In the UI:
- **Product page:** "批量控制 / Batch control" targets all devices of the product.
- **Group page:** the same button on the selected group, with an "include sub-groups" toggle.
- **Device list:** tick devices, then "批量控制" in the toolbar. Recent batch jobs and their per-device results are listed at the bottom of the device list.

The dialog previews which devices will run or be skipped (and why), shows progress while sending, and ends with a summary: succeeded, awaiting reply, failed, skipped, plus a per-device table.

```bash
# preview (no commands sent)
curl -X POST localhost:5288/api/devices/batch-control/preview -H 'Content-Type: application/json' \
     -d '{"scope":{"type":"Product","productId":2},"kind":"property","property":"brightness","value":30}'
# all lights in "Building A" and its sub-groups: switch off
curl -X POST localhost:5288/api/devices/batch-control -H 'Content-Type: application/json' \
     -d '{"scope":{"type":"Group","groupId":2,"includeSubGroups":true},"kind":"property","property":"switch","value":false,"waitSeconds":5}'
# selected devices: invoke a service
curl -X POST localhost:5288/api/devices/batch-control -H 'Content-Type: application/json' \
     -d '{"scope":{"type":"Devices","deviceIds":[6,7]},"kind":"service","service":"toggle","args":{}}'
curl localhost:5288/api/devices/batch-control            # recent jobs
curl localhost:5288/api/devices/batch-control/{jobId}    # job detail, pending items refreshed from their commands
```

Batch rules:
- The scope is expanded to concrete devices. Each device then receives its own command through the same channel as single-device control (MQTT, driver write/control, or HTTP polling queue), 8 in parallel. Each command is recorded as a `ServiceRecord` with `source = Batch:{jobId}`, and the job itself is stored in the `BatchJob` table.
- **Always skipped:** disabled devices or products, devices whose product has no such property or service, and read-only properties.
- **Offline handling** (`scope.onlineMode`):
  - `Auto` (default) skips offline MQTT and Driver devices, which need a live session or a running poller, and still queues commands for HTTP devices, which pick them up on their next poll.
  - `OnlineOnly` skips every offline device.
  - `All` attempts every device; offline MQTT and Driver devices are then reported as failed.
- `waitSeconds` (0–30) is the per-device reply wait. Devices that haven't replied are reported as *Sent*, and their status keeps updating in the device command log and in the job detail.

## MQTT server: built-in or external

Each MQTT **device** can use a different broker. The product may set a default; if both are unset the platform default is used (`Mode=Embedded` → built-in `embedded`; `Mode=External` → `ActiveBroker`, or the first `Brokers[]` entry).

`IoT:Mqtt:Brokers` is a **catalog** of named external servers. `embedded` is always selectable. The platform may keep several connections at once:

- Start the **built-in** broker when `Mode=Embedded` or any enabled MQTT device/product resolves to `embedded`.
- Open a **client** to each external profile that is the platform default or is assigned to an enabled MQTT device (or is the default of an enabled MQTT product).

| | Embedded (`embedded`) | External profile |
|---|---|---|
| Broker | NewLife.MQTT in-process on `IoT:Mqtt:Port` (TCP + WebSocket `/mqtt`) | Any MQTT 3.1.1/5 broker: Mosquitto, EMQX, HiveMQ, … |
| Platform role | Broker | Client that subscribes to `{root}/+/+/property/post, event/post, service/reply, heartbeat, status` |
| Device authentication and ACL | Built in: username = device code, password = device secret; devices may only use their own topics | **Configured on that external broker** (see the example below) |
| Online / offline | MQTT connect/disconnect, plus heartbeat timeout | `{root}/{pk}/{dc}/status` = `online` / `offline` (retained + last will), plus heartbeat timeout |
| Downlink (property set, service invoke, batch, rule `mqtt-publish`) | Published on the **device's** assigned broker | Same: published only on that device's broker |
| Kick device on disable | Yes, if the device uses Embedded | No (not possible on a foreign broker) |

Assignment is `device.BrokerProfileId` → `product.BrokerProfileId` → platform default. Empty means inherit. The REST API and the device/product edit forms expose the field as a dropdown of `embedded` plus every `Brokers[]` profile.

Uplink is accepted only on the broker the device is assigned to (the device must exist, be enabled, and belong to an enabled MQTT product). Downlink, batch control and rule `mqtt-publish` / `set-property` / `invoke-service` publish on that same broker.

`ActiveBroker` is only the **platform default** when `Mode=External` and the device/product leave the broker unset. An unknown `ActiveBroker` does not fall back to another profile.

The admin page **系统 → MQTT 配置** (`/settings/mqtt`) edits the same settings and persists them to `{DataPath}/Config/mqtt-ui.json`. Precedence is **environment variables `IoT__Mqtt__*` > UI file > `appsettings.json`**. A field set in the environment is shown as read-only. Empty password inputs keep the previously stored secret; passwords are never returned by `GET /api/settings/mqtt` or `GET /api/protocols/mqtt/config`.

Changing Enable, Port, Mode or ManagementPort **restarts the built-in broker immediately** (connected devices drop briefly). Admin user/password and external-server add/edit/enable/disable apply immediately; the platform client reconnects when the profile fingerprint changes. Assigning a device or product to a profile that is not yet connected is picked up within about 15 seconds.

Each profile supports these settings:

| Key | Meaning |
|---|---|
| `Id`, `Name` | Selection key and display name. A missing `Id` becomes `broker-1`, `broker-2`, … |
| `Server` | e.g. `tcp://host:1883`. Use `tcp://host:8883` with `UseTls: true` for TLS. |
| `ClientId`, `UserName`, `Password` | The platform's own MQTT credentials on that broker. |
| `UseTls`, `QoS` (0/1), `KeepAlive`, `ReconnectDelay` | Connection behaviour. The client reconnects automatically. |
| `PublicServer` | Broker address shown to devices on the device connection page, if different from `Server`. |
| `Enable` | When `false`, the profile is not the platform default and the platform does not open a client to it. Device assignments are kept. |
| `TopicRoot` | Optional per-broker override of the global `IoT:Mqtt:TopicRoot`, e.g. `tenant-a/iot` on a shared broker. |

```bash
# select a profile from appsettings.json
IoT__Mqtt__Mode=External IoT__Mqtt__ActiveBroker=emqx dotnet run --project src/IoTPlatform.Web

# define / override profiles entirely through environment variables (list items are addressed by index)
IoT__Mqtt__Mode=External IoT__Mqtt__ActiveBroker=backup \
IoT__Mqtt__Brokers__0__Id=primary IoT__Mqtt__Brokers__0__Server=tcp://10.0.0.5:1883 \
IoT__Mqtt__Brokers__1__Id=backup  IoT__Mqtt__Brokers__1__Server=tcp://10.0.0.6:1883 \
IoT__Mqtt__Brokers__1__UserName=platform IoT__Mqtt__Brokers__1__Password=secret \
dotnet run --project src/IoTPlatform.Web
```

Environment keys such as `Brokers__1__Server` *merge into* the profile at the same index in `appsettings.json` (unspecified fields keep their file values), which is the standard .NET configuration behaviour for lists. Keep secrets such as `Password` in environment variables or a secret store rather than in the file.

**Backward compatibility:** the older single `IoT:Mqtt:External { … }` block still works. When `Brokers` is empty it is used as the only profile, with `Id` `default`, and when `Brokers` has entries it is ignored.

The Protocols & MQTT page lists **Embedded** and every external profile with connection state, last error and how many enabled MQTT devices resolve there. Edit the catalog on **MQTT 配置**. Passwords are never shown (only a "••••" marker). `GET /api/protocols/mqtt/config` and `GET /api/settings/mqtt` return `defaultBroker`, per-profile `running` / `deviceCount` / `hasPassword` (never the password). `PUT /api/settings/mqtt` saves the built-in broker; `PUT/DELETE /api/settings/mqtt/brokers` maintains external servers; `POST /api/settings/mqtt/test` probes a connection. Device and product CRUD include `brokerProfileId`.

**Mosquitto example** (`mosquitto.conf`): devices log in as their device code, and the platform has its own account.

```conf
listener 1884
allow_anonymous false
password_file /etc/mosquitto/passwd     # mosquitto_passwd -c /etc/mosquitto/passwd platform ; add each device code
acl_file /etc/mosquitto/acl
```

```conf
# /etc/mosquitto/acl
user platform
topic readwrite iot/#

# every device: publish its own uplinks, read its own downlinks (%u = username = device code)
pattern write iot/+/%u/property/post
pattern write iot/+/%u/event/post
pattern write iot/+/%u/service/reply
pattern write iot/+/%u/heartbeat
pattern write iot/+/%u/status
pattern read  iot/+/%u/service/invoke
pattern read  iot/+/%u/property/set
```

For EMQX or HiveMQ, create an equivalent authentication source and ACL. The platform needs subscribe permission on `{root}/+/+/…` and publish permission on `{root}/+/+/service/invoke` and `{root}/+/+/property/set`.

**Demo simulator.** Each simulated MQTT device connects to **its assigned broker**: `127.0.0.1:{Port}` for Embedded, or that profile's `Server` (the broker must accept `<code>` / `<code>-secret`, or allow anonymous clients). The default seed keeps every demo device on Embedded (the smart-light product sets `BrokerProfileId=embedded`; env-sensor inherits the platform default) so `dotnet run` does not need Mosquitto. Simulated devices publish a retained `status=online` and set a retained `offline` last will. The standalone tool accepts `--mqtt tcp://broker:port --topic-root iot --tls`.

## Languages (i18n)

The admin UI supports **Simplified Chinese (`zh-CN`, default)** and **English (`en`)**. Switch languages with the **简体中文 / English** buttons at the bottom of the sidebar (**中 / EN** in the mobile top bar). The choice is stored in the standard `.AspNetCore.Culture` cookie for a year. Visitors without the cookie get Chinese; the browser `Accept-Language` header is deliberately ignored.

**How it works.** It follows the standard ASP.NET Core and Blazor Server localization pattern:

- **Culture selection.** `AddLocalization` plus `UseRequestLocalization` resolve the culture from the `?culture=` query string or the cookie, falling back to `zh-CN`.
- **Switching.** `GET /culture/set?culture=en&redirectUri=/devices` writes the cookie, and the UI reloads the page so the Blazor circuit restarts in the new culture.
- **UI strings.** Components use the injected `IStringLocalizer<SharedResource> L`, e.g. `@L["Devices"]` or `@L["{0} online", n]`. **The key is the English source text**, so English needs no resource file and any missing translation falls back to English.
- **Translation files.** Translations live in `src/IoTPlatform.Web/Resources/SharedResource.<culture>.resx`. `SharedResource.en.resx` only overrides texts whose source isn't English (the NewLife.Modbus driver names).
- **Server errors.** `PlatformException` carries an English template plus arguments (`"Device code '{0}' already exists"`), and the UI translates the template before formatting it into a toast. The REST API and Swagger stay English.
- **Form validation.** `<LocalizedValidator />` replaces `DataAnnotationsValidator` and builds messages from `{0} is required`-style keys plus `field.<Property>` display names.
- **Enum-like values.** Alarm levels, command status, protocols, event types and rule action names/parameters are shown through `Fmt.Text(...)`, `L[action.DisplayName]` and so on.
- **Demo data.** Demo product, device, group and rule names are seeded in Chinese; set `IoT:Demo:Language=en` for English names before the first start.

**Adding a language** (e.g. Japanese):

1. Append `("ja", "日本語")` to `UiCultures.Supported` in `src/IoTPlatform.Web/SharedResource.cs`.
2. Copy `Resources/SharedResource.zh-CN.resx` to `Resources/SharedResource.ja.resx` and translate the `<value>` elements, keeping the `name` keys unchanged.
3. Run `dotnet test`. `LocalizationTests` checks that every key used in the UI (and every service error template) has a `zh-CN` translation, and that no two keys differ only by case. `.resources` compilation silently drops case-insensitive duplicates. Point the coverage test at your new file too if you want the same guarantee for it.

When adding UI text, wrap it as `@L["Your English text"]`, add the Chinese entry to `SharedResource.zh-CN.resx`, and run `dotnet test`.

## Extension points

**New protocol driver.** Reference any NewLife.IoT driver package, or implement one:

```csharp
[Driver("MyProtocol")]
public class MyDriver : DriverBase<Node, MyParameter>
{
    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken ct = default) { … }
    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken ct = default) { … }
}
```

`DriverFactory.ScanAll()` discovers it at startup. Choose protocol `Driver` with driver name `MyProtocol` on a product; point addresses come from each property's `Address`.

**New payload format.** Implement `IPayloadCodec` and register it with `services.AddSingleton<IPayloadCodec, MyCodec>()`. It becomes selectable as a product *codec* for MQTT and HTTP.

**New rule action.** Implement `IRuleAction` (name, parameter schema, `ExecuteAsync`) and register it with `services.AddRuleAction<MyAction>()`. The rule editor renders its parameters automatically.

**New downlink transport.** Implement `IDeviceCommandChannel` (and optionally `IDeviceSessionManager`) and register it.

---

## Stand-alone device simulator

```bash
dotnet run --project tools/IoTPlatform.DeviceSimulator -- --help
dotnet run --project tools/IoTPlatform.DeviceSimulator -- --device env-sensor:th-005:th-005-secret
dotnet run --project tools/IoTPlatform.DeviceSimulator -- --http http://localhost:5288 --http-device http-gateway:http-gw-001:http-gw-001-secret
dotnet run --project tools/IoTPlatform.DeviceSimulator -- --modbus 1503        # extra Modbus slave
```

## Tests

```bash
dotnet test
```

The suite covers the rule evaluator and templates, the codecs, topics and value conversion. It also runs end-to-end integration tests that start the real services with a temporary SQLite DB and cover:
- MQTT: authentication (a bad secret is refused), online on connect, ingest, step rounding, threshold rule → alarm, the ACL, service invoke with reply, and offline on disconnect.
- Drivers: Simulator polling, Modbus TCP polling and write-back against a live `ModbusSlave`, and stopping on disable.
- MQTT brokers: assignment resolution (device → product → platform default), rejecting an unknown profile, option binding (including multiple broker profiles, selection by id/name/default, unknown `ActiveBroker`, legacy single `External`, per-profile topic root), multi-level topic roots, status payloads, an **External-mode end-to-end test**, and a **multi-broker test** that publishes downlink on the device's assigned broker only and ignores uplink arriving on the wrong broker.
- Batch control: scope expansion (product, group with/without sub-groups, device list, skip reasons), a batch property set to live MQTT clients, and HTTP queueing.
- UI components (bUnit): the device control panel sends typed commands and shows their status; the batch dialog previews skips and runs.

## Production notes

- Set `IoT:ApiKey` (`IoT__ApiKey`), change `IoT:Mqtt:AdminPassword` (`IoT__Mqtt__AdminPassword`), and disable the demo options (`IoT__Demo__SeedData=false`, `IoT__Demo__EnableSimulator=false`).
- The Blazor UI has no login. Put it behind your SSO or reverse-proxy auth, or add ASP.NET Core authentication.
- High-volume history (`DeviceData`, `DeviceHistory`) and the per-message shadow / counter updates (`DeviceProperty`, `Device.Messages`/`LastActive`, `DeviceGroup` online counts) are written through XCode's async batch queue (`SaveAsync`). Thing-model rows (`ThingProperty`) are cached in memory. Point `ConnectionStrings:IoT` at a server database for large fleets, and tune `DataRetentionDays`.
- SQLite defaults: WAL journal, `synchronous=NORMAL`, 30s busy timeout. `IoTDatabase.Init` adds `Cache=Shared;Default Timeout=30` even if the configured string is only `Data Source=…`. On Docker Desktop for Windows/macOS, keep `/app/Data` on a named volume — see [Run with Docker](#run-with-docker).
- NewLife logs (`XTrace`) go to the console and to `Log/` under the app directory (the Docker image mounts this as `/app/Log`).
- Prefer [Run with Docker](#run-with-docker) for deployments that should not install the .NET SDK on the host.
