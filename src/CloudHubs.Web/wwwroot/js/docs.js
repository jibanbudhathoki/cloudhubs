// CloudHubs Documentation Module Scripts (Editor, Detail, Index, ApiHub)

// ----------------------------------------------------
// Markdown Parser
// ----------------------------------------------------
function convertMarkdownToHtml(md) {
    if (!md) return '';
    var trimmed = md.trim();
    if (trimmed.startsWith('<') && (trimmed.endsWith('>') || trimmed.includes('</'))) {
        return md;
    }
    return md
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/^### (.*$)/gim, '<h3>$1</h3>')
        .replace(/^## (.*$)/gim, '<h2>$1</h2>')
        .replace(/^# (.*$)/gim, '<h1>$1</h1>')
        .replace(/```([a-z]*)\n([\s\S]*?)```/gim, '<pre class="ql-syntax">$2</pre>')
        .replace(/`([^`]+)`/gim, '<code>$1</code>')
        .replace(/\*\*([^*]+)\*\*/gim, '<strong>$1</strong>')
        .replace(/\*([^*]+)\*/gim, '<em>$1</em>')
        .replace(/^> (.*$)/gim, '<blockquote>$1</blockquote>')
        .replace(/^\- (.*$)/gim, '<li>$1</li>')
        .replace(/\n\n/gim, '<p><br></p>')
        .replace(/\n/gim, '<br>');
}

function renderMarkdown(md) {
    if (!md) return '';
    return md
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/^### (.*$)/gim, '<h3>$1</h3>')
        .replace(/^## (.*$)/gim, '<h2>$1</h2>')
        .replace(/^# (.*$)/gim, '<h1>$1</h1>')
        .replace(/```([a-z]*)\n([\s\S]*?)```/gim, '<pre><code>$2</code></pre>')
        .replace(/`([^`]+)`/gim, '<code>$1</code>')
        .replace(/\*\*([^*]+)\*\*/gim, '<strong>$1</strong>')
        .replace(/\*([^*]+)\*/gim, '<em>$1</em>')
        .replace(/^\- (.*$)/gim, '<li>$1</li>')
        .replace(/\n\n/gim, '<p></p>');
}

// ----------------------------------------------------
// Quill Editor Integration (Editor.cshtml)
// ----------------------------------------------------
var quill = null;

function initQuillEditor() {
    var editorEl = document.getElementById('quill-editor');
    if (!editorEl || typeof Quill === 'undefined') return;

    quill = new Quill('#quill-editor', {
        theme: 'snow',
        modules: {
            toolbar: '#quill-custom-toolbar'
        },
        placeholder: 'Write your API documentation, parameters, and endpoints here...'
    });

    var initialContent = document.getElementById('raw-initial-content')?.value || '';
    if (initialContent) {
        var html = convertMarkdownToHtml(initialContent);
        quill.clipboard.dangerouslyPasteHTML(html);
    }

    var editorForm = document.getElementById('doc-editor-form');
    if (editorForm) {
        editorForm.addEventListener('submit', function () {
            var contentInput = document.getElementById('quill-content-input');
            if (contentInput && quill) {
                contentInput.value = quill.root.innerHTML;
            }
        });
    }
}

function insertEndpointQuill(method) {
    if (!quill) return;
    var range = quill.getSelection(true) || { index: quill.getLength() };
    var color = method === 'GET' ? '#1d4ed8' : (method === 'POST' ? '#15803d' : (method === 'PUT' ? '#b45309' : '#b91c1c'));
    var bg = method === 'GET' ? '#eff6ff' : (method === 'POST' ? '#f0fdf4' : (method === 'PUT' ? '#fef3c7' : '#fee2e2'));

    var html = '<p><br></p>' +
        '<h3><span style="background-color: ' + bg + '; color: ' + color + '; padding: 3px 8px; border-radius: 4px; font-family: monospace; font-size: 13px; font-weight: bold;">' + method + '</span> <code>/api/v1/resource</code></h3>' +
        '<p><strong>Description:</strong> Explain what this endpoint returns or accepts.</p>' +
        '<p><strong>Headers:</strong></p>' +
        '<ul><li><code>Content-Type: application/json</code></li></ul>' +
        (method !== 'GET' ? '<p><strong>Request Body (JSON):</strong></p><pre class="ql-syntax">{\n  "name": "Sample",\n  "enabled": true\n}</pre>' : '') +
        '<p><strong>Response (200 OK):</strong></p>' +
        '<pre class="ql-syntax">{\n  "status": "success",\n  "data": []\n}</pre><p><br></p>';

    quill.clipboard.dangerouslyPasteHTML(range.index, html);
}

function insertTableQuill() {
    if (!quill) return;
    var range = quill.getSelection(true) || { index: quill.getLength() };
    var html = '<p><br></p>' +
        '<p><strong>Parameters:</strong></p>' +
        '<ul>' +
        '<li><code>id</code> <em>(string, required)</em> — Unique identifier</li>' +
        '<li><code>page</code> <em>(integer, optional)</em> — Page number for pagination</li>' +
        '<li><code>limit</code> <em>(integer, optional)</em> — Number of items per page</li>' +
        '</ul><p><br></p>';
    quill.clipboard.dangerouslyPasteHTML(range.index, html);
}

function insertCalloutQuill() {
    if (!quill) return;
    var range = quill.getSelection(true) || { index: quill.getLength() };
    var html = '<blockquote><strong>Note:</strong> Add important integration tips, rate limits, or requirements here.</blockquote><p><br></p>';
    quill.clipboard.dangerouslyPasteHTML(range.index, html);
}

function handlePostmanFile(input) {
    if (input.files && input.files[0]) {
        var file = input.files[0];
        var lbl = document.getElementById('postman-file-label');
        if (lbl) lbl.innerText = '✓ Loaded: ' + file.name + ' (' + Math.round(file.size / 1024) + ' KB)';
        var reader = new FileReader();
        reader.onload = function (e) {
            var content = e.target.result;
            var ta = document.getElementById('postman-textarea');
            if (ta) {
                try {
                    var parsed = JSON.parse(content);
                    ta.value = JSON.stringify(parsed, null, 2);
                } catch (err) {
                    ta.value = content;
                }
            }
        };
        reader.readAsText(file);
    }
}

function formatPostmanJson() {
    var ta = document.getElementById('postman-textarea');
    if (ta && ta.value) {
        try {
            var parsed = JSON.parse(ta.value);
            ta.value = JSON.stringify(parsed, null, 2);
        } catch (e) {
            alert('Invalid JSON: ' + e.message);
        }
    }
}

function handleEnvFile(input) {
    if (input.files && input.files[0]) {
        var file = input.files[0];
        var lbl = document.getElementById('env-file-label');
        if (lbl) lbl.innerText = '✓ Loaded: ' + file.name + ' (' + file.size + ' bytes)';
        var reader = new FileReader();
        reader.onload = function (e) {
            var ta = document.getElementById('env-textarea');
            if (ta) ta.value = e.target.result;
        };
        reader.readAsText(file);
    }
}

function toggleAccessList(isAll) {
    var card = document.getElementById('specific-members-card');
    if (card) {
        card.style.display = isAll ? 'none' : 'block';
    }
}

// ----------------------------------------------------
// Detail View & Snippets (Detail.cshtml & ApiHub.cshtml)
// ----------------------------------------------------
function getDocMeta() {
    var el = document.getElementById('doc-metadata');
    return {
        liveKey: el?.getAttribute('data-live-key') || '',
        testKey: el?.getAttribute('data-test-key') || '',
        baseUrl: el?.getAttribute('data-base-url') || 'https://api.cloudhubs.com/v1'
    };
}

function switchSnippetEnv(env) {
    var meta = getDocMeta();
    var isLive = env === 'live';
    var key = isLive ? meta.liveKey : meta.testKey;
    var btnLive = document.getElementById('btn-env-live');
    var btnTest = document.getElementById('btn-env-test');
    if (btnLive && btnTest) {
        if (isLive) {
            btnLive.classList.add('active');
            btnTest.classList.remove('active');
        } else {
            btnTest.classList.add('active');
            btnLive.classList.remove('active');
        }
    }

    var curlPre = document.getElementById('snippet-curl');
    if (curlPre) {
        curlPre.innerText = 'curl -X GET "' + meta.baseUrl + '" \\\n  -H "Accept: application/json"' +
            (key ? ' \\\n  -H "Authorization: Bearer ' + key + '"' : '');
    }

    var jsPre = document.getElementById('snippet-js');
    if (jsPre) {
        jsPre.innerText = 'const response = await fetch("' + meta.baseUrl + '", {\n  headers: {\n    "Accept": "application/json"' +
            (key ? ',\n    "Authorization": "Bearer ' + key + '"' : '') + '\n  }\n});\nconst data = await response.json();\nconsole.log(data);';
    }

    var pyPre = document.getElementById('snippet-python');
    if (pyPre) {
        pyPre.innerText = 'import requests\n\nurl = "' + meta.baseUrl + '"\nheaders = {\n    "Accept": "application/json"' +
            (key ? ',\n    "Authorization": "Bearer ' + key + '"' : '') + '\n}\n\nresponse = requests.get(url, headers=headers)\nprint(response.json())';
    }
}

function copyText(id, btn) {
    var el = document.getElementById(id);
    if (el && el.value) {
        navigator.clipboard.writeText(el.value).then(function () {
            var orig = btn.innerText; btn.innerText = '✓'; setTimeout(() => btn.innerText = orig, 1200);
        });
    }
}

function copyValue(id, btn) {
    copyText(id, btn);
}

function copyPassword(id, btn) {
    copyText(id, btn);
}

function togglePasswordVisibility(id, btn) {
    var el = document.getElementById(id);
    if (el) {
        if (el.type === 'password') {
            el.type = 'text';
            btn.innerText = '🙈';
        } else {
            el.type = 'password';
            btn.innerText = '👁';
        }
    }
}

function toggleKeyVisibility(id, btn) {
    togglePasswordVisibility(id, btn);
}

function copySnippet(id, btn) {
    var el = document.getElementById(id);
    if (el) {
        navigator.clipboard.writeText(el.innerText).then(function () {
            var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1200);
        });
    }
}

function copyMarkdownContent(btn) {
    var val = document.getElementById('raw-markdown-source')?.value;
    if (val) {
        navigator.clipboard.writeText(val).then(function () {
            var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1200);
        });
    }
}

