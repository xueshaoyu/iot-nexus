#!/usr/bin/env bash
# NewLife IoT 演示包。一条命令启动管理端、内置 MQTT 和设备模拟器。
#
#   Web 管理端 / API   http://0.0.0.0:5288     （无登录）
#   MQTT               0.0.0.0:1883            管理账号 admin / admin123
#   演示 Modbus 从站   127.0.0.1:1502
#
# 数据在包内 Data/iot.db。首次启动自动建表，并写入 env-sensor 等演示产品和设备。
# 模拟器随进程启动，界面上会看到在线设备和实时数据（th-005 故意保持离线）。
#
# 用法（在解压目录中）：
#   ./run.sh
# 停止：Ctrl+C
# 换端口示例：
#   ASPNETCORE_URLS=http://0.0.0.0:8080 IoT__Mqtt__Port=1883 ./run.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"
mkdir -p "$ROOT/Data" "$ROOT/Log"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5288}"
export NEWLIFE_DATAPATH="${NEWLIFE_DATAPATH:-$ROOT/Data}"
export NEWLIFE_LOGPATH="${NEWLIFE_LOGPATH:-$ROOT/Log}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if [[ ! -x "$ROOT/IoTPlatform.Web" ]]; then
  echo "找不到 $ROOT/IoTPlatform.Web。请在解压后的演示包目录里运行 ./run.sh" >&2
  exit 1
fi

exec "$ROOT/IoTPlatform.Web"
