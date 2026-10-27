-- 所有状态、epoch、探测租约和去重均在服务器时钟下原子变更。
local k = KEYS[1]
local sample = k .. ':sample'
local failure = k .. ':failure'
local admitted = k .. ':admitted'
local expiry = k .. ':expiry'
local done = k .. ':done'
local action = ARGV[1]
local cfg = ARGV[2]
local version = ARGV[3]
local ratio = tonumber(ARGV[4])
local minimum = tonumber(ARGV[5])
local window = tonumber(ARGV[6])
local breakms = tonumber(ARGV[7])
local leasems = tonumber(ARGV[8])
local attempt = ARGV[9]
local token = ARGV[10]
local outcome = ARGV[11]
local expected = tonumber(ARGV[12])
local tm = redis.call('TIME')
local now = tonumber(tm[1]) * 1000 + math.floor(tonumber(tm[2]) / 1000)
local keep = math.max(window, breakms, leasems) * 3
if redis.call('EXISTS', k) == 0 then
    redis.call('HSET', k, 'cfg', cfg, 'version', version, 'state', 0, 'epoch', 0, 'openUntil', 0, 'probeUntil', 0, 'probe', '')
end
if redis.call('HGET', k, 'cfg') ~= cfg then return cjson.encode({error='configuration_conflict'}) end
local state = tonumber(redis.call('HGET', k, 'state'))
local epoch = tonumber(redis.call('HGET', k, 'epoch'))
local openUntil = tonumber(redis.call('HGET', k, 'openUntil'))
local probeUntil = tonumber(redis.call('HGET', k, 'probeUntil'))
local probe = redis.call('HGET', k, 'probe')
local function save()
    redis.call('HSET', k, 'state', state, 'epoch', epoch, 'openUntil', openUntil, 'probeUntil', probeUntil, 'probe', probe)
end
local function result(allowed)
    return cjson.encode({allowed=allowed, state=state, epoch=epoch, now=now, openUntil=openUntil, probeUntil=probeUntil, probe=probe})
end
local expired = redis.call('ZRANGEBYSCORE', expiry, '-inf', now)
for _, id in ipairs(expired) do redis.call('HDEL', admitted, id) end
redis.call('ZREMRANGEBYSCORE', expiry, '-inf', now)
redis.call('ZREMRANGEBYSCORE', done, '-inf', now - keep)
redis.call('ZREMRANGEBYSCORE', sample, '-inf', now - window)
redis.call('ZREMRANGEBYSCORE', failure, '-inf', now - window)
local function clearSamples() redis.call('DEL', sample, failure) end
local function open()
    state=1; epoch=epoch+1; openUntil=now+breakms; probeUntil=0; probe=''; clearSamples()
end
local function close()
    state=0; epoch=epoch+1; openUntil=0; probeUntil=0; probe=''; clearSamples()
end
local function touchAuxiliary()
    -- 准入后即开始租约计时。若进程在上报前退出，辅助键也必须自动回收。
    redis.call('PEXPIRE', admitted, keep)
    redis.call('PEXPIRE', expiry, keep)
    redis.call('PEXPIRE', done, keep)
    redis.call('PEXPIRE', sample, keep)
    redis.call('PEXPIRE', failure, keep)
end
if action == 'read' then return result(false) end
if action == 'isolate' or action == 'close' then
    if epoch ~= expected then return cjson.encode({error='epoch_conflict'}) end
    if action == 'isolate' then state=3; epoch=epoch+1; probe=''; probeUntil=0; openUntil=0; clearSamples()
    else close() end
    save()
    return result(false)
end
if action == 'admit' then
    if redis.call('ZSCORE', done, attempt) then return cjson.encode({error='attempt_already_reported'}) end
    local prior = redis.call('HGET', admitted, attempt)
    if prior then
        local p = cjson.decode(prior)
        if p.epoch == epoch then
            return cjson.encode({allowed=true, state=state, epoch=p.epoch, now=now, openUntil=openUntil, probeUntil=p.untilTime, probe=p.token})
        end
        return result(false)
    end
    if state == 3 then return result(false) end
    if state == 1 and now < openUntil then return result(false) end
    if state == 2 and now < probeUntil then return result(false) end
    if redis.call('HLEN', admitted) >= 10000 then return cjson.encode({error='admission_capacity'}) end
    if state == 1 or state == 2 then
        state=2; epoch=epoch+1; probe=token; probeUntil=now+leasems
    end
    local ptoken = ''
    local untilTime = now+keep
    if state == 2 then ptoken=probe; untilTime=probeUntil end
    redis.call('HSET', admitted, attempt, cjson.encode({epoch=epoch,token=ptoken,untilTime=untilTime}))
    redis.call('ZADD', expiry, now+keep, attempt)
    touchAuxiliary()
    save()
    return cjson.encode({allowed=true, state=state, epoch=epoch, now=now, openUntil=openUntil, probeUntil=untilTime, probe=ptoken})
end
if action == 'report' then
    local prior = redis.call('HGET', admitted, attempt)
    if not prior then return result(false) end
    local p = cjson.decode(prior)
    if p.epoch ~= expected or p.token ~= token then return result(false) end
    redis.call('HDEL', admitted, attempt)
    redis.call('ZREM', expiry, attempt)
    redis.call('ZADD', done, now, attempt)
    if epoch ~= expected then return result(false) end
    if state == 2 then
        if probe ~= token or now > probeUntil then return result(false) end
        if outcome == 'success' then close() else open() end
    elseif state == 0 and outcome ~= 'excluded' then
        redis.call('ZADD', sample, now, attempt)
        if outcome == 'failure' then redis.call('ZADD', failure, now, attempt) end
        local total = redis.call('ZCARD', sample)
        if total >= minimum and redis.call('ZCARD', failure) / total >= ratio then open() end
    end
    save()
    -- 隔离主状态永不因 TTL 自动恢复；辅助尝试账本按固定上限时间清理。
    touchAuxiliary()
    return result(false)
end
return cjson.encode({error='unknown_action'})
