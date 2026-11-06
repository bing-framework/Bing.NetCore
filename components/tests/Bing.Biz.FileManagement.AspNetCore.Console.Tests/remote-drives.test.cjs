const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const sourcePath = path.resolve(__dirname, '../../src/Bing.Biz.FileManagement.AspNetCore/Console/remote-drives.js');
const source = fs.readFile(sourcePath, 'utf8');

class Element {
    constructor(tagName = 'div') {
        this.tagName = tagName;
        this.children = [];
        this._text = '';
        this.value = '';
        this.hidden = false;
        this.disabled = false;
        this.isConnected = true;
        this.classList = { toggle() {} };
    }
    get textContent() { return this._text + this.children.map(child => child.textContent || '').join(''); }
    set textContent(value) { this._text = String(value); this.children = []; }
    append(...items) { this.children.push(...items); }
    prepend(...items) { this.children.unshift(...items); }
    replaceChildren(...items) { this._text = ''; this.children = [...items]; }
}

async function createHarness({ initialStorage = {}, onApi }) {
    const values = new Map(Object.entries(initialStorage));
    const nodes = new Map();
    const events = [];
    let generatedId = 0;
    const localStorage = {
        getItem(key) { return values.has(key) ? values.get(key) : null; },
        setItem(key, value) { values.set(key, String(value)); },
    };
    const document = {
        getElementById(id) {
            if (!nodes.has(id)) nodes.set(id, new Element(id));
            return nodes.get(id);
        },
        createElement(tag) { return new Element(tag); },
        createTextNode(text) { const node = new Element('#text'); node.textContent = text; return node; },
        dispatchEvent(event) { events.push(event); return true; },
    };
    const apiCalls = [];
    const api = async (url, options = {}) => {
        const call = { url, method: options.method || 'GET', body: options.body ? JSON.parse(options.body) : null };
        apiCalls.push(call);
        return onApi(call, apiCalls);
    };
    const context = vm.createContext({
        document,
        localStorage,
        Option: class Option { constructor(text, value) { this.textContent = text; this.value = value; } },
        URL,
        URLSearchParams,
        CustomEvent: class CustomEvent { constructor(type, init) { this.type = type; this.detail = init?.detail; } },
        history: { replaceState() {} },
        location: { origin: 'https://files.test', pathname: '/files', search: '' },
        crypto: { randomUUID: () => 'request-' + (++generatedId) },
        setTimeout: callback => { queueMicrotask(callback); return 0; },
        console,
    });
    const uploader = new vm.SyntheticModule(['api', 'list'], function () {
        this.setExport('api', api);
        this.setExport('list', async () => {});
    }, { context });
    const module = new vm.SourceTextModule(await source, { context, identifier: sourcePath });
    await module.link(specifier => {
        assert.equal(specifier, './uploader.js');
        return uploader;
    });
    await module.evaluate();
    return { module: module.namespace, values, nodes, events, apiCalls };
}

function connectionResponses(call) {
    if (call.url.startsWith('/storage-targets?')) return { templates: [], stores: [], connections: [], nextSkip: null };
    if (call.url.startsWith('/storage-connections?')) return [];
    throw new Error('Unexpected connection request: ' + call.url);
}

