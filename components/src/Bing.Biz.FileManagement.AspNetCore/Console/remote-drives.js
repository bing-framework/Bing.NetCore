import { api, list } from './uploader.js';

const $ = id => document.getElementById(id);
let contextKey = '', generation = 0;
let canManageConnections = false;
const copyRuns = new Map();
const post = (path, body) => api(path, { method: 'POST', body: JSON.stringify(body) });
const notice = text => { $('notice').textContent = text; };
const run = action => action().catch(error => notice('操作未完成：' + error.message));
const saved = key => { try { return JSON.parse(localStorage.getItem(contextKey + key) || '[]'); } catch { return []; } };
const savedCopies = storageKey => { try { return JSON.parse(localStorage.getItem(storageKey) || '[]'); } catch { return []; } };
const saveCopy = (storageKey, request) => {
    const requests = savedCopies(storageKey);
    let index = requests.findIndex(item => item.requestId === request.requestId);
    if (index < 0) index = requests.findIndex(item => item.sourceId === request.sourceId
        && item.businessType === request.businessType && item.businessId === request.businessId);
    if (index < 0) requests.push(request);
    else requests[index] = request;
    localStorage.setItem(storageKey, JSON.stringify(requests));
};
function button(label, action) { const item = document.createElement('button'); item.textContent = label; item.onclick = () => run(action); return item; }

