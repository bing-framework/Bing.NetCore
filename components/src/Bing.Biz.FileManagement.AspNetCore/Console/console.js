import { api, list } from './uploader.js';

const $ = id => document.getElementById(id);
const show = (message, error = false) => { $('notice').textContent = message; $('notice').classList.toggle('error', error); };
const post = (path, body) => api(path, { method: 'POST', body: JSON.stringify(body) });
const requests = new Map();
let identityGeneration = 0;
let selectedFile = null;
let preview = null;
let migrationSelection = null;
const date = value => value && value < 8640000000000000 ? new Date(value).toLocaleString() : '—';
const operationStates = ['等待', '执行中', '成功', '失败'];
const batchStates = ['运行', '暂停', '完成'];
const kinds = ['任务', '事件', '迁移批次'];
const reason = () => { const value = $('reason').value.trim(); if (!value) throw new Error('请填写管理操作原因。'); return value; };

// 同一请求响应丢失后复用请求号；身份变化时清空，令牌从不落盘。
async function command(path, body) {
    const key = path + JSON.stringify(body);
    if (!requests.has(key)) requests.set(key, crypto.randomUUID());
    const result = await post(path, { ...body, requestId: requests.get(key) });
    requests.delete(key); return result;
}
async function run(action, button) {
    if (button) button.disabled = true;
    try { await action(); }
    catch (error) { show('操作未完成：' + error.message + '。请检查权限或刷新当前记录后重试。', true); }
    finally { if (button?.isConnected) button.disabled = false; }
}
function button(label, action) {
    const item = document.createElement('button'); item.textContent = label;
    item.onclick = () => run(action, item); return item;
}
function table(target, headings, rows) {
    const wrapper = $(target); wrapper.replaceChildren();
    if (!rows.length) { const empty = document.createElement('p'); empty.textContent = '暂无记录'; wrapper.append(empty); return; }
    const result = document.createElement('table'), head = document.createElement('thead'), body = document.createElement('tbody');
    const hr = document.createElement('tr');
    for (const label of headings) { const th = document.createElement('th'); th.textContent = label; hr.append(th); }
    head.append(hr);
    for (const cells of rows) {
        const row = document.createElement('tr');
        for (const content of cells) {
            const cell = document.createElement('td');
            if (Array.isArray(content)) cell.append(...content);
            else cell.textContent = content == null ? '—' : String(content);
            row.append(cell);
        }
        body.append(row);
    }
    result.append(head, body); wrapper.append(result);
}
function pager(name, query, render) {
    let offset = 0, loading = false;
    const load = async (more = false) => {
        if (loading) return;
        loading = true;
        const generation = identityGeneration;
        try {
            const nextOffset = more ? offset + 20 : 0;
            const path = query(); const items = await api(path + (path.includes('?') ? '&' : '?') + 'skip=' + nextOffset + '&take=20');
            if (generation !== identityGeneration) return;
            offset = nextOffset; render(items); $(name + '-more').disabled = items.length < 20;
        } finally { loading = false; }
    };
    $(name + '-load').onclick = () => run(() => load(false));
    $(name + '-more').onclick = () => run(() => load(true));
    return load;
}

async function connect() {
    const generation = ++identityGeneration;
    selectedFile = null; preview = null; migrationSelection = null; requests.clear();
    $('identity').textContent = '正在连接…';
    $('bindings-panel').hidden = true; $('history-archive').disabled = true; $('migration-create').disabled = true;
    for (const id of ['operations', 'batches', 'events', 'event-detail', 'history', 'audit', 'history-preview-result', 'migration-preview-result']) $(id).replaceChildren();
    document.querySelectorAll('.admin-only').forEach(el => el.hidden = true);
    $('manage-files').checked = false;
    const capabilities = await api('/capabilities');
    if (generation !== identityGeneration) return;
    $('identity').textContent = capabilities.context.userId + ' · ' + (capabilities.context.tenantId || '宿主');
    $('container').replaceChildren(...capabilities.containers.map(name => new Option(name, name)));
    $('retention-days').value = capabilities.historyRetentionDays;
    $('retention-days').min = capabilities.historyRetentionDays;
    document.querySelectorAll('.admin-only').forEach(el => el.hidden = !capabilities.manage);
    show(capabilities.manage ? '已连接，可管理当前范围的文件与后台任务。' : '已连接，可上传和管理本人有权限的附件。');
}
$('connect').onclick = () => { $('token').dispatchEvent(new Event('change')); };
document.addEventListener('bing-files-context', () => run(connect));
$('manage-files').onchange = () => list(false);
document.addEventListener('bing-file-selected', event => run(async () => {
    selectedFile = event.detail; $('bindings-panel').hidden = false;
    $('binding-title').textContent = '业务关联 · ' + selectedFile.fileName;
    await loadBindings(); $('bindings-panel').scrollIntoView({ block: 'nearest', behavior: 'smooth' });
}));
async function loadBindings() {
    if (!selectedFile) return;
    const fileId = selectedFile.id, generation = identityGeneration;
    const bindings = await api('/' + fileId + '/bindings');
    if (generation !== identityGeneration || selectedFile?.id !== fileId) return;
    table('bindings', ['业务类型', '业务标识', '操作'], bindings.map(b => [b.businessType, b.businessId, [button('解除关联', async () => {
        await api('/' + fileId + '/bindings/' + b.id, { method: 'DELETE' }); await loadBindings(); show('关联已解除，文件内容仍保留。');
    })]]));
}
$('bindings-refresh').onclick = () => run(loadBindings);
$('bind').onclick = () => run(async () => {
    if (!selectedFile) return;
    await post('/' + selectedFile.id + '/bindings', { businessType: $('business-type').value.trim(), businessId: $('business-id').value.trim() });
    await loadBindings(); show('业务关联已添加。');
}, $('bind'));

