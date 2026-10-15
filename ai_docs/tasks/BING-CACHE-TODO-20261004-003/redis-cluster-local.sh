#!/bin/sh
set -eu

for port in 7000 7001 7002 7003 7004 7005; do
    redis-server \
        --port "$port" \
        --bind 0.0.0.0 \
        --protected-mode no \
        --cluster-enabled yes \
        --cluster-config-file "/tmp/nodes-$port.conf" \
        --cluster-node-timeout 5000 \
        --cluster-announce-ip 127.0.0.1 \
        --cluster-announce-port "$port" \
        --cluster-announce-bus-port "$((port + 10000))" \
        --appendonly no \
        --save "" &
done

wait