function copyPostmanJson(btn) {
    var val = document.getElementById('doc-postman-raw')?.innerText;
    if (val) {
        navigator.clipboard.writeText(val).then(function () {
            var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1200);
        });
    }
}

function copyEnvText(btn) {
    var val = document.getElementById('doc-env-raw')?.innerText;
    if (val) {
        navigator.clipboard.writeText(val).then(function () {
            var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1200);
        });
    }
}

function parsePostmanRequests() {
    var listEl = document.getElementById('doc-postman-endpoints');
    if (!listEl) return;
    var jsonText = document.getElementById('doc-postman-raw')?.innerText;
    if (!jsonText) return;
    try {
        var col = JSON.parse(jsonText);
        var items = col.item || [];
        var requests = [];
        function extract(arr) {
            arr.forEach(function (i) {
                if (i.request) { requests.push(i); }
                else if (i.item) { extract(i.item); }
            });
        }
        extract(items);
        if (requests.length === 0) {
            listEl.innerHTML = '<div class="list-group-item text-muted small">No request endpoints found inside collection.</div>';
            return;
        }
        var pill = document.getElementById('postman-count-pill');
        if (pill) pill.innerText = requests.length + ' endpoints';

        var html = '';
        requests.forEach(function (r) {
            var req = r.request;
            var method = (req.method || 'GET').toUpperCase();
            var url = typeof req.url === 'string' ? req.url : (req.url?.raw || req.url?.path?.join('/') || '');
            var badgeClass = 'badge-' + method.toLowerCase();
            html += '<div class="list-group-item d-flex align-items-center justify-content-between gap-3 py-2 px-3 border rounded mb-2">' +
                '<div class="d-flex align-items-center gap-2">' +
                '<span class="postman-req-badge ' + badgeClass + '">' + method + '</span>' +
                '<strong>' + (r.name || url) + '</strong>' +
                '</div>' +
                '<code class="small text-muted">' + url + '</code>' +
                '</div>';
        });
        listEl.innerHTML = html;
    } catch (e) {
        listEl.innerHTML = '<div class="list-group-item text-danger small">Could not parse requests from JSON: ' + e.message + '</div>';
    }
}