test('Connection managers get a separate scope list and can save only shared scope defaults', async () => {
    const personal = { id: '11111111-1111-1111-1111-111111111111', displayName: 'My drive', ownership: 1, state: 1 };
    const otherPersonal = { id: '22222222-2222-2222-2222-222222222222', displayName: 'Other drive', ownership: 1, state: 1 };
    const sharedPending = { id: '33333333-3333-3333-3333-333333333333', template: 'alipan', displayName: 'Pending team drive', ownership: 0, state: 0 };
    const sharedActive = { id: '44444444-4444-4444-4444-444444444444', template: 'alipan', displayName: 'Active team drive', ownership: 0, state: 1 };
    const sharedNeedsAuth = { id: '55555555-5555-5555-5555-555555555555', template: 'alipan', displayName: 'Expired team drive', ownership: 0, state: 2 };
    const sharedDisabled = { id: '66666666-6666-6666-6666-666666666666', template: 'alipan', displayName: 'Disabled team drive', ownership: 0, state: 3 };
    const sharedConnections = [sharedPending, sharedActive, sharedNeedsAuth, sharedDisabled];
    const harness = await createHarness({
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?')) return {
                templates: [{ name: 'alipan', provider: 'AliyunDrive' }], stores: ['local-v1'],
                connections: [personal, ...sharedConnections], nextSkip: null,
            };
            if (call.url.startsWith('/storage-connections?manage=true')) return [otherPersonal, ...sharedConnections];
            if (call.url.startsWith('/storage-connections?')) return [personal, ...sharedConnections];
            if (call.url === '/storage-connections/authorize' && call.method === 'POST') return {
                id: call.body.reconnectId, authorizationState: 'shared-state', authorizationUrl: 'https://auth.test/authorize',
            };
            if (call.url === '/storage-preference' && call.method === 'PUT') return null;
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    const ownership = new Element('select'); harness.nodes.set('drive-ownership', ownership);
    ownership.replaceChildren({ value: '1', hidden: false }, { value: '0', hidden: false });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' }, { manageConnections: true });

    assert.equal(harness.nodes.get('drive-management-panel').hidden, false);
    assert.equal(harness.nodes.get('scope-default-panel').hidden, false);
    assert.equal(ownership.children[1].hidden, false);
    assert.ok(harness.apiCalls.some(call => call.url.startsWith('/storage-connections?manage=true')));
    assert.equal(harness.nodes.get('drive-connections').children.length, 1, 'personal list excludes shared and other users');
    const managementRows = harness.nodes.get('managed-drive-connections').children;
    assert.equal(managementRows.length, 5);
    assert.match(managementRows[0].textContent, /Other drive · 个人连接/);
    assert.equal(managementRows[0].children.some(child => child.textContent === '重新授权'), false,
        'managers cannot reauthorize another owner from this list');
    for (const row of managementRows.slice(1))
        assert.equal(row.children.some(child => child.textContent === '重新授权'), true,
            'managers can reauthorize shared connections in every state');
    assert.deepEqual(harness.nodes.get('scope-default-target').children.map(option => option.value),
        ['', 'local-v1', ...sharedConnections.map(connection => connection.id.replaceAll('-', ''))]);
    assert.equal(harness.nodes.get('storage-target').children.some(option => option.value === personal.id.replaceAll('-', '')), true,
        'the owner can still choose their personal connection for their own uploads');
    assert.equal(harness.nodes.get('scope-default-target').children.some(option => option.value === personal.id.replaceAll('-', '')), false,
        'a personal connection is never offered as a scope default');

    await managementRows[1].children.find(child => child.textContent === '重新授权').onclick();
    const reauthorization = harness.apiCalls.find(call => call.url === '/storage-connections/authorize' && call.method === 'POST');
    assert.equal(reauthorization.body.reconnectId, sharedPending.id);
    assert.equal(reauthorization.body.ownership, 0);

    harness.nodes.set('container', new Element('select'));
    harness.nodes.get('container').value = 'attachments';
    harness.nodes.get('scope-default-target').value = sharedActive.id.replaceAll('-', '');
    await harness.nodes.get('save-scope-default').onclick();
    const saved = harness.apiCalls.find(call => call.url === '/storage-preference' && call.method === 'PUT');
    assert.deepEqual(saved.body, { container: 'attachments', targetId: sharedActive.id.replaceAll('-', ''), scopeDefault: true });
});

test('Users see only their personal connection actions and cannot configure scope defaults', async () => {
    const personal = { id: '11111111-1111-1111-1111-111111111111', displayName: 'My drive', ownership: 1, state: 1 };
    const shared = { id: '33333333-3333-3333-3333-333333333333', displayName: 'Team drive', ownership: 0, state: 1 };
    const harness = await createHarness({
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?')) return {
                templates: [{ name: 'alipan', provider: 'AliyunDrive' }, { name: '115', provider: 'Pan115' }],
                stores: ['local-v1'], connections: [personal, shared], nextSkip: null,
            };
            if (call.url.startsWith('/storage-connections?')) return [personal, shared];
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    const ownership = new Element('select'); harness.nodes.set('drive-ownership', ownership);
    ownership.replaceChildren({ value: '1', hidden: false }, { value: '0', hidden: false });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });

    assert.equal(harness.nodes.get('drive-management-panel').hidden, true);
    assert.equal(harness.nodes.get('scope-default-panel').hidden, true);
    assert.equal(ownership.children[1].hidden, true);
    assert.equal(harness.apiCalls.some(call => call.url.includes('manage=true')), false);
    const rows = harness.nodes.get('drive-connections').children;
    assert.equal(rows.length, 1);
    assert.equal(rows[0].children.some(child => child.textContent === '重新授权'), true);
    assert.deepEqual(harness.nodes.get('scope-default-target').children.map(option => option.value), ['', 'local-v1', shared.id.replaceAll('-', '')]);
});