export async function connectDrives(context, capabilities = {}) {
    contextKey = 'bing-file-drives:' + JSON.stringify([context.tenantId || '', context.userId]); generation++;
    canManageConnections = capabilities.manageConnections === true;
    $('scope-default-panel').hidden = !canManageConnections;
    $('drive-management-panel').hidden = !canManageConnections;
    const sharedOption = [...$('drive-ownership').children].find(option => option.value === '0');
    if (sharedOption) sharedOption.hidden = !canManageConnections;
    $('drive-authorization').replaceChildren();
    await refreshDrives(); await refreshCopies();
    const query = new URLSearchParams(location.search), state = query.get('state'), code = query.get('code');
    const auth = saved(':authorization').find(item => item.state === state);
    if (auth && code) {
        await post('/storage-connections/' + auth.id + '/authorize', { state, code });
        history.replaceState(null, '', location.pathname); await refreshDrives(); notice('网盘授权已完成。');
    }
}
async function refreshDrives() {
    const current = generation, targets = await api('/storage-targets?take=100');
    let next = targets.nextSkip;
    while (next != null && current === generation) {
        const page = await api('/storage-targets?take=100&skip=' + next);
        targets.connections.push(...page.connections); next = page.nextSkip;
    }
    if (current !== generation) return;
    $('drive-template').replaceChildren(...targets.templates.map(t => new Option(t.name + ' · ' + t.provider, t.name)));
    $('connections-panel').hidden = targets.templates.length === 0;
    const connectionOptions = targets.connections.map(c =>
        new Option(c.displayName + (c.ownership === 1 ? '（个人）' : '（公共）'), c.id.replaceAll('-', '')));
    $('storage-target').replaceChildren(new Option('使用默认偏好', ''), ...targets.stores.map(name => new Option(name, name)), ...connectionOptions);
    $('scope-default-target').replaceChildren(new Option('选择默认目标', ''),
        ...targets.stores.map(name => new Option(name, name)),
        ...targets.connections.filter(c => c.ownership === 0).map(c =>
            new Option(c.displayName + '（公共）', c.id.replaceAll('-', ''))));
    const connections = await listConnections(false, current);
    if (current !== generation) return;
    $('drive-connections').replaceChildren(...connections.filter(c => c.ownership === 1).map(c => {
        const row = document.createElement('p'); row.append(document.createTextNode(c.displayName + ' · ' + ['待授权', '可用', '需要重新授权', '已停用'][c.state]));
        row.append(button('重新授权', () => authorize(c)), button('停用', async () => { await api('/storage-connections/' + c.id, { method: 'DELETE' }); await refreshDrives(); })); return row;
    }));
    if (canManageConnections) await refreshManagedDrives(current);
}
async function listConnections(manage, current) {
    const rows = []; let page;
    do {
        const query = manage ? 'manage=true&' : '';
        page = await api('/storage-connections?' + query + 'take=100&skip=' + rows.length);
        rows.push(...page);
    } while (page.length === 100 && current === generation);
    return rows;
}
async function refreshManagedDrives(expectedGeneration = generation) {
    if (!canManageConnections || expectedGeneration !== generation) return;
    const connections = await listConnections(true, expectedGeneration);
    if (expectedGeneration !== generation) return;
    $('managed-drive-connections').replaceChildren(...connections.map(c => {
        const row = document.createElement('p');
        row.append(document.createTextNode(c.displayName + ' · ' + (c.ownership === 1 ? '个人连接' : '公共连接')
            + ' · ' + ['待授权', '可用', '需要重新授权', '已停用'][c.state]));
        if (c.ownership === 0) row.append(button('重新授权', () => authorize(c)));
        if (c.state !== 3) row.append(button('停用', async () => {
            await api('/storage-connections/' + c.id, { method: 'DELETE' }); await refreshDrives();
        }));
        return row;
    }));
}
async function authorize(existing) {
    const current = generation;
    const result = await post('/storage-connections/authorize', { template: existing?.template || $('drive-template').value,
        displayName: existing?.displayName || $('drive-name').value, ownership: existing?.ownership ?? Number($('drive-ownership').value),
        reconnectId: existing?.id || null, redirectUri: location.origin + location.pathname });
    if (current !== generation) return;
    const auth = saved(':authorization').filter(x => x.id !== result.id); auth.push({ id: result.id, state: result.authorizationState });
    localStorage.setItem(contextKey + ':authorization', JSON.stringify(auth));
    const url = new URL(result.authorizationUrl); if (!['https:', 'http:'].includes(url.protocol)) throw new Error('不支持的授权地址');
    const link = document.createElement('a'); link.href = url.href; link.target = '_blank'; link.rel = 'noopener noreferrer'; link.textContent = '打开官方授权页面（115 请使用官方扫码确认）';
    $('drive-authorization').replaceChildren(link, button('我已确认授权', async () => {
        const connection = await post('/storage-connections/' + result.id + '/authorize', { state: result.authorizationState });
        notice(connection.state === 1 ? '网盘连接已授权。' : '等待官方授权确认。'); await refreshDrives();
    }));
    if (result.authorizationQrPng) {
        const qr = document.createElement('img'); qr.alt = '使用官方应用扫描以授权此连接'; qr.width = 240; qr.height = 240;
        qr.src = 'data:image/png;base64,' + result.authorizationQrPng; $('drive-authorization').prepend(qr);
    }
}
export function copyAndBind(file, businessType, businessId) {
    if (!contextKey) return Promise.reject(new Error('请先连接当前账号。'));
    const current = generation, ownerKey = contextKey;
    const key = JSON.stringify([ownerKey, current, file.id, businessType, businessId]);
    if (copyRuns.has(key)) return copyRuns.get(key);
    const promise = copyAndBindCore(file, businessType, businessId, ownerKey, current)
        .finally(() => { if (copyRuns.get(key) === promise) copyRuns.delete(key); });
    copyRuns.set(key, promise);
    return promise;
}
async function copyAndBindCore(file, businessType, businessId, ownerKey, current) {
    const storageKey = ownerKey + ':copies';
    let request = savedCopies(storageKey).find(x => x.sourceId === file.id && x.businessType === businessType && x.businessId === businessId);
    if (!request) {
        request = { requestId: crypto.randomUUID(), sourceId: file.id, businessType, businessId };
        saveCopy(storageKey, request);
    }

    for (let creationAttempt = 0; current === generation; creationAttempt++) {
        let status;
        if (request.id) {
            status = await api('/binding-copies/' + request.id);
            if (current !== generation) return;
        } else {
            const created = await post('/' + file.id + '/binding-copies', request);
            if (current !== generation) return;
            request.id = created.id;
            saveCopy(storageKey, request);
            status = created;
        }

        if (status.state === 2) {
            if (!status.copyFileId) throw new Error('已绑定请求没有副本标识，请刷新请求状态。');
            notice('此附件已关联，正在选择已有公共副本。');
            await selectCopy(status.copyFileId);
            return;
        }
        if (status.state === 3 || status.state === 4) {
            if (creationAttempt >= 1) {
                notice(status.state === 3 ? '复制请求已取消，请稍后刷新状态。' : '复制请求失败，请稍后刷新状态。');
                if (current === generation) await refreshCopies();
                return;
            }
            request = { requestId: crypto.randomUUID(), sourceId: file.id, businessType, businessId };
            saveCopy(storageKey, request);
            notice(status.state === 3 ? '上次复制已取消，正在重新发起。' : '上次复制失败，正在重新尝试。');
            continue;
        }

        notice(status.state === 1 ? '副本已就绪，正在完成业务关联。' : '正在复制到公共存储。个人原件会保留，关闭页面后可恢复。');
        for (let i = 0; i < 60 && current === generation; i++) {
            if (status.state === 1) {
                const bound = await post('/binding-copies/' + request.id + '/bind');
                if (current !== generation) return;
                notice('已关联公共副本：' + bound.fileId);
                await selectCopy(bound.fileId);
                if (current === generation) await refreshCopies();
                return;
            }
            if (status.state === 2) {
                if (!status.copyFileId) throw new Error('已绑定请求没有副本标识，请刷新请求状态。');
                await selectCopy(status.copyFileId);
                if (current === generation) await refreshCopies();
                return;
            }
            if (status.state === 3 || status.state === 4) {
                notice(status.state === 3 ? '复制已取消；再次操作可重新创建请求。' : '复制失败；再次操作可重新尝试。');
                break;
            }
            if (status.state !== 0) {
                notice('复制请求状态未知，请刷新请求状态。');
                break;
            }
            await new Promise(resolve => setTimeout(resolve, 1000));
            if (current !== generation) return;
            status = await api('/binding-copies/' + request.id);
            if (current !== generation) return;
        }
        if (current === generation) await refreshCopies();
        return;
    }
}
async function refreshCopies() {
    const current = generation, panel = $('binding-copies'); panel.replaceChildren();
    for (const request of saved(':copies').filter(x => x && x.requestId && x.sourceId)) {
        if (!request.id) {
            const row = document.createElement('p');
            row.append(document.createTextNode(request.businessType + '/' + request.businessId + ' · 请求待恢复'));
            row.append(button('恢复复制请求', () => copyAndBind({ id: request.sourceId }, request.businessType, request.businessId)));
            panel.append(row);
            continue;
        }
        const status = await api('/binding-copies/' + request.id);
        if (current !== generation) return;
        const row = document.createElement('p');
        row.append(document.createTextNode(request.businessType + '/' + request.businessId + ' · ' + ['复制中', '可绑定', '已绑定', '已取消', '失败'][status.state]));
        if (status.state === 0 || status.state === 1) row.append(button(status.state === 0 ? '继续并关联' : '绑定公共副本',
            () => copyAndBind({ id: request.sourceId }, request.businessType, request.businessId)));
        if (status.state === 2 && status.copyFileId) row.append(button('选择已有副本', () => selectCopy(status.copyFileId)));
        if (status.state === 3 || status.state === 4) row.append(button('重新复制并关联',
            () => copyAndBind({ id: request.sourceId }, request.businessType, request.businessId)));
        if (status.state === 0 || status.state === 1) row.append(button('取消并回收副本', async () => {
            await api('/binding-copies/' + request.id, { method: 'DELETE' }); await refreshCopies();
        }));
        panel.append(row);
    }
}
async function selectCopy(fileId) {
    const current = generation; const file = await api('/' + fileId);
    if (current !== generation) return;
    document.dispatchEvent(new CustomEvent('bing-file-selected', { detail: file })); await list();
}
$('authorize-drive').onclick = () => run(() => authorize());
$('refresh-drives').onclick = () => run(refreshDrives);
$('refresh-managed-drives').onclick = () => run(() => refreshManagedDrives());
$('refresh-copies').onclick = () => run(refreshCopies);
$('save-storage-preference').onclick = () => run(async () => {
    if (!$('storage-target').value) throw new Error('先选择一个具体上传目标');
    await api('/storage-preference', { method: 'PUT', body: JSON.stringify({ container: $('container').value, targetId: $('storage-target').value }) }); notice('默认上传目标已保存，新会话开始生效。');
});
$('save-scope-default').onclick = () => run(async () => {
    if (!canManageConnections) throw new Error('缺少存储连接管理权限');
    if (!$('scope-default-target').value) throw new Error('先选择静态存储或公共连接');
    await api('/storage-preference', { method: 'PUT', body: JSON.stringify({
        container: $('container').value, targetId: $('scope-default-target').value, scopeDefault: true
    }) });
    notice('范围默认目标已保存，仅影响之后创建的上传会话。');
});
