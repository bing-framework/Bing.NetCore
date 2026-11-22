#!/usr/bin/env bash
set -euo pipefail

# Windows 托管 runner 在 WSL2 内运行 Linux Docker，并共享当前工作目录。
if [[ -n ${WSL_DISTRIBUTION:-} && -z ${WSL_DISTRO_NAME:-} ]]; then
  export WSLENV="${WSLENV:+$WSLENV:}RUNNER_TEMP/p:GITHUB_ENV/p:WSL_IP"
  wsl.exe --distribution "$WSL_DISTRIBUTION" --user root --exec bash "${BASH_SOURCE[0]}" "$@"
  exit
fi

receipt="${RUNNER_TEMP:?RUNNER_TEMP is required}/sql-aot-oracle-container"
logs=output/sql-aot-oracle

case "${1:-}" in
  start)
    : "${GITHUB_ENV:?GITHUB_ENV is required}"
    mkdir -p "$logs"
    export ORACLE_PWD="Admin1$(openssl rand -hex 8)"
    password="Bing1$(openssl rand -hex 8)"
    echo "::add-mask::$ORACLE_PWD"
    echo "::add-mask::$password"
    image=container-registry.oracle.com/database/free@sha256:51836f712638fff7fd6d9cf8fe2d8cc91b3bd910aaad30292eb9c58935cbce5f
    id=$(docker create -p "${WSL_IP:-127.0.0.1}::1521" -e ORACLE_PWD "$image")
    printf '%s\n' "$id" > "$receipt"
    docker start "$id" > "$logs/start.log"

    health=starting
    for attempt in {1..90}; do
      health=$(docker inspect --format '{{.State.Health.Status}}' "$id")
      if [[ "$health" == healthy ]]; then break; fi
      sleep 2
    done
    printf '%s\n' "$health" > "$logs/ready.log"
    [[ "$health" == healthy ]]

    docker exec -i "$id" sqlplus -s / as sysdba <<SQL | sed "s/$password/[masked]/g" > "$logs/database.log"
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
ALTER SESSION SET CONTAINER=FREEPDB1;
CREATE USER BING_SQL_AOT_TEST IDENTIFIED BY "$password";
GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, UNLIMITED TABLESPACE TO BING_SQL_AOT_TEST;
SELECT BANNER_FULL FROM v\$version;
EXIT
SQL
    binding=$(docker port "$id" 1521/tcp)
    printf 'RUN_ORACLE_INTEGRATION_TESTS=true\nConnectionStrings__OracleConnection=Data Source=%s/FREEPDB1;User Id=BING_SQL_AOT_TEST;Password=%s;Pooling=false\n' \
      "$binding" "$password" >> "$GITHUB_ENV"
    printf '%s\n' ORACLE_FIXTURE_READY_PASS >> "$logs/ready.log"
    ;;
  stop)
    if [[ ! -s "$receipt" ]]; then exit 0; fi
    read -r id < "$receipt"
    [[ "$id" =~ ^[0-9a-f]{64}$ ]]
    remaining=$(docker ps -aq --filter "id=$id")
    if [[ -n "$remaining" ]]; then
      docker rm -f -v "$id" > "$logs/stop.log"
    fi
    rm -- "$receipt"
    printf '%s\n' ORACLE_FIXTURE_CLEANUP_PASS >> "$logs/stop.log"
    ;;
  *)
    echo 'Usage: Invoke-LinuxOracle.sh start|stop' >&2
    exit 2
    ;;
esac