var isEnvMasked = true;
function parseDocEnvTable() {
    var tbody = document.getElementById('doc-env-table-body');
    if (!tbody) return;
    var raw = document.getElementById('doc-env-raw')?.innerText || '';
    var lines = raw.split('\n');
    var rows = '';
    lines.forEach(function (line) {
        line = line.trim();
        if (!line || line.startsWith('#')) return;
        var idx = line.indexOf('=');
        if (idx === -1) return;
        var key = line.substring(0, idx).trim();
        var val = line.substring(idx + 1).trim();
        var isSecret = /KEY|SECRET|PASSWORD|TOKEN|AUTH|CREDENTIAL|PRIVATE/i.test(key);

        rows += '<tr>' +
            '<td class="font-monospace fw-bold text-success">' + key + '</td>' +
            '<td><span class="' + (isSecret ? 'env-val env-val-secret masked' : 'env-val') + '" data-raw="' + val.replace(/"/g, '&quot;') + '">' + (isSecret ? '••••••••••••••••' : val) + '</span></td>' +
            '<td style="text-align: right;"><button type="button" class="btn btn-outline-secondary btn-sm py-0" style="font-size: 11px;" onclick="copySingleEnv(\'' + val.replace(/'/g, "\\'") + '\', this)">Copy</button></td>' +
            '</tr>';
    });
    tbody.innerHTML = rows || '<tr><td colspan="3" class="text-center text-muted small py-3">No valid KEY=VALUE lines found.</td></tr>';
}

function copySingleEnv(val, btn) {
    navigator.clipboard.writeText(val).then(function () {
        var orig = btn.innerText; btn.innerText = '✓'; setTimeout(() => btn.innerText = orig, 1200);
    });
}

function toggleDocEnvMasking(btn) {
    isEnvMasked = !isEnvMasked;
    document.querySelectorAll('#doc-env-table-body .env-val-secret').forEach(function (el) {
        if (isEnvMasked) {
            el.classList.add('masked');
            el.innerText = '••••••••••••••••';
        } else {
            el.classList.remove('masked');
            el.innerText = el.getAttribute('data-raw');
        }
    });
    if (btn) btn.innerText = isEnvMasked ? '👁 Show Secrets' : '🙈 Hide Secrets';
}

// ----------------------------------------------------
// Page Initialization & Event Handlers
// ----------------------------------------------------
document.addEventListener('DOMContentLoaded', function () {
    // 1. Init Quill if on editor page
    initQuillEditor();

    // 2. Render Markdown in Detail view
    var mdSource = document.getElementById('raw-markdown-source')?.value || '';
    var renderEl = document.getElementById('rendered-markdown-text');
    if (renderEl && mdSource) {
        var trimmed = mdSource.trim();
        if (trimmed.startsWith('<') && (trimmed.endsWith('>') || trimmed.includes('</'))) {
            renderEl.innerHTML = mdSource;
        } else {
            renderEl.innerHTML = renderMarkdown(mdSource);
        }
    }

    // 3. Parse Postman and Env in Detail view
    parsePostmanRequests();
    parseDocEnvTable();

    // 4. Docs Index Search & Filter
    var searchInput = document.getElementById('search-docs');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            var q = this.value.toLowerCase().trim();
            var visible = 0;
            document.querySelectorAll('#docs-table .doc-row').forEach(function (row) {
                var name = row.getAttribute('data-name') || '';
                var cat = row.getAttribute('data-cat') || '';
                var match = !q || name.includes(q) || cat.includes(q);
                row.style.display = match ? '' : 'none';
                if (match) visible++;
            });
            var countEl = document.getElementById('docs-count');
            if (countEl) countEl.innerText = visible;
        });
    }
});