const loadOperations = pager('operations', () => '/operations?' + new URLSearchParams($('operation-state').value ? { state: $('operation-state').value } : {}), items =>
    table('operations', ['任务 / 文件', '状态', '尝试 / 错误', '创建时间', '操作'], items.map(item => [item.id + '\n' + item.fileId,
        operationStates[item.state], item.attempts + ' / ' + (item.errorCode || '—'), date(item.createdAt), [
            button('审计', async () => show(JSON.stringify(await api('/operations/' + item.id + '/audit'), null, 2))),
            ...(item.recoveryAction === 'retry-if-current' ? [button('重新排队', async () => {
                await command('/operations/' + item.id + '/retry', { expectedVersion: item.version, reason: reason() });
                await loadOperations(); show('后继任务已排队。');
            })] : [])
        ]])));
const loadBatches = pager('batches', () => '/migration-batches', items => table('batches', ['批次', '源 → 目标', '状态', '操作'], items.map(item => [
    item.id, item.sourceStore + ' → ' + item.targetStore, batchStates[item.state], [
        button('进度', async () => show(JSON.stringify(await api('/migration-batches/' + item.id), null, 2))),
        ...(item.state !== 2 ? [button(item.state === 0 ? '暂停' : '恢复', async () => {
            await post('/migration-batches/' + item.id + (item.state === 0 ? '/pause' : '/resume'), { expectedVersion: item.version });
            await loadBatches(); show('批次状态已更新。');
        })] : []),
        button('重试失败项', async () => { await post('/migration-batches/' + item.id + '/retry-failed', { expectedVersion: item.version, reason: reason() }); await loadBatches(); show('符合条件的失败项已排队。'); })
    ]])));
const selection = () => ({ sourceStore: $('source-store').value.trim(), targetStore: $('target-store').value.trim() });
for (const id of ['source-store', 'target-store']) $(id).oninput = () => { migrationSelection = null; $('migration-create').disabled = true; };
$('migration-preview').onclick = () => run(async () => {
    migrationSelection = selection();
    const result = await post('/migration-batches/preview', { selection: migrationSelection });
    $('migration-preview-result').textContent = JSON.stringify(result, null, 2); $('migration-create').disabled = result.items.length === 0;
});
$('migration-create').onclick = () => run(async () => {
    if (!migrationSelection) return;
    const batch = await command('/migration-batches', migrationSelection); migrationSelection = null;
    $('migration-create').disabled = true; await loadBatches(); show('迁移批次已创建：' + batch.id);
});
const loadEvents = pager('events', () => '/events?failedOnly=' + $('failed-events').checked, items => table('events', ['事件 / 文件', '状态', '创建时间', '操作'], items.map(item => [
    item.kind + '\n' + item.fileId, item.delivered ? '已投递' : item.processing ? '处理中' : item.errorCode || '等待投递', date(item.createdAt), [button('回执与重放', () => eventDetail(item.id))]
])));
async function eventDetail(id) {
    const generation = identityGeneration, detail = await api('/events/' + id);
    if (generation !== identityGeneration) return;
    table('event-detail', ['消费者', '状态', '次数 / 错误', '操作'], detail.consumers.map(name => {
        const receipt = detail.deliveries.find(r => r.consumer === name);
        return [name, receipt?.delivered ? '已确认' : receipt ? '未确认' : '未订阅', (receipt?.attempts || 0) + ' / ' + (receipt?.errorCode || '—'), [button('重放此消费者', async () => {
            await command('/events/' + id + '/replay', { consumer: name, expectedVersion: detail.event.version, reason: reason() });
            await eventDetail(id); await loadEvents(); show('已排队重放；其他消费者的确认结果保留。');
        })]];
    }));
    const missing = detail.deliveries.filter(r => !detail.consumers.includes(r.consumer));
    if (missing.length) { const text = document.createElement('p'); text.textContent = '未注册消费者的历史回执：' + JSON.stringify(missing); $('event-detail').append(text); }
}
const loadHistory = pager('history', () => '/history', items => table('history', ['类型 / 记录', '归档人', '归档时间', '操作'], items.map(item => [
    kinds[item.kind] + '\n' + item.entityId, item.actorId, date(item.archivedAt), [button('恢复到列表', async () => {
        await command('/history/' + item.id + '/restore', { reason: reason() }); await loadHistory(); show('记录已恢复到默认管理列表。');
    })]
])));
for (const id of ['history-kind', 'retention-days']) $(id).onchange = () => { preview = null; $('history-archive').disabled = true; };
$('history-preview').onclick = () => run(async () => {
    preview = await post('/history/preview', { kind: Number($('history-kind').value), retentionDays: Number($('retention-days').value), take: 20 });
    $('history-preview-result').textContent = JSON.stringify(preview.selection, null, 2); $('history-archive').disabled = preview.selection.items.length === 0;
});
$('history-archive').onclick = () => run(async () => {
    if (!preview) return;
    await command('/history/archive', { token: preview.token, reason: reason() }); preview = null;
    $('history-archive').disabled = true; $('history-preview-result').textContent = ''; await loadHistory(); show('预览记录已归档。');
});
pager('audit', () => '/governance-audit', items => table('audit', ['操作', '操作者 / 原因', '时间', '结果'], items.map(item => [
    item.action, item.actorId + '\n' + item.reason, date(item.createdAt), item.resultJson
])));