function savedCopies(values, ownerKey) {
    return JSON.parse(values.get(ownerKey + ':copies') || '[]');
}

function status(id, state, copyFileId = 'public-copy') { return { id, state, copyFileId }; }

test('Cancelled and Failed requests use a new idempotency key on retry', async t => {
    for (const [oldState, label] of [[3, 'Cancelled'], [4, 'Failed']]) {
        await t.test(label, async () => {
            const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
            const oldRequest = { requestId: 'request-old', id: 'copy-old', sourceId: 'source-file', businessType: 'user-info', businessId: 'self' };
            let newRequestId;
            let newStatusReads = 0;
            const harness = await createHarness({
                initialStorage: { [ownerKey + ':copies']: JSON.stringify([oldRequest]) },
                onApi: async call => {
                    if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
                    if (call.url === '/binding-copies/copy-old') return status('copy-old', oldState);
                    if (call.url === '/source-file/binding-copies' && call.method === 'POST') {
                        newRequestId = call.body.requestId;
                        return status('copy-new', 0);
                    }
                    if (call.url === '/binding-copies/copy-new') return status('copy-new', newStatusReads++ === 0 ? 1 : 2, 'public-new');
                    if (call.url === '/binding-copies/copy-new/bind' && call.method === 'POST') return { fileId: 'public-new' };
                    if (call.url === '/public-new') return { id: 'public-new', fileName: 'copy' };
                    throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
                },
            });
            await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
            const terminalRow = harness.nodes.get('binding-copies').children[0];
            assert.match(terminalRow.textContent, new RegExp(oldState === 3 ? '已取消' : '失败'));
            const retry = terminalRow.children.find(child => child.textContent === '重新复制并关联');
            assert.ok(retry, 'terminal request exposes an explicit retry action');
            await retry.onclick();
            const saved = savedCopies(harness.values, ownerKey);
            assert.equal(saved.length, 1);
            assert.equal(saved[0].requestId, newRequestId);
            assert.notEqual(saved[0].requestId, 'request-old');
            assert.equal(saved[0].id, 'copy-new');
            assert.equal(harness.apiCalls.filter(call => call.method === 'POST' && call.url.endsWith('/binding-copies')).length, 1);
            assert.ok(harness.apiCalls.some(call => call.url === '/binding-copies/copy-new/bind'));
            assert.ok(harness.events.some(event => event.type === 'bing-file-selected' && event.detail.id === 'public-new'));
        });
    }
});

