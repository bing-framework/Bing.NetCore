#!/bin/sh
set -eu

for port in 7000 7001 7002 7003 7004 7005; do
    mkdir -p "/tmp/cache-cluster/$port"
    redis-server --port "$port" --cluster-enabled yes \
        --cluster-config-file "/tmp/cache-cluster/$port/nodes.conf" \
        --cluster-node-timeout 5000 --appendonly no --save '' \
        --daemonize yes --bind 0.0.0.0 --protected-mode no \
        --cluster-announce-ip 127.0.0.1 --cluster-announce-port "$port" \
        --cluster-announce-bus-port "$((port + 10000))"
done

redis-cli --cluster create 127.0.0.1:7000 127.0.0.1:7001 127.0.0.1:7002 \
    127.0.0.1:7003 127.0.0.1:7004 127.0.0.1:7005 \
    --cluster-replicas 1 --cluster-yes
