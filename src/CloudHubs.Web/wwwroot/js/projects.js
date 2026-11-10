// CloudHubs Projects Module Scripts (Projects List, Create Project, Project Resources)

// ----------------------------------------------------
// Project Creation (CreateProject.cshtml)
// ----------------------------------------------------
function initCreateProject() {
    var nameInput = document.getElementById('project-name');
    var slugInput = document.getElementById('project-slug');
    var rootPathInput = document.getElementById('project-rootpath');
    var metaEl = document.getElementById('project-create-meta');
    var orgSlug = metaEl?.getAttribute('data-org-slug') || '';

    if (!nameInput || !slugInput) return;

    var userEditedSlug = slugInput.value.trim().length > 0;

    slugInput.addEventListener('input', function () {
        userEditedSlug = true;
        updateRootPath();
    });

    nameInput.addEventListener('input', function () {
        if (!userEditedSlug) {
            var slug = nameInput.value.toLowerCase().trim()
                .replace(/[^a-z0-9_-]+/g, '-')
                .replace(/^-+|-+$/g, '');
            slugInput.value = slug;
            updateRootPath();
        }
    });

    function updateRootPath() {
        if (!rootPathInput) return;
        var currentSlug = slugInput.value.trim() || 'project-slug';
        if (!rootPathInput.dataset.custom) {
            rootPathInput.placeholder = 'projects/' + orgSlug + '/' + currentSlug;
        }
    }

    if (rootPathInput) {
        rootPathInput.addEventListener('input', function () {
            rootPathInput.dataset.custom = rootPathInput.value.trim().length > 0 ? 'true' : '';
        });
    }

    var templateCards = document.querySelectorAll('.template-card');
    templateCards.forEach(function (card) {
        card.addEventListener('click', function () {
            templateCards.forEach(function (c) { c.classList.remove('active'); });
            card.classList.add('active');
        });
    });
}

// ----------------------------------------------------
// Project Resources (ProjectResources.cshtml)
// ----------------------------------------------------
function copyShareUrl() {
    navigator.clipboard.writeText(window.location.href).then(function () {
        var btn1 = document.getElementById('copy-share-btn');
        var btn2 = document.getElementById('share-link-btn');
        if (btn1) { var orig1 = btn1.innerText; btn1.innerText = '✓ Copied!'; setTimeout(() => btn1.innerText = orig1, 1500); }
        if (btn2) { var orig2 = btn2.innerText; btn2.innerText = '✓ Copied!'; setTimeout(() => btn2.innerText = orig2, 1500); }
    });
}

function copyApiKey() {
    var val = document.getElementById('org-api-key-input')?.value;
    if (val) {
        navigator.clipboard.writeText(val).then(function () {
            var btn = document.getElementById('api-key-btn');
            if (btn) {
                var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1500);
            }
        });
    }
}

function copyCurlSnippet() {
    var val = document.getElementById('curl-snippet')?.innerText;
    if (val) {
        navigator.clipboard.writeText(val).then(function () {
            var btn = document.getElementById('curl-copy-btn');
            if (btn) {
                var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1500);
            }
        });
    }
}

function toggleDocsEditor() {
    var view = document.getElementById('docs-view');
    var form = document.getElementById('docs-form');
    var btn = document.getElementById('toggle-docs-btn');
    if (!form || !view) return;
    if (form.style.display === 'none') {
        form.style.display = 'block';
        view.style.display = 'none';
        if (btn) btn.innerText = '👁 View Documentation';
    } else {
        form.style.display = 'none';
        view.style.display = 'block';
        if (btn) btn.innerText = '✏️ Edit Documentation';
    }
}

function copyDocumentation() {
    var text = document.getElementById('doc-textarea')?.value;
    if (text) {
        navigator.clipboard.writeText(text).then(function () {
            var btn = document.getElementById('copy-docs-btn');
            if (btn) {
                var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1500);
            }
        });
    }
}

function renderProjectMarkdown(md) {
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

function togglePostmanEditor() {
    var view = document.getElementById('postman-view');
    var form = document.getElementById('postman-form');
    var btn = document.getElementById('toggle-postman-btn');
    if (!form || !view) return;
    if (form.style.display === 'none') {
        form.style.display = 'block';
        view.style.display = 'none';
        if (btn) btn.innerText = '👁 View Requests';
    } else {
        form.style.display = 'none';
        view.style.display = 'block';
        if (btn) btn.innerText = '✏️ Edit Collection';
    }
}

function copyPostmanJson() {
    var text = document.getElementById('postman-textarea')?.value;
    if (text) {
        navigator.clipboard.writeText(text).then(function () {
            var btn = document.getElementById('copy-postman-btn');
            if (btn) {
                var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1500);
            }
        });
    }
}