test('Pending and Copied requests resume with the saved server id', async t => {
    for (const [states, label] of [[[0, 0, 1, 2], 'Pending'], [[1, 1, 2], 'Copied']]) {
        await t.test(label, async () => {
            const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
            const request = { requestId: 'request-existing', id: 'copy-existing', sourceId: 'source-file', businessType: 'user-info', businessId: 'self' };
            const queue = [...states];
            const harness = await createHarness({
                initialStorage: { [ownerKey + ':copies']: JSON.stringify([request]) },
                onApi: async call => {
                    if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
                    if (call.url === '/binding-copies/copy-existing') return status('copy-existing', queue.shift() ?? states.at(-1), 'public-existing');
                    if (call.url === '/binding-copies/copy-existing/bind' && call.method === 'POST') return { fileId: 'public-existing' };
                    if (call.url === '/public-existing') return { id: 'public-existing' };
                    throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
                },
            });
            await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
            await harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
            assert.equal(savedCopies(harness.values, ownerKey)[0].requestId, 'request-existing');
            assert.equal(savedCopies(harness.values, ownerKey)[0].id, 'copy-existing');
            assert.equal(harness.apiCalls.filter(call => call.method === 'POST' && call.url.endsWith('/binding-copies')).length, 0);
            assert.equal(harness.apiCalls.filter(call => call.url === '/binding-copies/copy-existing/bind').length, 1);
        });
    }
});

