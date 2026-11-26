#!/bin/sh
# 每秒记录容器 cgroup；停止标记后再保留一个边界样本。
out="$1"
stop="$2"
i=0
while [ "$i" -lt 270 ]; do
  ts=$(date +%s)
  cpu=$(awk '$1=="usage_usec"{print $2}' /sys/fs/cgroup/cpu.stat)
  mem=$(cat /sys/fs/cgroup/memory.current)
  peak=$(cat /sys/fs/cgroup/memory.peak)
  oom=$(awk '$1=="oom_kill"{print $2}' /sys/fs/cgroup/memory.events)
  io=$(awk '{printf "%s%s", sep,$0;sep="\\n"}' /sys/fs/cgroup/io.stat)
  printf '{"timestampUnixMilliseconds":%s000,"cpuUsageMicroseconds":%s,"memoryCurrentBytes":%s,"memoryPeakBytes":%s,"oomKill":%s,"ioStat":"%s"}\n' "$ts" "$cpu" "$mem" "$peak" "$oom" "$io" >> "$out"
  [ -e "$stop" ] && break
  i=$((i+1))
  sleep 1
done
touch "$out.done"