function formatPostmanTextarea() {
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

function parseProjectPostmanRequests() {
    var listEl = document.getElementById('postman-requests-list');
    if (!listEl) return;
    var jsonText = document.getElementById('postman-textarea')?.value;
    if (!jsonText) return;
    try {
        var col = JSON.parse(jsonText);
        var items = col.item || [];
        var requests = [];

        function extract(arr) {
            arr.forEach(function (i) {
                if (i.request) {
                    requests.push(i);
                } else if (i.item) {
                    extract(i.item);
                }
            });
        }
        extract(items);

        if (requests.length === 0) {
            listEl.innerHTML = '<div class="list-group-item text-muted small">No request endpoints found inside collection.</div>';
            return;
        }

        var html = '';
        requests.forEach(function (r) {
            var req = r.request;
            var method = (req.method || 'GET').toUpperCase();
            var url = typeof req.url === 'string' ? req.url : (req.url?.raw || req.url?.path?.join('/') || '');
            var badgeClass = 'badge-' + method.toLowerCase();
            html += '<div class="list-group-item d-flex align-items-center justify-content-between gap-3 py-2">' +
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

function toggleEnvEditor() {
    var view = document.getElementById('env-view');
    var form = document.getElementById('env-form');
    var btn = document.getElementById('toggle-env-btn');
    if (!form || !view) return;
    if (form.style.display === 'none') {
        form.style.display = 'block';
        view.style.display = 'none';
        if (btn) btn.innerText = '👁 View Table';
    } else {
        form.style.display = 'none';
        view.style.display = 'block';
        if (btn) btn.innerText = '✏️ Edit .env';
    }
}

function copyEnvText() {
    var text = document.getElementById('env-textarea')?.value;
    if (text) {
        navigator.clipboard.writeText(text).then(function () {
            var btn = document.getElementById('copy-env-btn');
            if (btn) {
                var orig = btn.innerText; btn.innerText = '✓ Copied!'; setTimeout(() => btn.innerText = orig, 1500);
            }
        });
    }
}

var isResourceEnvMasked = true;
function toggleSecretMasking() {
    isResourceEnvMasked = !isResourceEnvMasked;
    document.querySelectorAll('.env-val-secret').forEach(function (el) {
        if (isResourceEnvMasked) {
            el.classList.add('masked');
            el.innerText = '••••••••••••••••';
        } else {
            el.classList.remove('masked');
            el.innerText = el.getAttribute('data-raw');
        }
    });
    var btn = document.getElementById('mask-env-btn');
    if (btn) btn.innerText = isResourceEnvMasked ? '👁 Show Secret Values' : '🙈 Hide Secret Values';
}

function parseResourceEnvTable() {
    var tbody = document.getElementById('env-table-body');
    if (!tbody) return;
    var raw = document.getElementById('env-textarea')?.value || '';
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
            '<td class="env-key">' + key + '</td>' +
            '<td><span class="' + (isSecret ? 'env-val env-val-secret masked' : 'env-val') + '" data-raw="' + val.replace(/"/g, '&quot;') + '">' + (isSecret ? '••••••••••••••••' : val) + '</span></td>' +
            '<td style="text-align: right;"><button type="button" class="btn btn-outline-secondary btn-sm py-0" style="font-size: 11px;" onclick="copySingleResourceEnv(\'' + val.replace(/'/g, "\\'") + '\', this)">Copy</button></td>' +
            '</tr>';
    });

    tbody.innerHTML = rows || '<tr><td colspan="3" class="text-center text-muted small py-3">No valid KEY=VALUE lines found.</td></tr>';
}

function copySingleResourceEnv(val, btn) {
    navigator.clipboard.writeText(val).then(function () {
        var orig = btn.innerText; btn.innerText = '✓'; setTimeout(() => btn.innerText = orig, 1200);
    });
}

// ----------------------------------------------------
// Page Initialization & Event Handlers
// ----------------------------------------------------
document.addEventListener('DOMContentLoaded', function () {
    // 1. Projects List Search Filter
    var searchInput = document.getElementById('search-projects');
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            var q = this.value.toLowerCase().trim();
            var rows = document.querySelectorAll('.project-row');
            var visible = 0;
            rows.forEach(function (row) {
                var name = row.dataset.name || '';
                var tpl = row.dataset.template || '';
                var match = !q || name.includes(q) || tpl.includes(q);
                row.style.display = match ? '' : 'none';
                if (match) visible++;
            });
            var countEl = document.getElementById('projects-count');
            if (countEl) countEl.innerText = visible;
        });
    }

    // 2. Create Project initialization
    initCreateProject();

    // 3. Project Resources page initialization
    var shareLink = document.getElementById('share-link-input');
    if (shareLink) {
        shareLink.value = window.location.href;
    }

    document.querySelectorAll('#resourceTabs button').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('#resourceTabs button').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-pane-content').forEach(p => p.style.display = 'none');
            btn.classList.add('active');
            var tab = btn.getAttribute('data-tab');
            var pane = document.getElementById('pane-' + tab);
            if (pane) pane.style.display = 'block';
        });
    });

    var docsRaw = document.getElementById('doc-textarea')?.value;
    var docsRenderEl = document.getElementById('docs-rendered-content');
    if (docsRenderEl && docsRaw) {
        docsRenderEl.innerHTML = renderProjectMarkdown(docsRaw);
    }

    parseProjectPostmanRequests();
    parseResourceEnvTable();
});