test('Refreshing restores a Pending row and its Continue action reuses the saved request', async () => {
    const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
    const request = { requestId: 'request-refresh', id: 'copy-refresh', sourceId: 'source-file', businessType: 'user-info', businessId: 'self' };
    const queue = [0, 0, 1, 2];
    const harness = await createHarness({
        initialStorage: { [ownerKey + ':copies']: JSON.stringify([request]) },
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            if (call.url === '/binding-copies/copy-refresh') return status('copy-refresh', queue.shift() ?? 1, 'public-refresh');
            if (call.url === '/binding-copies/copy-refresh/bind' && call.method === 'POST') return { fileId: 'public-refresh' };
            if (call.url === '/public-refresh') return { id: 'public-refresh' };
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    const panel = harness.nodes.get('binding-copies');
    const row = panel.children[0];
    assert.match(row.textContent, /复制中/);
    const resume = row.children.find(child => child.textContent === '继续并关联');
    assert.ok(resume, 'restored Pending request exposes a resume action');
    await resume.onclick();
    assert.equal(savedCopies(harness.values, ownerKey)[0].requestId, 'request-refresh');
    assert.equal(harness.apiCalls.filter(call => call.method === 'POST' && call.url.endsWith('/binding-copies')).length, 0);
    assert.ok(harness.events.some(event => event.type === 'bing-file-selected' && event.detail.id === 'public-refresh'));
});

test('Refreshing exposes a recovery action for a local request whose create response was lost', async () => {
    const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
    const requestId = 'request-lost-response';
    const request = { requestId, sourceId: 'source-file', businessType: 'user-info', businessId: 'self' };
    let createCount = 0;
    const harness = await createHarness({
        initialStorage: { [ownerKey + ':copies']: JSON.stringify([request]) },
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            if (call.url === '/source-file/binding-copies' && call.method === 'POST') {
                createCount++;
                assert.equal(call.body.requestId, requestId);
                if (createCount === 1) throw new Error('response lost after server accepted the request');
                return status('copy-recovered', 1, 'public-recovered');
            }
            if (call.url === '/binding-copies/copy-recovered/bind' && call.method === 'POST') return { fileId: 'public-recovered' };
            if (call.url === '/public-recovered') return { id: 'public-recovered' };
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });

    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    let row = harness.nodes.get('binding-copies').children[0];
    assert.match(row.textContent, /请求待恢复/);
    assert.equal(harness.apiCalls.filter(call => call.method === 'POST' && call.url === '/source-file/binding-copies').length, 0,
        'refreshing a local intent does not create a request automatically');
    const recovery = row.children.find(child => child.textContent === '恢复复制请求');
    assert.ok(recovery, 'local request without a server id exposes an explicit recovery action');

    await assert.rejects(harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self'), /response lost/);
    assert.equal(createCount, 1);
    assert.equal(savedCopies(harness.values, ownerKey)[0].id, undefined);
    await harness.nodes.get('refresh-copies').onclick();

    row = harness.nodes.get('binding-copies').children[0];
    await row.children.find(child => child.textContent === '恢复复制请求').onclick();
    const saved = savedCopies(harness.values, ownerKey)[0];
    assert.equal(createCount, 2);
    assert.equal(saved.requestId, requestId);
    assert.equal(saved.id, 'copy-recovered');
    assert.equal(harness.apiCalls.filter(call => call.method === 'POST' && call.url === '/source-file/binding-copies').length, 2);
    assert.ok(harness.events.some(event => event.type === 'bing-file-selected' && event.detail.id === 'public-recovered'));
});

test('Bound replay selects the existing copy without another create or bind', async () => {
    const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
    const request = { requestId: 'request-bound', id: 'copy-bound', sourceId: 'source-file', businessType: 'user-info', businessId: 'self' };
    const harness = await createHarness({
        initialStorage: { [ownerKey + ':copies']: JSON.stringify([request]) },
        onApi: async call => {
            if (call.url === '/binding-copies/copy-bound') return status('copy-bound', 2, 'public-bound');
            if (call.url === '/public-bound') return { id: 'public-bound', fileName: 'existing copy' };
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    const selected = harness.nodes.get('binding-copies').children[0].children.find(child => child.textContent === '选择已有副本');
    assert.ok(selected, 'Bound row exposes the already published copy');
    await selected.onclick();
    await harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
    assert.ok(harness.events.filter(event => event.type === 'bing-file-selected').length >= 2);
    assert.ok(harness.events.every(event => event.detail.id === 'public-bound'));
    assert.equal(harness.apiCalls.some(call => call.method === 'POST'), false);
});

test('Concurrent clicks share one create request and one bind', async () => {
    let releaseCreate;
    const createResponse = new Promise(resolve => { releaseCreate = resolve; });
    const harness = await createHarness({
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            if (call.url === '/source-file/binding-copies' && call.method === 'POST') return createResponse;
            if (call.url === '/binding-copies/copy-concurrent/bind' && call.method === 'POST') return { fileId: 'public-concurrent' };
            if (call.url === '/binding-copies/copy-concurrent') return status('copy-concurrent', 2, 'public-concurrent');
            if (call.url === '/public-concurrent') return { id: 'public-concurrent' };
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    const first = harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
    const second = harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
    assert.strictEqual(second, first);
    await new Promise(resolve => setImmediate(resolve));
    const creates = harness.apiCalls.filter(call => call.method === 'POST' && call.url === '/source-file/binding-copies');
    assert.equal(creates.length, 1);
    releaseCreate(status('copy-concurrent', 1, 'public-concurrent'));
    await Promise.all([first, second]);
    assert.equal(harness.apiCalls.filter(call => call.url === '/binding-copies/copy-concurrent/bind').length, 1);
});

test('Different copy requests retain both records when create responses arrive in reverse order', async () => {
    let releaseCreateA;
    let releaseCreateB;
    const createA = new Promise(resolve => { releaseCreateA = resolve; });
    const createB = new Promise(resolve => { releaseCreateB = resolve; });
    const ownerKey = 'bing-file-drives:["tenant-a","user-a"]';
    const harness = await createHarness({
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            if (call.url === '/source-a/binding-copies' && call.method === 'POST') return createA;
            if (call.url === '/source-b/binding-copies' && call.method === 'POST') return createB;
            if (call.url === '/binding-copies/copy-a/bind' && call.method === 'POST') return { fileId: 'public-a' };
            if (call.url === '/binding-copies/copy-b/bind' && call.method === 'POST') return { fileId: 'public-b' };
            if (call.url === '/binding-copies/copy-a') return status('copy-a', 2, 'public-a');
            if (call.url === '/binding-copies/copy-b') return status('copy-b', 2, 'public-b');
            if (call.url === '/public-a') return { id: 'public-a' };
            if (call.url === '/public-b') return { id: 'public-b' };
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    const operationA = harness.module.copyAndBind({ id: 'source-a' }, 'user-info', 'self');
    const operationB = harness.module.copyAndBind({ id: 'source-b' }, 'project', '42');
    const createCalls = harness.apiCalls.filter(call => call.method === 'POST' && call.url.endsWith('/binding-copies'));
    assert.equal(createCalls.length, 2);

    releaseCreateB(status('copy-b', 1, 'public-b'));
    await operationB;
    let saved = savedCopies(harness.values, ownerKey);
    assert.equal(saved.length, 2);
    assert.equal(saved.find(item => item.sourceId === 'source-b').id, 'copy-b');
    assert.equal(saved.find(item => item.sourceId === 'source-a').id, undefined);

    releaseCreateA(status('copy-a', 1, 'public-a'));
    await operationA;
    saved = savedCopies(harness.values, ownerKey);
    assert.equal(saved.length, 2);
    assert.equal(saved.find(item => item.sourceId === 'source-a').id, 'copy-a');
    assert.equal(saved.find(item => item.sourceId === 'source-b').id, 'copy-b');
    assert.equal(saved.find(item => item.sourceId === 'source-a').businessId, 'self');
    assert.equal(saved.find(item => item.sourceId === 'source-b').businessId, '42');
});

test('Identity switch keeps saved and pending copy requests in their owner keys', async () => {
    let releaseFirstCreate;
    let createCount = 0;
    const firstCreate = new Promise(resolve => { releaseFirstCreate = resolve; });
    const harness = await createHarness({
        onApi: async call => {
            if (call.url.startsWith('/storage-targets?') || call.url.startsWith('/storage-connections?')) return connectionResponses(call);
            if (call.url === '/source-file/binding-copies' && call.method === 'POST') {
                createCount++;
                return createCount === 1 ? firstCreate : status('copy-user-b', 1, 'public-user-b');
            }
            if (call.url === '/binding-copies/copy-user-b/bind' && call.method === 'POST') return { fileId: 'public-user-b' };
            if (call.url === '/binding-copies/copy-user-b') return status('copy-user-b', 2, 'public-user-b');
            if (call.url === '/public-user-b') return { id: 'public-user-b' };
            throw new Error('Unexpected API request: ' + call.method + ' ' + call.url);
        },
    });
    await harness.module.connectDrives({ tenantId: 'tenant-a', userId: 'user-a' });
    const oldIdentityFlow = harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
    await new Promise(resolve => setImmediate(resolve));
    const firstCreateCall = harness.apiCalls.find(call => call.url === '/source-file/binding-copies');
    assert.ok(firstCreateCall);

    await harness.module.connectDrives({ tenantId: 'tenant-b', userId: 'user-b' });
    await harness.module.copyAndBind({ id: 'source-file' }, 'user-info', 'self');
    releaseFirstCreate(status('copy-user-a', 1, 'public-user-a'));
    await oldIdentityFlow;

    const ownerA = 'bing-file-drives:["tenant-a","user-a"]';
    const ownerB = 'bing-file-drives:["tenant-b","user-b"]';
    const requestA = savedCopies(harness.values, ownerA)[0];
    const requestB = savedCopies(harness.values, ownerB)[0];
    assert.ok(requestA);
    assert.ok(requestB);
    assert.equal(requestA.id, undefined, 'stale response does not write an id after identity changes');
    assert.equal(requestB.id, 'copy-user-b');
    assert.notEqual(requestA.requestId, requestB.requestId);
    assert.equal(harness.events.some(event => event.type === 'bing-file-selected' && event.detail.id === 'public-user-a'), false);
});
