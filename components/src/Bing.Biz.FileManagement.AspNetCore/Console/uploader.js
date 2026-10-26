const $ = id => document.getElementById(id);
let storageKey = null;
let saved = null;
let tasks = {};
let activeKey = null;
let nextCursor = null;
let sessionSkip = 0;
let running = false;
let busy = false;
let identityChanging = false;
const controllers = new Set();
export { api, list };
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
const persist = () => {
    if (!storageKey) return;
    if (saved) { activeKey = saved.requestId; tasks[activeKey] = saved; }
    else if (activeKey) { delete tasks[activeKey]; activeKey = null; }
    localStorage.setItem(storageKey, JSON.stringify({ tasks, activeKey })); renderSessions();
};
function renderSessions() {
    $('sessions').replaceChildren(new Option('新上传', ''));
    for (const [key, task] of Object.entries(tasks)) $('sessions').append(new Option(task.name + ' · ' + task.size.toLocaleString() + ' B', key));
    $('sessions').value = activeKey || '';
}
async function loadContext() {
    const context = await api('/context');
    const key = 'bing-file-demo-sessions-v2:' + context.scopeKey + ':' + encodeURIComponent(context.userId);
    if (key === storageKey) return;
    storageKey = key;
    let record = {};
    try { record = JSON.parse(localStorage.getItem(key) || '{}'); } catch { /* 无效本地缓存可从服务端找回。 */ }
    tasks = record.tasks && typeof record.tasks === 'object' ? record.tasks : {};
    activeKey = record.activeKey || null; saved = tasks[activeKey] || null;
    nextCursor = null; sessionSkip = 0; renderSessions();
}
async function discover(more = false) {
    if (busy) return status('请先暂停当前上传并等待结束。');
    try {
        await loadContext();
        if (!more) sessionSkip = 0;
        const sessions = await api('/upload-sessions?skip=' + sessionSkip + '&take=100');
        for (const session of sessions) {
            if (![0, 1, 2, 8].includes(session.state)) continue;
            let task = Object.values(tasks).find(t => t.id === session.id);
            if (!task) { task = { requestId: crypto.randomUUID(), id: session.id, fileId: session.fileId, name: session.fileName,
                size: session.length, lastModified: session.lastModified, container: session.container, contentType: session.contentType }; tasks[task.requestId] = task; }
        }
        sessionSkip += sessions.length; $('more-sessions').disabled = sessions.length < 100;
        localStorage.setItem(storageKey, JSON.stringify({ tasks, activeKey })); renderSessions();
        status('已找回 ' + sessions.length + ' 个任务。选择任务并重新选择原文件后可以恢复。');
    } catch (e) { status('找回任务失败：' + e.message, true); }
}
const auth = () => ({ Authorization: 'Bearer ' + $('token').value });
async function api(path, options = {}) {
    const response = await fetch('/api/files' + path, { ...options, headers: { ...auth(), ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
    if (!response.ok) { const body = await response.json().catch(() => ({})); throw new Error(body.code || 'HTTP ' + response.status); }
    return response.status === 204 || response.headers.get('content-length') === '0' ? null : response.json().catch(() => null);
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const sha256 = async blob => btoa(String.fromCharCode(...new Uint8Array(await crypto.subtle.digest('SHA-256', await blob.arrayBuffer()))));
async function getSession() { return api('/upload-sessions/' + saved.id); }
async function waitForState(wanted) {
    while (running) {
        const session = await getSession();
        if (wanted.includes(session.state)) return session;
        if ([6, 7].includes(session.state)) throw new Error('上传已中止或失败，请创建新会话。');
        status('服务端正在处理：' + ({ 0: '待初始化', 1: '上传中', 2: '完成中', 3: '已就绪', 8: '内容检查中' }[session.state] || session.state));
        await sleep(1000);
    }
    throw new DOMException('已暂停', 'AbortError');
}
async function start() {
    if (busy) return;
    const file = $('file').files[0];
    if (!file) return status('请重新选择要上传的文件。', true);
    running = true; busy = true; $('token').disabled = true; $('start').disabled = true; $('pause').disabled = false;
    try {
        await loadContext();
        if (saved && (file.name !== saved.name || file.size !== saved.size || file.lastModified !== saved.lastModified))
            throw new Error('文件名、大小或修改时间与原会话不同，请选择原文件，或先中止旧会话。');
        if (!saved) {
            const requestId = crypto.randomUUID();
            // 先保存幂等请求号，即使创建响应丢失也不会重复分配附件。
            saved = { requestId, name: file.name, size: file.size, lastModified: file.lastModified, container: $('container').value, contentType: file.type || 'application/octet-stream' };
            persist();
        }
        if (!saved.id) {
            const created = await api('/upload-sessions', { method: 'POST', body: JSON.stringify({ requestId: saved.requestId, fileName: saved.name,
                length: saved.size, lastModified: saved.lastModified, container: saved.container, contentType: saved.contentType || file.type || 'application/octet-stream' }) });
            saved.id = created.id; saved.fileId = created.fileId; persist();
        }
        const session = await waitForState([1, 2, 3, 8]);
        if (session.state === 3) { status('该会话已完成。文件 ID：' + saved.fileId); saved = null; persist(); await list(); return; }
        if ([2, 8].includes(session.state)) { await waitForState([3]); status('上传完成。文件 ID：' + saved.fileId); saved = null; persist(); await list(); return; }
        const completed = new Set();
        let confirmed = 0;
        // 以服务端清单为准，并校验重新选择的文件内容。
        for (const part of session.parts) {
            const blob = file.slice((part.number - 1) * session.partSize, Math.min(file.size, part.number * session.partSize));
            if (await sha256(blob) !== part.sha256) throw new Error('已上传分片内容不同，拒绝错误续传。');
            completed.add(part.number); confirmed += part.length;
        }
        const totalParts = Math.ceil(file.size / session.partSize);
        let next = 1;
        const progress = () => { $('progress').value = file.size ? 100 * confirmed / file.size : 0;
            status('已确认 ' + confirmed.toLocaleString() + ' / ' + file.size.toLocaleString() + ' 字节；可暂停或刷新后恢复。'); };
        progress();
        async function worker() {
            while (running) {
                const number = next++;
                if (number > totalParts) return;
                if (completed.has(number)) continue;
                const blob = file.slice((number - 1) * session.partSize, Math.min(file.size, number * session.partSize));
                const checksum = await sha256(blob);
                for (let attempt = 0; attempt < 3; attempt++) {
                    if (!running) return;
                    const signed = await api('/upload-sessions/' + saved.id + '/part-requests', { method: 'POST', body: JSON.stringify({ number, sha256: checksum }) });
                    if (!running) return;
                    const controller = new AbortController(); controllers.add(controller);
                    try {
                        const url = new URL(signed.url, location.href);
                        const response = await fetch(url, { method: signed.method, headers: { ...(url.origin === location.origin ? auth() : {}), ...signed.headers },
                            body: blob, signal: controller.signal, credentials: 'omit' });
                        if (response.ok) break;
                        // 签名过期或后端暂时不可达时重新申请，不能重复使用旧签名。
                        if (attempt === 2 || ![403, 408, 429, 500, 502, 503].includes(response.status)) throw new Error('分片上传 HTTP ' + response.status);
                    } finally { controllers.delete(controller); }
                }
                if (!running) return;
                confirmed += blob.size; completed.add(number); progress();
            }
        }
        const outcomes = await Promise.allSettled(Array.from({ length: 4 }, async () => {
            try { await worker(); } catch (error) { running = false; for (const controller of controllers) controller.abort(); throw error; }
        }));
        const failed = outcomes.find(outcome => outcome.status === "rejected");
        if (failed) throw failed.reason;
        if (!running) return;
        await api('/upload-sessions/' + saved.id + '/complete', { method: 'POST' });
        await waitForState([3]);
        $('progress').value = 100; status('上传完成。文件 ID：' + saved.fileId);
        saved = null; persist(); await list();
    } catch (error) {
        running = false; for (const controller of controllers) controller.abort();
        status(error.name === 'AbortError' ? '已暂停。重新选择同一个文件后可恢复。' : '上传未完成：' + error.message + '。会话已保留，可恢复。', error.name !== 'AbortError');
    } finally { running = false; busy = false; $('token').disabled = false; $('start').disabled = false; $('pause').disabled = true; }
}
async function list(more = false) {
    try {
        await loadContext();
        const query = new URLSearchParams({ name: $('search-name').value, take: '50', manage: String($('manage-files')?.checked || false) });
        if (more && nextCursor) query.set('cursor', nextCursor);
        const page = await api('/search?' + query);
        nextCursor = page.nextCursor; $('next-page').disabled = !nextCursor;
        const files = page.items; $('files').replaceChildren();
        for (const file of files) {
            const row = document.createElement('tr');
            const name = document.createElement('td'); name.textContent = file.fileName;
            const size = document.createElement('td'); size.textContent = file.length.toLocaleString() + ' B';
            const actions = document.createElement('td');
            const download = document.createElement('button'); download.textContent = '下载';
            download.onclick = async () => { try { const link = await api('/' + file.id + '/download-link', { method: 'POST' }); location.assign(link.url); } catch (e) { status(e.message, true); } };
            const remove = document.createElement('button'); remove.textContent = '删除';
            remove.onclick = async () => { if (!confirm('删除文件“' + file.fileName + '”？有业务关联的文件需要先解除关联。')) return;
                try { await api('/' + file.id, { method: 'DELETE' }); await list(); } catch (e) { status(e.message, true); } };
            if ($('bindings-panel')) {
                const bindings = document.createElement('button'); bindings.textContent = '业务关联';
                bindings.onclick = () => document.dispatchEvent(new CustomEvent('bing-file-selected', { detail: file })); actions.append(bindings);
            }
            actions.append(download, remove); row.append(name, size, actions); $('files').append(row);
        }
    } catch (error) { status('列表查询失败：' + error.message, true); }
}
$('start').onclick = start;
$('pause').onclick = () => { running = false; for (const controller of controllers) controller.abort(); status('正在暂停；已提交分片保留。'); };
$('refresh').onclick = async () => {
    if (!saved?.id) return status('没有可恢复会话。');
    try { const s = await getSession(); status('会话：' + s.id + '\n状态：' + s.state + '\n服务端已确认分片：' + s.parts.length); } catch (e) { status(e.message, true); }
};
$('abort').onclick = async () => {
    if (busy) return status('请先暂停上传并等待结束。');
    try { if (saved?.id) await api('/upload-sessions/' + saved.id, { method: 'DELETE' }); saved = null; persist(); $('progress').value = 0; status('会话已清除，可以选择新文件。'); }
    catch (e) { status(e.message, true); }
};
$('list').onclick = () => list(false);
$('next-page').onclick = () => list(true);
$('discover').onclick = () => discover(false);
$('more-sessions').onclick = () => discover(true);
$('sessions').onchange = () => {
    if (busy) { $('sessions').value = activeKey || ''; return status('请先暂停上传并等待结束。'); }
    activeKey = $('sessions').value || null; saved = tasks[activeKey] || null;
    localStorage.setItem(storageKey, JSON.stringify({ tasks, activeKey }));
    $('file').value = ''; status(saved ? '请重新选择原文件：' + saved.name : '请选择新文件。');
};
$('new-upload').onclick = () => {
    if (busy) return status('请先暂停当前上传并等待结束。');
    activeKey = null; saved = null; $('file').value = ''; persist(); status('已保留其他任务，可以上传新文件。');
};
$('token').onchange = async () => {
    if (identityChanging) return;
    identityChanging = true; $('token').disabled = true;
    running = false; for (const controller of controllers) controller.abort();
    $('files').replaceChildren();
    try { await loadContext(); await list(); } catch (e) { status('身份读取失败：' + e.message, true); }
    finally { identityChanging = false; $('token').disabled = busy; }
    document.dispatchEvent(new Event('bing-files-context'));
};
if ($('backend')) fetch('/demo/config').then(r => r.json()).then(c => $('backend').textContent = c.provider + ' · ' + c.orm).catch(() => $('backend').textContent = '服务未连接');
if ($('token').value) list();
