// MeroSpace Cloud Workspace Client Scripts
var appEl = document.getElementById('merospace-app') || document.body;
var config = window.MeroSpaceConfig || {
    projectId: appEl?.getAttribute('data-project-id') || '',
    projectTemplate: appEl?.getAttribute('data-project-template') || '',
    projectName: appEl?.getAttribute('data-project-name') || '',
    token: document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
};
var projectId = config.projectId || appEl?.getAttribute('data-project-id') || '';
var projectTemplate = config.projectTemplate || appEl?.getAttribute('data-project-template') || '';
var projectName = config.projectName || appEl?.getAttribute('data-project-name') || '';
        var openTabs = [];
        var activeTab = null;
        var monacoEditor = null;
        var allProjectFiles = [];
        var projectComments = [];
        var editorDecorations = [];
        var contextNode = null;
        var token = config.token || document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

        // Real-Time Running State Machine
        var executionState = 'STOPPED'; // STOPPED, STARTING, RUNNING
        var executionUptimeSeconds = 0;
        var executionUptimeTimer = null;
        var executionPort = projectTemplate.includes('node') ? 3000 : 8080;

        // Terminal History
        var commandHistory = [];
        var historyIndex = -1;
        var isProgrammaticUpdate = false;

        // ==========================================
        // 1. Monaco Editor Initialization
        // ==========================================
        require.config({ paths: { vs: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs' } });
        require(['vs/editor/editor.main'], function() {
            monacoEditor = monaco.editor.create(document.getElementById('monaco-editor-container'), {
                value: '// Welcome to MeroSpace Cloud IDE\n// Opening project files...\n',
                language: 'javascript',
                theme: 'vs-dark',
                automaticLayout: true,
                fontSize: 13,
                tabSize: 2,
                glyphMargin: true,
                minimap: { enabled: true }
            });

            // Keyboard shortcut inside Monaco: Ctrl+S / Cmd+S
            monacoEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, function() {
                saveCurrentFile();
            });

            // Keyboard shortcut inside Monaco: Ctrl+P / Cmd+P
            monacoEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyP, function() {
                quickInput.focus();
                quickInput.select();
            });

            // Keyboard shortcuts inside Monaco: Close Tab (Ctrl+W), Close All (Ctrl+Shift+W)
            monacoEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyW, function() {
                closeActiveTab();
            });
            monacoEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.KeyW, function() {
                closeAllTabs();
            });

            monacoEditor.onDidChangeCursorPosition(function(e) {
                document.getElementById('status-cursor').innerText = 'Ln ' + e.position.lineNumber + ', Col ' + e.position.column;
            });

            monacoEditor.onDidChangeModelContent(function() {
                if (isProgrammaticUpdate) return;
                if (activeTab) {
                    activeTab.content = monacoEditor.getValue();
                    if (!activeTab.isDirty) {
                        activeTab.isDirty = true;
                        renderTabs();
                        document.getElementById('status-save-state').innerText = '● Unsaved';
                    }
                }
            });

            // Gutter glyph click -> opens comment for that line
            monacoEditor.onMouseDown(function(e) {
                if (e.target.type === monaco.editor.MouseTargetType.GUTTER_GLYPH_MARGIN) {
                    var lineNumber = e.target.position.lineNumber;
                    openCommentModalForCurrentFile(lineNumber);
                }
            });

            // Load initial files & comments & init API tester
            loadTree(true);
            loadComments();
            initApiTester();
        });

        // ==========================================
        // 2. Premium File Explorer & Icons (Persistent Folders)
        // ==========================================
        var EXPANDED_FOLDERS_KEY = 'merospace_expanded_' + projectId;
        var expandedFolderPaths = null;

        function getExpandedFolders() {
            if (expandedFolderPaths !== null) return expandedFolderPaths;
            try {
                var stored = localStorage.getItem(EXPANDED_FOLDERS_KEY);
                if (stored) {
                    var parsed = JSON.parse(stored);
                    if (Array.isArray(parsed)) {
                        expandedFolderPaths = new Set(parsed);
                        return expandedFolderPaths;
                    }
                }
            } catch (e) {
                console.warn('Failed to parse expanded folders from localStorage', e);
            }
            expandedFolderPaths = new Set();
            return expandedFolderPaths;
        }

        function saveExpandedFolders() {
            try {
                if (expandedFolderPaths) {
                    localStorage.setItem(EXPANDED_FOLDERS_KEY, JSON.stringify(Array.from(expandedFolderPaths)));
                }
            } catch (e) {
                console.warn('Failed to save expanded folders to localStorage', e);
            }
        }

        function collapseAllFolders() {
            var expSet = getExpandedFolders();
            expSet.clear();
            saveExpandedFolders();

            document.querySelectorAll('#file-tree-container .tree-chevron.expanded').forEach(function(chev) {
                chev.classList.remove('expanded');
            });

            document.querySelectorAll('#file-tree-container .tree-node-row[data-is-dir="true"]').forEach(function(r) {
                var name = r.dataset.name;
                var icon = r.querySelector('.tree-icon');
                if (icon && name) {
                    icon.innerHTML = getModernFileIconSvg(name, true, false);
                }
            });

            document.querySelectorAll('#file-tree-container .tree-children-box').forEach(function(box) {
                box.style.display = 'none';
            });
        }

        async function loadTree(openFirstFile) {
            try {
                var res = await fetch('/organization/merospace/' + projectId + '/tree');
                if (!res.ok) throw new Error('Tree error');
                var tree = await res.json();
                var container = document.getElementById('file-tree-container');
                container.innerHTML = '';

                allProjectFiles = [];
                buildFlatFileList(tree);

                renderTreeChildren(tree.children || [], container, 0, openFirstFile);
            } catch (e) {
                document.getElementById('file-tree-container').innerHTML = '<div class="p-3 text-danger">Failed to load files.</div>';
            }
        }

        function buildFlatFileList(node) {
            if (!node) return;
            if (!node.isDirectory && node.path) {
                allProjectFiles.push({ name: node.name, path: node.path });
            }
            if (node.children) {
                node.children.forEach(buildFlatFileList);
            }
        }

        function renderTreeChildren(nodes, container, level, isFirstLoad) {
            nodes.sort(function(a, b) {
                if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
                return a.name.localeCompare(b.name);
            });

            nodes.forEach(function(node) {
                var row = document.createElement('div');
                row.className = 'tree-node-row';
                row.dataset.path = node.path;
                row.dataset.name = node.name;
                row.dataset.isDir = node.isDirectory ? 'true' : 'false';

                // Indentation & Guide Lines
                var indentWrap = document.createElement('span');
                indentWrap.className = 'tree-indent';
                for (var i = 0; i < level; i++) {
                    var guide = document.createElement('span');
                    guide.className = 'tree-guide-line';
                    indentWrap.appendChild(guide);
                }
                row.appendChild(indentWrap);

                // Expansion determination for directories
                var isDirExpanded = false;
                var chevron = document.createElement('span');
                if (node.isDirectory) {
                    var expSet = getExpandedFolders();
                    var isFirstTime = localStorage.getItem(EXPANDED_FOLDERS_KEY) === null;
                    if (isFirstTime) {
                        isDirExpanded = (level === 0);
                        if (isDirExpanded) {
                            expSet.add(node.path);
                            saveExpandedFolders();
                        }
                    } else {
                        isDirExpanded = expSet.has(node.path);
                    }

                    chevron.className = 'tree-chevron' + (isDirExpanded ? ' expanded' : '');
                    chevron.innerHTML = '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"/></svg>';
                } else {
                    chevron.className = 'tree-chevron-leaf';
                }
                row.appendChild(chevron);

                // SVG Icon
                var icon = document.createElement('span');
                icon.className = 'tree-icon';
                icon.innerHTML = getModernFileIconSvg(node.name, node.isDirectory, isDirExpanded);
                row.appendChild(icon);

                // Label
                var label = document.createElement('span');
                label.className = 'tree-label';
                label.innerText = node.name;
                row.appendChild(label);

                // Comment badge if file has comments
                var commentCount = getCommentCountForFile(node.path);
                if (commentCount > 0) {
                    var badge = document.createElement('span');
                    badge.className = 'tree-comment-badge';
                    badge.title = commentCount + ' comments';
                    badge.innerText = '💬 ' + commentCount;
                    row.appendChild(badge);
                }

                // Hover Actions
                var actions = document.createElement('div');
                actions.className = 'tree-node-actions';
                if (node.isDirectory) {
                    actions.innerHTML = `
                        <button class="tree-mini-btn" title="New File inside" onclick="event.stopPropagation(); createNewFilePrompt('${node.path}')">📄</button>
                        <button class="tree-mini-btn" title="New Folder inside" onclick="event.stopPropagation(); createNewFolderPrompt('${node.path}')">📁</button>
                        <button class="tree-mini-btn" title="Rename" onclick="event.stopPropagation(); startInlineRename('${node.path}')">✏️</button>
                        <button class="tree-mini-btn" title="Delete" onclick="event.stopPropagation(); deleteNode('${node.path}')">🗑️</button>
                    `;
                } else {
                    actions.innerHTML = `
                        <button class="tree-mini-btn" title="Rename" onclick="event.stopPropagation(); startInlineRename('${node.path}')">✏️</button>
                        <button class="tree-mini-btn" title="Delete" onclick="event.stopPropagation(); deleteNode('${node.path}')">🗑️</button>
                    `;
                }
                row.appendChild(actions);

                container.appendChild(row);

                // Context Menu Listener
                row.oncontextmenu = function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                    openContextMenu(e, node);
                };

                // Click handler
                if (node.isDirectory) {
                    var childContainer = document.createElement('div');
                    childContainer.className = 'tree-children-box';
                    childContainer.style.display = isDirExpanded ? 'block' : 'none';
                    container.appendChild(childContainer);

                    if (node.children && node.children.length > 0) {
                        renderTreeChildren(node.children, childContainer, level + 1, isFirstLoad);
                    }

                    row.onclick = function() {
                        var isExpanded = chevron.classList.contains('expanded');
                        var expSet = getExpandedFolders();
                        if (isExpanded) {
                            chevron.classList.remove('expanded');
                            childContainer.style.display = 'none';
                            icon.innerHTML = getModernFileIconSvg(node.name, true, false);
                            expSet.delete(node.path);
                            saveExpandedFolders();
                        } else {
                            chevron.classList.add('expanded');
                            childContainer.style.display = 'block';
                            icon.innerHTML = getModernFileIconSvg(node.name, true, true);
                            expSet.add(node.path);
                            saveExpandedFolders();
                        }
                    };
                } else {
                    row.onclick = function() {
                        document.querySelectorAll('.tree-node-row').forEach(function(r) { r.classList.remove('active'); });
                        row.classList.add('active');
                        openFile(node.path, node.name);
                    };

                    // Auto open primary file on first load
                    if (isFirstLoad && (node.name.toLowerCase() === 'readme.md' || node.name.toLowerCase() === 'package.json' || node.name.toLowerCase() === 'main.go' || node.name.toLowerCase() === 'pom.xml')) {
                        row.classList.add('active');
                        openFile(node.path, node.name);
                        isFirstLoad = false;
                    }
                }
            });
        }

        function getModernFileIconSvg(name, isDir, isOpen) {
            if (isDir) {
                var folderColor = '#e8a838';
                if (isOpen) {
                    return `<svg width="15" height="15" viewBox="0 0 24 24" fill="${folderColor}"><path d="M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"/></svg>`;
                }
                return `<svg width="15" height="15" viewBox="0 0 24 24" fill="${folderColor}"><path d="M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"/></svg>`;
            }

            var ext = name.split('.').pop().toLowerCase();
            if (name.toLowerCase() === 'package.json') {
                return `<span style="color: #cb3837; font-weight: bold; font-size: 11px;">{⬢}</span>`;
            }
            if (name.toLowerCase() === '.gitignore') {
                return `<span style="color: #f05032; font-weight: bold; font-size: 11px;">⑂</span>`;
            }

            switch (ext) {
                case 'js':
                case 'mjs':
                    return `<span style="color: #f7df1e; font-weight: 800; font-size: 10px; background: #323330; padding: 1px 2px; border-radius: 2px;">JS</span>`;
                case 'ts':
                    return `<span style="color: #3178c6; font-weight: 800; font-size: 10px; background: #fff; padding: 1px 2px; border-radius: 2px;">TS</span>`;
                case 'json':
                    return `<span style="color: #cbcb41; font-weight: bold; font-size: 11px;">{ }</span>`;
                case 'md':
                    return `<span style="color: #42a5f5; font-weight: bold; font-size: 11px;">M↓</span>`;
                case 'go':
                    return `<span style="color: #00add8; font-weight: 800; font-size: 10px;">GO</span>`;
                case 'java':
                    return `<span style="color: #ea2d2e; font-size: 12px;">☕</span>`;
                case 'html':
                    return `<span style="color: #e34f26; font-weight: bold; font-size: 11px;">&lt;&gt;</span>`;
                case 'css':
                    return `<span style="color: #2965f1; font-weight: bold; font-size: 11px;">#</span>`;
                case 'xml':
                    return `<span style="color: #f29b38; font-weight: bold; font-size: 10px;">XML</span>`;
                default:
                    return `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#90a4ae" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>`;
            }
        }

        // ==========================================
        // 3. Tab Management & Monaco Editor
        // ==========================================
        async function openFile(filePath, fileName) {
            var existing = openTabs.find(function(t) { return t.path === filePath; });
            if (existing) {
                setActiveTab(existing);
                return;
            }

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/file?path=' + encodeURIComponent(filePath));
                if (!res.ok) throw new Error('Read failed');
                var data = await res.json();

                var newTab = {
                    path: filePath,
                    name: fileName,
                    content: data.content,
                    isDirty: false
                };
                openTabs.push(newTab);
                renderTabs();
                setActiveTab(newTab);
            } catch (e) {
                logTerminal('Error opening file: ' + filePath, 'error');
            }
        }

        function setActiveTab(tab) {
            activeTab = tab;
            renderTabs();

            document.getElementById('breadcrumb-path').innerText = tab.path;
            var lang = getLanguageMode(tab.name);
            document.getElementById('status-language').innerText = lang.toUpperCase();
            document.getElementById('status-save-state').innerText = tab.isDirty ? '● Unsaved' : '✓ Saved';

            if (monacoEditor) {
                isProgrammaticUpdate = true;
                monacoEditor.setValue(tab.content);
                var model = monacoEditor.getModel();
                if (model) monaco.editor.setModelLanguage(model, lang);
                isProgrammaticUpdate = false;
                updateEditorCommentDecorations();
            }
        }

        function renderTabs() {
            var bar = document.getElementById('editor-tabs-bar');
            bar.innerHTML = '';
            openTabs.forEach(function(tab) {
                var el = document.createElement('div');
                el.className = 'editor-tab' + (activeTab === tab ? ' active' : '') + (tab.isDirty ? ' dirty' : '');
                el.innerHTML = `<span>${tab.name}</span><span class="tab-dirty-indicator"></span><button class="tab-close" title="Close Tab">&times;</button>`;

                el.onclick = function(e) {
                    if (e.target.classList.contains('tab-close')) {
                        closeTab(tab);
                    } else {
                        setActiveTab(tab);
                    }
                };
                bar.appendChild(el);
            });
        }

        function closeTab(tab) {
            var idx = openTabs.indexOf(tab);
            if (idx > -1) {
                if (tab.isDirty) {
                    if (!confirm(`File '${tab.name}' has unsaved changes. Discard and close tab?`)) {
                        return;
                    }
                }
                openTabs.splice(idx, 1);
                if (activeTab === tab) {
                    if (openTabs.length > 0) {
                        setActiveTab(openTabs[Math.max(0, idx - 1)]);
                    } else {
                        activeTab = null;
                        if (monacoEditor) {
                            isProgrammaticUpdate = true;
                            monacoEditor.setValue('// No file open\n// Select a file from the explorer on the left or press Ctrl+P to quick open');
                            isProgrammaticUpdate = false;
                        }
                        document.getElementById('breadcrumb-path').innerText = projectName;
                        document.getElementById('status-language').innerText = 'PLAINTEXT';
                        document.getElementById('status-save-state').innerText = '✓ Saved';
                        document.querySelectorAll('.tree-node-row').forEach(function(r) { r.classList.remove('active'); });
                        renderTabs();
                    }
                } else {
                    renderTabs();
                }
            }
        }

        function closeActiveTab() {
            if (activeTab) closeTab(activeTab);
        }

        function closeAllTabs() {
            if (!openTabs || openTabs.length === 0) return;

            var dirtyTabs = openTabs.filter(function(t) { return t.isDirty; });
            if (dirtyTabs.length > 0) {
                var dirtyNames = dirtyTabs.map(function(t) { return t.name; }).join(', ');
                if (!confirm(`You have unsaved changes in:\n${dirtyNames}\n\nDiscard changes and close all tabs?`)) {
                    return;
                }
            }

            openTabs = [];
            activeTab = null;
            renderTabs();

            if (monacoEditor) {
                isProgrammaticUpdate = true;
                monacoEditor.setValue('// No file open\n// Select a file from the explorer on the left or press Ctrl+P to quick open');
                isProgrammaticUpdate = false;
            }

            document.getElementById('breadcrumb-path').innerText = projectName;
            document.getElementById('status-language').innerText = 'PLAINTEXT';
            document.getElementById('status-save-state').innerText = '✓ Saved';
            document.querySelectorAll('.tree-node-row').forEach(function(r) { r.classList.remove('active'); });
        }

        function getLanguageMode(fileName) {
            var ext = fileName.split('.').pop().toLowerCase();
            switch (ext) {
                case 'js':
                case 'mjs': return 'javascript';
                case 'ts': return 'typescript';
                case 'json': return 'json';
                case 'go': return 'go';
                case 'java': return 'java';
                case 'html': return 'html';
                case 'css': return 'css';
                case 'md': return 'markdown';
                case 'xml': return 'xml';
                default: return 'plaintext';
            }
        }

        // ==========================================
        // 4. Save & File CRUD
        // ==========================================
        async function saveCurrentFile() {
            if (!activeTab) return;
            var content = monacoEditor.getValue();
            activeTab.content = content;

            var saveBtn = document.getElementById('btn-save');
            var originalText = saveBtn ? saveBtn.innerHTML : '';
            if (saveBtn) saveBtn.innerHTML = '💾 Saving...';

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/file', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ path: activeTab.path, content: content })
                });

                if (res.ok) {
                    activeTab.isDirty = false;
                    renderTabs();
                    document.getElementById('status-save-state').innerText = '✓ Saved';
                    logTerminal('Saved file: ' + activeTab.path, 'info');
                    if (saveBtn) {
                        saveBtn.innerHTML = '✓ Saved';
                        setTimeout(function() { saveBtn.innerHTML = originalText; }, 1000);
                    }
                } else {
                    var errData = await res.json().catch(() => ({}));
                    throw new Error(errData.error || 'Save error');
                }
            } catch (e) {
                logTerminal('Failed to save file: ' + activeTab.path + ' (' + e.message + ')', 'error');
                if (saveBtn) {
                    saveBtn.innerHTML = '❌ Error';
                    setTimeout(function() { saveBtn.innerHTML = originalText; }, 1500);
                }
            }
        }

        function createNewFilePrompt(parentDir) {
            document.getElementById('modal-title').innerText = '📄 Create New File';
            document.getElementById('modal-input-name').placeholder = parentDir ? parentDir + '/filename.js' : 'src/new-file.js';
            document.getElementById('modal-input-name').value = parentDir ? parentDir + '/' : '';
            document.getElementById('modal-is-directory').value = 'false';
            document.getElementById('modal-parent-dir').value = parentDir || '';
            openModal('modal-new-item');
            document.getElementById('modal-input-name').focus();
        }

        function createNewFolderPrompt(parentDir) {
            document.getElementById('modal-title').innerText = '📁 Create New Folder';
            document.getElementById('modal-input-name').placeholder = parentDir ? parentDir + '/new-folder' : 'src/new-folder';
            document.getElementById('modal-input-name').value = parentDir ? parentDir + '/' : '';
            document.getElementById('modal-is-directory').value = 'true';
            document.getElementById('modal-parent-dir').value = parentDir || '';
            openModal('modal-new-item');
            document.getElementById('modal-input-name').focus();
        }

        async function submitCreateItem() {
            var path = document.getElementById('modal-input-name').value.trim();
            var isDir = document.getElementById('modal-is-directory').value === 'true';
            if (!path) return;

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/create-item', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ path: path, isDirectory: isDir })
                });

                if (res.ok) {
                    closeModal('modal-new-item');
                    await loadTree();
                    if (!isDir) openFile(path, path.split('/').pop());
                    logTerminal('Created ' + (isDir ? 'directory: ' : 'file: ') + path, 'info');
                } else {
                    alert('Could not create item.');
                }
            } catch (e) {
                alert('Error creating item.');
            }
        }

        document.getElementById('modal-input-name').addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                submitCreateItem();
            } else if (e.key === 'Escape') {
                closeModal('modal-new-item');
            }
        });

        async function deleteNode(path) {
            if (!confirm('Are you sure you want to delete "' + path + '"?')) return;
            try {
                var res = await fetch('/organization/merospace/' + projectId + '/delete-item', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ path: path })
                });

                if (res.ok) {
                    // Close tab if open
                    var tab = openTabs.find(function(t) { return t.path === path; });
                    if (tab) closeTab(tab);
                    await loadTree();
                    logTerminal('Deleted item: ' + path, 'info');
                } else {
                    alert('Could not delete item.');
                }
            } catch (e) {
                alert('Error deleting item.');
            }
        }

        function startInlineRename(oldPath) {
            var row = document.querySelector(`.tree-node-row[data-path="${oldPath}"]`);
            if (!row) return;

            var label = row.querySelector('.tree-label');
            var oldName = row.dataset.name;

            var input = document.createElement('input');
            input.className = 'tree-rename-input';
            input.value = oldName;

            label.innerHTML = '';
            label.appendChild(input);
            input.focus();
            input.select();

            var finished = false;
            async function commitRename() {
                if (finished) return;
                finished = true;
                var newName = input.value.trim();
                if (!newName || newName === oldName) {
                    label.innerText = oldName;
                    return;
                }

                var parentPath = oldPath.includes('/') ? oldPath.substring(0, oldPath.lastIndexOf('/')) : '';
                var newPath = parentPath ? parentPath + '/' + newName : newName;

                try {
                    var res = await fetch('/organization/merospace/' + projectId + '/rename-item', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': token
                        },
                        body: JSON.stringify({ oldPath: oldPath, newPath: newPath })
                    });

                    if (res.ok) {
                        var tab = openTabs.find(function(t) { return t.path === oldPath; });
                        if (tab) {
                            tab.path = newPath;
                            tab.name = newName;
                            renderTabs();
                        }
                        await loadTree();
                        logTerminal(`Renamed '${oldPath}' to '${newPath}'`, 'info');
                    } else {
                        alert('Could not rename item.');
                        label.innerText = oldName;
                    }
                } catch (e) {
                    alert('Error renaming.');
                    label.innerText = oldName;
                }
            }

            input.onkeydown = function(e) {
                if (e.key === 'Enter') commitRename();
                if (e.key === 'Escape') { finished = true; label.innerText = oldName; }
            };
            input.onblur = function() { commitRename(); };
        }

        // ==========================================
        // 5. Context Menu
        // ==========================================
        function openContextMenu(e, node) {
            contextNode = node;
            var menu = document.getElementById('custom-context-menu');
            menu.style.left = e.clientX + 'px';
            menu.style.top = e.clientY + 'px';
            menu.classList.add('active');
        }

        function closeContextMenu() {
            document.getElementById('custom-context-menu').classList.remove('active');
        }

        window.addEventListener('click', function() {
            closeContextMenu();
            closeMenus();
        });

        function contextAction(action) {
            if (!contextNode) return;
            var path = contextNode.path;
            var isDir = contextNode.isDirectory;

            switch (action) {
                case 'new-file':
                    createNewFilePrompt(isDir ? path : getParentDir(path));
                    break;
                case 'new-folder':
                    createNewFolderPrompt(isDir ? path : getParentDir(path));
                    break;
                case 'rename':
                    startInlineRename(path);
                    break;
                case 'delete':
                    deleteNode(path);
                    break;
                case 'copy-path':
                    navigator.clipboard.writeText(path);
                    logTerminal('Copied path to clipboard: ' + path, 'info');
                    break;
                case 'add-comment':
                    openCommentModalForFile(path);
                    break;
            }
            closeContextMenu();
        }

        function getParentDir(p) {
            return p.includes('/') ? p.substring(0, p.lastIndexOf('/')) : '';
        }

        // ==========================================
        // 6. Real-Time Running Engine
        // ==========================================
        async function startExecution() {
            if (executionState === 'RUNNING') return;
            executionState = 'STARTING';
            updateExecutionUI();

            logTerminal(`[${getCurrentTime()}] Starting real-time runner for '${projectName}'...`, 'info');

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/run', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ command: 'start' })
                });

                if (!res.ok) {
                    var errText = await res.text();
                    try {
                        var errJson = JSON.parse(errText);
                        throw new Error(errJson.error || errJson.message || `HTTP ${res.status}`);
                    } catch (pe) {
                        throw new Error(`HTTP ${res.status}: ${errText.substring(0, 80)}`);
                    }
                }

                var data = await res.json();
                if (data.port) {
                    executionPort = data.port;
                    var btnPreview = document.getElementById('btn-preview');
                    if (btnPreview) {
                        btnPreview.href = `http://localhost:${executionPort}/health`;
                        btnPreview.innerText = `🌐 Open localhost:${executionPort}`;
                    }
                }

                if (data.success) {
                    var lines = (data.output || '').split('\n');
                    var delay = 0;
                    lines.forEach(function(line, idx) {
                        setTimeout(function() {
                            logTerminal(line);
                            if (idx === lines.length - 1) {
                                executionState = 'RUNNING';
                                updateExecutionUI();
                                startUptimeTimer();
                                logTerminal(`[${getCurrentTime()}] ✅ Real process active at http://localhost:${executionPort}/ (PID ${data.pid || 'live'})`, 'success');
                            }
                        }, delay);
                        delay += 80;
                    });
                } else {
                    executionState = 'STOPPED';
                    updateExecutionUI();
                    logTerminal(`[${getCurrentTime()}] Execution halted: ${data.error || data.output}`, 'error');
                }
            } catch (e) {
                executionState = 'STOPPED';
                updateExecutionUI();
                logTerminal(`[${getCurrentTime()}] Execution failed: ${e.message}`, 'error');
            }
        }

        async function stopExecution() {
            try {
                await fetch('/organization/merospace/' + projectId + '/stop', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    }
                });
            } catch (e) {
                console.warn('Stop error:', e);
            }

            executionState = 'STOPPED';
            stopUptimeTimer();
            updateExecutionUI();
            logTerminal(`[${getCurrentTime()}] Process gracefully terminated. (Exit code 0)`, 'warn');
        }

        async function restartExecution() {
            logTerminal(`[${getCurrentTime()}] Restarting process...`, 'info');
            await stopExecution();
            await new Promise(function(resolve) { setTimeout(resolve, 600); });
            await startExecution();
        }

        function updateExecutionUI() {
            var btnRun = document.getElementById('btn-run');
            var btnStop = document.getElementById('btn-stop');
            var btnRestart = document.getElementById('btn-restart');
            var btnPreview = document.getElementById('btn-preview');
            var statusText = document.getElementById('status-runner-text');
            var statusDot = document.querySelector('.status-dot');
            var statusbar = document.getElementById('main-statusbar');
            var debugStatus = document.getElementById('debug-status-display');

            if (executionState === 'RUNNING') {
                btnRun.style.display = 'none';
                btnStop.style.display = 'inline-flex';
                btnRestart.style.display = 'inline-flex';
                btnPreview.style.display = 'inline-flex';
                statusText.innerText = `Running :${executionPort}`;
                statusDot.className = 'status-dot running-pulse';
                statusbar.classList.add('running');
                debugStatus.innerText = 'Running';
                debugStatus.className = 'text-success fw-bold';
            } else if (executionState === 'STARTING') {
                btnRun.style.display = 'none';
                btnStop.style.display = 'inline-flex';
                btnRestart.style.display = 'none';
                btnPreview.style.display = 'none';
                statusText.innerText = 'Starting...';
                statusDot.className = 'status-dot';
                debugStatus.innerText = 'Starting...';
                debugStatus.className = 'text-warning';
            } else {
                btnRun.style.display = 'inline-flex';
                btnStop.style.display = 'none';
                btnRestart.style.display = 'none';
                btnPreview.style.display = 'none';
                statusText.innerText = 'Stopped';
                statusDot.className = 'status-dot';
                statusbar.classList.remove('running');
                debugStatus.innerText = 'Stopped';
                debugStatus.className = 'text-muted';
            }
        }

        function startUptimeTimer() {
            stopUptimeTimer();
            executionUptimeSeconds = 0;
            var pill = document.getElementById('status-uptime-pill');
            var debugUptime = document.getElementById('debug-uptime-display');
            pill.style.display = 'inline-block';

            executionUptimeTimer = setInterval(function() {
                executionUptimeSeconds++;
                var mins = String(Math.floor(executionUptimeSeconds / 60)).padStart(2, '0');
                var secs = String(executionUptimeSeconds % 60).padStart(2, '0');
                var str = `${mins}:${secs}`;
                pill.innerText = `Uptime: ${str}`;
                debugUptime.innerText = str;
            }, 1000);
        }

        function stopUptimeTimer() {
            if (executionUptimeTimer) clearInterval(executionUptimeTimer);
            document.getElementById('status-uptime-pill').style.display = 'none';
            document.getElementById('debug-uptime-display').innerText = '00:00';
        }

        function openLivePreview() {
            var frame = document.getElementById('preview-frame');
            document.getElementById('preview-url').value = `http://localhost:${executionPort}/`;

            var templateHtml = '';
            if (projectTemplate.includes('node')) {
                templateHtml = `
                    <div style="max-width: 680px; margin: 40px auto; font-family: sans-serif;">
                        <h1 style="color: #1f795f;">🟢 ${projectName} is Live</h1>
                        <p style="font-size: 16px; color: #555;">Server responding on <code>http://localhost:${executionPort}/</code></p>
                        <div style="background: #f8f9fa; border: 1px solid #e9ecef; border-radius: 6px; padding: 16px; margin: 20px 0;">
                            <strong>Active API Routes:</strong>
                            <ul style="margin-top: 8px; color: #333;">
                                <li><code>GET /health</code> &rarr; <code>{"status":"healthy","uptime":${executionUptimeSeconds}}</code></li>
                                <li><code>GET /api/items</code> &rarr; <code>[{"id":1,"name":"Default Item"}]</code></li>
                            </ul>
                        </div>
                        <p style="color: #888; font-size: 12px;">Powered by MeroSpace Cloud Workspace · CloudHubs</p>
                    </div>
                `;
            } else if (projectTemplate === 'go') {
                templateHtml = `
                    <div style="max-width: 680px; margin: 40px auto; font-family: sans-serif;">
                        <h1 style="color: #00add8;">🔷 Go Application Running</h1>
                        <p style="font-size: 16px; color: #555;">Listening on <code>http://localhost:${executionPort}/</code></p>
                        <div style="background: #f0faff; border: 1px solid #bce4fa; border-radius: 6px; padding: 16px;">
                            <strong>Go HTTP Server:</strong> Status 200 OK. Standard routing active.
                        </div>
                    </div>
                `;
            } else {
                templateHtml = `
                    <div style="max-width: 680px; margin: 40px auto; font-family: sans-serif;">
                        <h1 style="color: #ea2d2e;">☕ Java Service Active</h1>
                        <p style="font-size: 16px; color: #555;">Endpoint: <code>http://localhost:${executionPort}/</code></p>
                    </div>
                `;
            }

            frame.innerHTML = templateHtml;
            openModal('modal-live-preview');
        }

        function refreshPreview() {
            openLivePreview();
        }

        // ==========================================
        // 7. Interactive Terminal Input & Commands
        // ==========================================
        document.getElementById('terminal-input').addEventListener('keydown', async function(e) {
            if (e.key === 'Enter') {
                var cmd = this.value.trim();
                this.value = '';
                if (!cmd) return;

                commandHistory.push(cmd);
                historyIndex = commandHistory.length;

                logTerminal('$ ' + cmd);

                if (cmd === 'clear') {
                    clearTerminal();
                } else if (cmd === 'run' || cmd === 'start' || cmd === 'npm start' || cmd === 'go run') {
                    startExecution();
                } else if (cmd === 'stop') {
                    stopExecution();
                } else if (cmd === 'restart') {
                    restartExecution();
                } else if (cmd === 'status') {
                    logTerminal(`Project: ${projectName}\nStatus: ${executionState}\nUptime: ${executionUptimeSeconds}s\nPort: ${executionPort}`, 'info');
                } else if (cmd === 'help') {
                    logTerminal('Available commands:\n  run, start   - Start real-time server\n  stop         - Stop server\n  restart      - Reboot server\n  status       - Check runtime status\n  ls           - List workspace files\n  cat <file>   - Print file contents\n  clear        - Clear terminal output', 'info');
                } else {
                    // Send to backend exec
                    try {
                        var res = await fetch('/organization/merospace/' + projectId + '/exec', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                                'RequestVerificationToken': token
                            },
                            body: JSON.stringify({ command: cmd })
                        });
                        var data = await res.json();
                        logTerminal(data.output);
                    } catch (err) {
                        logTerminal(`Unknown command: '${cmd}'. Type 'help' for available commands.`);
                    }
                }
            } else if (e.key === 'ArrowUp') {
                if (historyIndex > 0) {
                    historyIndex--;
                    this.value = commandHistory[historyIndex];
                }
            } else if (e.key === 'ArrowDown') {
                if (historyIndex < commandHistory.length - 1) {
                    historyIndex++;
                    this.value = commandHistory[historyIndex];
                } else {
                    historyIndex = commandHistory.length;
                    this.value = '';
                }
            }
        });

        function logTerminal(text, type) {
            var out = document.getElementById('terminal-output-text');
            var prefix = '';
            if (type === 'error') prefix = '❌ ';
            else if (type === 'success') prefix = '✅ ';
            else if (type === 'warn') prefix = '⚠️ ';

            out.innerText += '\n' + prefix + text;
            var body = document.getElementById('panel-body');
            body.scrollTop = body.scrollHeight;
        }

        function clearTerminal() {
            document.getElementById('terminal-output-text').innerText = '$ ';
        }

        function toggleTerminal() {
            var panel = document.getElementById('bottom-panel');
            panel.style.display = panel.style.display === 'none' ? 'flex' : 'none';
        }

        function switchPanelTab(tabName) {
            document.querySelectorAll('.panel-tab').forEach(function(t) {
                t.classList.toggle('active', t.dataset.panel === tabName);
            });
            if (tabName === 'output') {
                logTerminal(`[Output] Build & telemetry stream active for '${projectName}'.`, 'info');
            } else if (tabName === 'apitester') {
                openApiTester();
            }
        }

        // ==========================================
        // 7b. Futuristic In-IDE API Tester Engine
        // ==========================================
        var API_HISTORY_KEY = 'merospace_api_history_' + projectId;
        var apiHistoryList = [];
        var activeApiTab = 'params';
        var lastApiResponse = null;

        function initApiTester() {
            var methodSelect = document.getElementById('api-method');
            if (methodSelect) {
                onApiMethodChange();
            }

            var urlInput = document.getElementById('api-url');
            if (urlInput) {
                if (!urlInput.value) {
                    urlInput.value = 'http://localhost:' + executionPort + '/health';
                }
                syncUrlToParams();
            }

            var portDisplay = document.getElementById('api-detected-port');
            if (portDisplay) {
                portDisplay.innerText = 'Target: :' + executionPort;
            }

            loadApiHistory();
            updateAllApiBadges();
        }

        function onApiMethodChange() {
            var methodSelect = document.getElementById('api-method');
            if (!methodSelect) return;
            var m = methodSelect.value.toLowerCase();
            methodSelect.className = 'api-method-select ' + m;

            // If switching to POST, PUT, or PATCH, recommend body tab if empty or none
            if (m === 'post' || m === 'put' || m === 'patch') {
                var bodyRadio = document.querySelector('input[name="api-body-type"]:checked');
                if (bodyRadio && bodyRadio.value === 'none') {
                    var jsonRadio = document.querySelector('input[name="api-body-type"][value="json"]');
                    if (jsonRadio) {
                        jsonRadio.checked = true;
                        onBodyTypeChange('json');
                    }
                }
            }
        }

        function switchApiTab(tabName) {
            activeApiTab = tabName;
            var tabs = ['params', 'headers', 'auth', 'body', 'history'];
            tabs.forEach(function(t) {
                var btn = document.getElementById('tab-btn-' + t);
                var panel = document.getElementById('api-panel-' + t);
                if (btn) btn.classList.toggle('active', t === tabName);
                if (panel) panel.style.display = (t === tabName) ? 'block' : 'none';
            });
            if (tabName === 'history') {
                renderApiHistory();
            }
        }

        // --- Query Params Management ---
        function onApiUrlInput() {
            syncUrlToParams();
        }

        function syncUrlToParams() {
            var urlInput = document.getElementById('api-url');
            if (!urlInput) return;
            var raw = urlInput.value;
            var qIdx = raw.indexOf('?');
            var container = document.getElementById('api-params-table');
            if (!container) return;

            var params = [];
            if (qIdx !== -1 && qIdx < raw.length - 1) {
                var qStr = raw.substring(qIdx + 1);
                var pairs = qStr.split('&');
                pairs.forEach(function(p) {
                    var parts = p.split('=');
                    var k = decodeURIComponent(parts[0] || '');
                    var v = decodeURIComponent(parts.slice(1).join('=') || '');
                    if (k || v) params.push({ key: k, value: v });
                });
            }

            renderParamsRows(params);
            updateParamBadge();
        }

        function renderParamsRows(params) {
            var container = document.getElementById('api-params-table');
            if (!container) return;
            container.innerHTML = '';
            if (params.length === 0) {
                addParamRow('', '', false);
                return;
            }
            params.forEach(function(p) {
                addParamRow(p.key, p.value, false);
            });
        }

        function addParamRow(key, value, doSync) {
            var container = document.getElementById('api-params-table');
            if (!container) return;
            var row = document.createElement('div');
            row.className = 'api-kv-row';
            row.innerHTML = `
                <input class="api-kv-input param-key" placeholder="Parameter key" value="${escapeHtml(key || '')}" oninput="syncParamsToUrl()" />
                <input class="api-kv-input param-val" placeholder="Value" value="${escapeHtml(value || '')}" oninput="syncParamsToUrl()" />
                <button type="button" class="api-kv-del" onclick="removeParamRow(this)" title="Delete Parameter">&times;</button>
            `;
            container.appendChild(row);
            if (doSync !== false) syncParamsToUrl();
            updateParamBadge();
        }

        function removeParamRow(btn) {
            var row = btn.closest('.api-kv-row');
            if (row) {
                row.remove();
                syncParamsToUrl();
                updateParamBadge();
            }
        }

        function clearAllParams() {
            var container = document.getElementById('api-params-table');
            if (container) container.innerHTML = '';
            addParamRow('', '', false);
            syncParamsToUrl();
            updateParamBadge();
        }

        function syncParamsToUrl() {
            var urlInput = document.getElementById('api-url');
            if (!urlInput) return;
            var currentUrl = urlInput.value;
            var base = currentUrl.split('?')[0];

            var rows = document.querySelectorAll('#api-params-table .api-kv-row');
            var pairs = [];
            rows.forEach(function(r) {
                var k = r.querySelector('.param-key')?.value.trim();
                var v = r.querySelector('.param-val')?.value.trim();
                if (k) {
                    pairs.push(encodeURIComponent(k) + '=' + encodeURIComponent(v || ''));
                }
            });

            if (pairs.length > 0) {
                urlInput.value = base + '?' + pairs.join('&');
            } else {
                urlInput.value = base;
            }
            updateParamBadge();
        }

        function updateParamBadge() {
            var rows = document.querySelectorAll('#api-params-table .api-kv-row');
            var count = 0;
            rows.forEach(function(r) {
                if (r.querySelector('.param-key')?.value.trim()) count++;
            });
            var badge = document.getElementById('badge-params');
            if (badge) {
                badge.innerText = count;
                badge.classList.toggle('has-data', count > 0);
            }
        }

        // --- HTTP Headers Management ---
        function addHeaderRow(key, value) {
            var container = document.getElementById('api-headers-table');
            if (!container) return;
            var row = document.createElement('div');
            row.className = 'api-kv-row';
            row.innerHTML = `
                <input class="api-kv-input header-key" placeholder="Header name (e.g. Accept)" value="${escapeHtml(key || '')}" oninput="updateHeaderBadge()" />
                <input class="api-kv-input header-val" placeholder="Header value" value="${escapeHtml(value || '')}" oninput="updateHeaderBadge()" />
                <button type="button" class="api-kv-del" onclick="removeHeaderRow(this)" title="Delete Header">&times;</button>
            `;
            container.appendChild(row);
            updateHeaderBadge();
        }

        function removeHeaderRow(btn) {
            var row = btn.closest('.api-kv-row');
            if (row) {
                row.remove();
                updateHeaderBadge();
            }
        }

        function addQuickHeader(key, val) {
            var existingRows = document.querySelectorAll('#api-headers-table .api-kv-row');
            var found = false;
            existingRows.forEach(function(r) {
                var kInput = r.querySelector('.header-key');
                if (kInput && kInput.value.trim().toLowerCase() === key.toLowerCase()) {
                    var vInput = r.querySelector('.header-val');
                    if (vInput) vInput.value = val;
                    found = true;
                }
            });
            if (!found) {
                addHeaderRow(key, val);
            }
            switchApiTab('headers');
            updateHeaderBadge();
        }

        function updateHeaderBadge() {
            var rows = document.querySelectorAll('#api-headers-table .api-kv-row');
            var count = 0;
            rows.forEach(function(r) {
                if (r.querySelector('.header-key')?.value.trim()) count++;
            });
            var badge = document.getElementById('badge-headers');
            if (badge) {
                badge.innerText = count;
                badge.classList.toggle('has-data', count > 0);
            }
        }

        function collectHeaders() {
            var headers = {};
            var rows = document.querySelectorAll('#api-headers-table .api-kv-row');
            rows.forEach(function(r) {
                var k = r.querySelector('.header-key')?.value.trim();
                var v = r.querySelector('.header-val')?.value.trim();
                if (k) headers[k] = v || '';
            });

            // Auth Injection
            var authType = document.getElementById('api-auth-type')?.value || 'none';
            if (authType === 'bearer') {
                var tokenVal = document.getElementById('api-auth-token')?.value.trim();
                if (tokenVal) headers['Authorization'] = 'Bearer ' + tokenVal;
            } else if (authType === 'basic') {
                var user = document.getElementById('api-auth-user')?.value || '';
                var pass = document.getElementById('api-auth-pass')?.value || '';
                if (user || pass) {
                    try {
                        headers['Authorization'] = 'Basic ' + btoa(user + ':' + pass);
                    } catch(e) {}
                }
            } else if (authType === 'apikey') {
                var apikeyKey = document.getElementById('api-auth-apikey-key')?.value.trim() || 'X-API-Key';
                var apikeyVal = document.getElementById('api-auth-apikey-val')?.value.trim() || '';
                if (apikeyVal) headers[apikeyKey] = apikeyVal;
            }

            return headers;
        }

        // --- Authentication ---
        function onAuthTypeChange() {
            var select = document.getElementById('api-auth-type');
            var val = select ? select.value : 'none';
            var bearerWrap = document.getElementById('auth-bearer-wrap');
            var basicWrap = document.getElementById('auth-basic-wrap');
            var apikeyWrap = document.getElementById('auth-apikey-wrap');

            if (bearerWrap) bearerWrap.style.display = (val === 'bearer') ? 'block' : 'none';
            if (basicWrap) basicWrap.style.display = (val === 'basic') ? 'block' : 'none';
            if (apikeyWrap) apikeyWrap.style.display = (val === 'apikey') ? 'block' : 'none';

            var badge = document.getElementById('badge-auth');
            if (badge) {
                badge.innerText = val;
                badge.classList.toggle('has-data', val !== 'none');
            }
        }

        // --- Body Management ---
        function onBodyTypeChange(type) {
            var bodyText = document.getElementById('api-body');
            var actionsDeck = document.getElementById('body-actions-deck');
            if (!bodyText) return;

            if (type === 'none') {
                bodyText.style.display = 'none';
                if (actionsDeck) actionsDeck.style.display = 'none';
            } else {
                bodyText.style.display = 'block';
                if (actionsDeck) actionsDeck.style.display = (type === 'json') ? 'flex' : 'none';
                if (type === 'json' && !bodyText.value.trim()) {
                    bodyText.value = '{\n  \n}';
                }
            }
            updateBodyBadge();
        }

        function formatApiJsonBody() {
            var bodyText = document.getElementById('api-body');
            if (!bodyText || !bodyText.value.trim()) return;
            try {
                var parsed = JSON.parse(bodyText.value.trim());
                bodyText.value = JSON.stringify(parsed, null, 2);
            } catch (e) {
                alert('Invalid JSON in Request Body: ' + e.message);
            }
            updateBodyBadge();
        }

        function minifyApiJsonBody() {
            var bodyText = document.getElementById('api-body');
            if (!bodyText || !bodyText.value.trim()) return;
            try {
                var parsed = JSON.parse(bodyText.value.trim());
                bodyText.value = JSON.stringify(parsed);
            } catch (e) {
                alert('Invalid JSON in Request Body: ' + e.message);
            }
            updateBodyBadge();
        }

        function updateBodyBadge() {
            var radio = document.querySelector('input[name="api-body-type"]:checked');
            var type = radio ? radio.value : 'none';
            var bodyText = document.getElementById('api-body');
            var badge = document.getElementById('badge-body');
            if (!badge) return;

            if (type === 'none') {
                badge.innerText = 'none';
                badge.classList.remove('has-data');
            } else {
                var len = (bodyText && bodyText.value) ? bodyText.value.trim().length : 0;
                if (len > 0) {
                    badge.innerText = type + ' (' + len + 'c)';
                    badge.classList.add('has-data');
                } else {
                    badge.innerText = type;
                    badge.classList.remove('has-data');
                }
            }
        }

        function updateAllApiBadges() {
            updateParamBadge();
            updateHeaderBadge();
            onAuthTypeChange();
            updateBodyBadge();
            updateHistoryBadge();
        }

        // --- Quick Presets ---
        function setApiPreset(method, path, bodyJson) {
            var methodSelect = document.getElementById('api-method');
            var urlInput = document.getElementById('api-url');
            var bodyText = document.getElementById('api-body');

            if (methodSelect) {
                methodSelect.value = method;
                onApiMethodChange();
            }

            if (urlInput) {
                var cleanPath = path.startsWith('/') ? path : '/' + path;
                urlInput.value = 'http://localhost:' + executionPort + cleanPath;
                syncUrlToParams();
            }

            if (bodyJson !== undefined && bodyJson !== null) {
                var jsonRadio = document.querySelector('input[name="api-body-type"][value="json"]');
                if (jsonRadio) {
                    jsonRadio.checked = true;
                    onBodyTypeChange('json');
                }
                if (bodyText) bodyText.value = bodyJson;
                switchApiTab('body');
            } else {
                var noneRadio = document.querySelector('input[name="api-body-type"][value="none"]');
                if (noneRadio && (method === 'GET' || method === 'DELETE')) {
                    noneRadio.checked = true;
                    onBodyTypeChange('none');
                }
            }
            updateAllApiBadges();
        }

        // --- Core Execution Engine ---
        async function sendApiRequest() {
            var methodSelect = document.getElementById('api-method');
            var urlInput = document.getElementById('api-url');
            var sendBtn = document.getElementById('btn-send-api');
            var sendIcon = document.getElementById('btn-send-icon');
            var sendLabel = document.getElementById('btn-send-label');
            var respContainer = document.getElementById('api-response-container');
            var responseBody = document.getElementById('api-response-body');

            var method = methodSelect ? methodSelect.value.toUpperCase() : 'GET';
            var rawUrl = urlInput ? urlInput.value.trim() : '';

            if (!rawUrl) {
                alert('Please enter a target URL.');
                return;
            }

            var fullUrl = rawUrl;
            if (!fullUrl.startsWith('http://') && !fullUrl.startsWith('https://')) {
                fullUrl = 'http://localhost:' + executionPort + (fullUrl.startsWith('/') ? fullUrl : '/' + fullUrl);
                if (urlInput) urlInput.value = fullUrl;
            }

            // Collect Body
            var bodyContent = null;
            var bodyRadio = document.querySelector('input[name="api-body-type"]:checked');
            var bodyType = bodyRadio ? bodyRadio.value : 'none';
            if (bodyType !== 'none') {
                var bodyText = document.getElementById('api-body');
                bodyContent = bodyText ? bodyText.value : null;
            }

            // Collect Headers
            var reqHeaders = collectHeaders();
            if (bodyType === 'json' && !reqHeaders['Content-Type']) {
                reqHeaders['Content-Type'] = 'application/json';
            }
            if (!reqHeaders['Accept']) {
                reqHeaders['Accept'] = 'application/json, text/plain, */*';
            }

            if (sendBtn) {
                sendBtn.disabled = true;
                if (sendIcon) sendIcon.innerText = '⏳';
                if (sendLabel) sendLabel.innerText = 'Dispatching...';
            }

            var startTime = performance.now();

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/api-proxy', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        method: method,
                        url: fullUrl,
                        headers: reqHeaders,
                        body: bodyContent
                    })
                });

                var elapsed = Math.round(performance.now() - startTime);

                if (!res.ok) {
                    var errData = await res.json().catch(function() { return {}; });
                    var errMsg = errData.error || ('HTTP ' + res.status);
                    renderResponseStatus(res.status, 'Error', elapsed, 0);
                    if (responseBody) {
                        responseBody.style.color = '#f85149';
                        responseBody.textContent = 'Proxy Error: ' + errMsg + '\n\nMake sure the local server is running by clicking "▶ Run" or pressing F5.';
                    }
                    if (respContainer) respContainer.style.display = 'flex';
                    logTerminal(`[API Tester] ${method} ${fullUrl} -> Failed (${errMsg})`, 'error');
                    saveToHistory(method, fullUrl, res.status, elapsed, 0);
                    return;
                }

                var data = await res.json();
                var duration = data.elapsedMs || elapsed;
                var sizeBytes = data.sizeBytes || (data.body ? data.body.length : 0);

                lastApiResponse = {
                    status: data.statusCode,
                    statusText: data.statusText || '',
                    duration: duration,
                    sizeBytes: sizeBytes,
                    body: data.body || '',
                    headers: data.headers || {}
                };

                renderResponseStatus(data.statusCode, data.statusText, duration, sizeBytes);
                renderResponseBody(data.body);
                renderResponseHeaders(data.headers);

                if (respContainer) respContainer.style.display = 'flex';
                logTerminal(`[API Tester] ${method} ${fullUrl} -> ${data.statusCode} ${data.statusText || ''} (${duration}ms, ${formatBytes(sizeBytes)})`, data.statusCode < 400 ? 'success' : 'warn');

                saveToHistory(method, fullUrl, data.statusCode, duration, sizeBytes);

            } catch (err) {
                var errElapsed = Math.round(performance.now() - startTime);
                renderResponseStatus(0, 'Connection Refused', errElapsed, 0);
                if (responseBody) {
                    responseBody.style.color = '#f85149';
                    responseBody.textContent = 'Could not reach ' + fullUrl + '.\n\nIs your application running on port ' + executionPort + '?\nClick ▶ Run in the top bar to start your project server.';
                }
                if (respContainer) respContainer.style.display = 'flex';
                logTerminal(`[API Tester] Connection Refused on ${fullUrl}. Server not running or blocked.`, 'error');
                saveToHistory(method, fullUrl, 0, errElapsed, 0);
            } finally {
                if (sendBtn) {
                    sendBtn.disabled = false;
                    if (sendIcon) sendIcon.innerText = '⚡';
                    if (sendLabel) sendLabel.innerText = 'Send Request';
                }
            }
        }

        function renderResponseStatus(code, text, elapsedMs, bytes) {
            var badge = document.getElementById('api-status-badge');
            var timeBadge = document.getElementById('api-time-badge');
            var sizeBadge = document.getElementById('api-size-badge');

            if (badge) {
                badge.className = 'api-status-badge';
                if (code >= 200 && code < 300) {
                    badge.classList.add('s2xx');
                    badge.innerText = code + ' ' + (text || 'OK');
                } else if (code >= 300 && code < 400) {
                    badge.classList.add('s3xx');
                    badge.innerText = code + ' ' + (text || 'Redirect');
                } else if (code >= 400 && code < 500) {
                    badge.classList.add('s4xx');
                    badge.innerText = code + ' ' + (text || 'Bad Request');
                } else if (code >= 500) {
                    badge.classList.add('s5xx');
                    badge.innerText = code + ' ' + (text || 'Server Error');
                } else {
                    badge.classList.add('s5xx');
                    badge.innerText = text || 'Failed';
                }
            }

            if (timeBadge) timeBadge.innerText = elapsedMs + ' ms';
            if (sizeBadge) sizeBadge.innerText = formatBytes(bytes);
        }

        function renderResponseBody(bodyRaw) {
            var viewer = document.getElementById('api-response-body');
            if (!viewer) return;
            var text = bodyRaw || '';
            try {
                var parsed = JSON.parse(text);
                viewer.textContent = JSON.stringify(parsed, null, 2);
                viewer.style.color = '#a6e22e';
            } catch (e) {
                viewer.textContent = text || '(empty response)';
                viewer.style.color = '#e6edf3';
            }
            switchResponseView('body');
        }

        function renderResponseHeaders(headers) {
            var wrap = document.getElementById('api-response-headers-wrap');
            if (!wrap) return;
            if (!headers || Object.keys(headers).length === 0) {
                wrap.innerHTML = '<div class="text-muted p-2">No response headers returned.</div>';
                return;
            }
            var rows = Object.entries(headers).map(function(e) {
                return `<div class="d-flex justify-content-between py-1 border-bottom border-dark">
                    <strong class="text-info">${escapeHtml(e[0])}:</strong>
                    <span class="text-white font-monospace text-truncate ms-2" title="${escapeHtml(e[1])}">${escapeHtml(e[1])}</span>
                </div>`;
            }).join('');
            wrap.innerHTML = rows;
        }

        function switchResponseView(view) {
            var bodyEl = document.getElementById('api-response-body');
            var headersEl = document.getElementById('api-response-headers-wrap');
            var bodyBtn = document.getElementById('resp-tab-body-btn');
            var headersBtn = document.getElementById('resp-tab-headers-btn');

            if (view === 'body') {
                if (bodyEl) bodyEl.style.display = 'block';
                if (headersEl) headersEl.style.display = 'none';
                if (bodyBtn) { bodyBtn.className = 'btn btn-sm btn-link p-0 text-decoration-none text-white fw-bold'; }
                if (headersBtn) { headersBtn.className = 'btn btn-sm btn-link p-0 text-decoration-none text-muted'; }
            } else {
                if (bodyEl) bodyEl.style.display = 'none';
                if (headersEl) headersEl.style.display = 'block';
                if (bodyBtn) { bodyBtn.className = 'btn btn-sm btn-link p-0 text-decoration-none text-muted'; }
                if (headersBtn) { headersBtn.className = 'btn btn-sm btn-link p-0 text-decoration-none text-white fw-bold'; }
            }
        }

        function formatBytes(bytes) {
            if (!bytes || bytes === 0) return '0 B';
            var k = 1024;
            var sizes = ['B', 'KB', 'MB', 'GB'];
            var i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
        }

        // --- Response Actions ---
        function copyApiResponse() {
            var body = document.getElementById('api-response-body');
            if (body && body.textContent) {
                navigator.clipboard.writeText(body.textContent).then(function() {
                    var btn = document.querySelector('button[onclick="copyApiResponse()"]');
                    if (btn) {
                        var orig = btn.innerHTML;
                        btn.innerHTML = '✓ Copied';
                        setTimeout(function() { btn.innerHTML = orig; }, 1200);
                    }
                });
            }
        }

        function downloadApiResponse() {
            if (!lastApiResponse || !lastApiResponse.body) {
                alert('No response content to download.');
                return;
            }
            var blob = new Blob([lastApiResponse.body], { type: 'application/json' });
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = 'api-response-' + Date.now() + '.json';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }

        function clearApiResponse() {
            var resp = document.getElementById('api-response-container');
            if (resp) resp.style.display = 'none';
            lastApiResponse = null;
        }

        function resetApiTester() {
            var methodSelect = document.getElementById('api-method');
            var urlInput = document.getElementById('api-url');
            var bodyText = document.getElementById('api-body');
            var respContainer = document.getElementById('api-response-container');

            if (methodSelect) {
                methodSelect.value = 'GET';
                onApiMethodChange();
            }
            if (urlInput) {
                urlInput.value = 'http://localhost:' + executionPort + '/health';
            }
            clearAllParams();

            var hTable = document.getElementById('api-headers-table');
            if (hTable) hTable.innerHTML = '';

            var authSel = document.getElementById('api-auth-type');
            if (authSel) {
                authSel.value = 'none';
                onAuthTypeChange();
            }

            var noneRadio = document.querySelector('input[name="api-body-type"][value="none"]');
            if (noneRadio) {
                noneRadio.checked = true;
                onBodyTypeChange('none');
            }
            if (bodyText) bodyText.value = '';

            if (respContainer) respContainer.style.display = 'none';
            switchApiTab('params');
            updateAllApiBadges();
        }

        function openApiTester() {
            var btn = document.querySelector('.activity-btn[data-view="apitester"]');
            if (btn) btn.click();
            var input = document.getElementById('api-url');
            if (input) input.focus();
        }

        // --- History Sub-System ---
        function loadApiHistory() {
            try {
                var stored = localStorage.getItem(API_HISTORY_KEY);
                apiHistoryList = stored ? JSON.parse(stored) : [];
            } catch (e) {
                apiHistoryList = [];
            }
            updateHistoryBadge();
        }

        function saveToHistory(method, url, status, timeMs, sizeBytes) {
            var item = {
                id: 'req_' + Date.now(),
                time: new Date().toLocaleTimeString(),
                method: method,
                url: url,
                status: status,
                timeMs: timeMs,
                sizeBytes: sizeBytes
            };
            apiHistoryList.unshift(item);
            if (apiHistoryList.length > 25) apiHistoryList.pop();

            try {
                localStorage.setItem(API_HISTORY_KEY, JSON.stringify(apiHistoryList));
            } catch (e) {}

            updateHistoryBadge();
            if (activeApiTab === 'history') renderApiHistory();
        }

        function updateHistoryBadge() {
            var badge = document.getElementById('badge-history');
            if (badge) {
                badge.innerText = apiHistoryList.length;
                badge.classList.toggle('has-data', apiHistoryList.length > 0);
            }
        }

        function renderApiHistory() {
            var container = document.getElementById('api-history-container');
            if (!container) return;

            if (apiHistoryList.length === 0) {
                container.innerHTML = '<div class="text-muted text-center py-3" style="font-size: 11px;">No past requests yet.</div>';
                return;
            }

            container.innerHTML = apiHistoryList.map(function(item) {
                return `
                    <div class="api-history-item" onclick="restoreHistoryItem('${item.id}')" title="Click to reload this request">
                        <span class="api-hist-method ${item.method}">${item.method}</span>
                        <span class="api-hist-url">${escapeHtml(item.url)}</span>
                        <span class="api-hist-time">${item.status ? item.status : 'ERR'} • ${item.timeMs}ms</span>
                    </div>
                `;
            }).join('');
        }

        function restoreHistoryItem(id) {
            var item = apiHistoryList.find(function(x) { return x.id === id; });
            if (!item) return;

            var methodSelect = document.getElementById('api-method');
            var urlInput = document.getElementById('api-url');

            if (methodSelect) {
                methodSelect.value = item.method;
                onApiMethodChange();
            }
            if (urlInput) {
                urlInput.value = item.url;
                syncUrlToParams();
            }

            switchApiTab('params');
            logTerminal(`[API Tester] Restored ${item.method} ${item.url} from history.`, 'info');
        }

        function clearApiHistory() {
            apiHistoryList = [];
            try {
                localStorage.removeItem(API_HISTORY_KEY);
            } catch (e) {}
            updateHistoryBadge();
            renderApiHistory();
        }

        // ==========================================
        // 8. Code Comments & Review System
        // ==========================================
        var commentTarget = { filePath: '', lineNumber: 1, selectedText: '' };

        async function loadComments() {
            try {
                var res = await fetch('/organization/merospace/' + projectId + '/comments');
                if (!res.ok) throw new Error('Failed to load comments');
                projectComments = await res.json();
                renderCommentsList();
                updateEditorCommentDecorations();

                var badge = document.getElementById('comments-badge');
                var countPill = document.getElementById('status-comments-count');
                var unresolved = projectComments.filter(function(c) { return !c.isResolved; }).length;

                badge.innerText = unresolved;
                badge.style.display = unresolved > 0 ? 'inline-block' : 'none';
                countPill.innerText = `💬 ${projectComments.length} comments`;
            } catch (e) {
                console.error(e);
            }
        }

        function getCommentCountForFile(filePath) {
            return projectComments.filter(function(c) { return c.filePath === filePath; }).length;
        }

        function renderCommentsList(filter) {
            var container = document.getElementById('comments-list-container');
            container.innerHTML = '';

            var currentFilter = filter || document.querySelector('.filter-btn.active')?.dataset.filter || 'all';

            var filtered = projectComments.filter(function(c) {
                if (currentFilter === 'open') return !c.isResolved;
                if (currentFilter === 'resolved') return c.isResolved;
                return true;
            });

            if (filtered.length === 0) {
                container.innerHTML = '<div class="text-muted small p-3 text-center">No comments found.<br><button class="btn btn-sm btn-outline-secondary mt-2" onclick="openCommentModalForCurrentFile()">+ Add Comment</button></div>';
                return;
            }

            filtered.forEach(function(c) {
                var card = document.createElement('div');
                card.className = 'comment-card' + (c.isResolved ? ' resolved' : '');

                var initials = (c.authorName || 'U').substring(0, 2).toUpperCase();
                var timeStr = formatRelativeTime(c.createdAt);

                var repliesHtml = '';
                if (c.replies && c.replies.length > 0) {
                    repliesHtml = '<div class="comment-reply-box">';
                    c.replies.forEach(function(r) {
                        repliesHtml += `
                            <div class="reply-item">
                                <div class="d-flex justify-content-between mb-1">
                                    <strong class="text-white">${r.authorName}</strong>
                                    <span class="text-muted">${formatRelativeTime(r.createdAt)}</span>
                                </div>
                                <div>${escapeHtml(r.content)}</div>
                            </div>
                        `;
                    });
                    repliesHtml += '</div>';
                }

                card.innerHTML = `
                    <div class="comment-header">
                        <div class="comment-author">
                            <span class="comment-avatar">${initials}</span>
                            <span>${c.authorName}</span>
                        </div>
                        <span class="comment-time">${timeStr}</span>
                    </div>
                    <span class="comment-file-badge" onclick="jumpToComment('${c.filePath}', ${c.lineNumber})">
                        ${c.filePath}:${c.lineNumber}
                    </span>
                    ${c.selectedText ? `<div class="comment-snippet">${escapeHtml(c.selectedText)}</div>` : ''}
                    <div class="comment-body-text">${escapeHtml(c.content)}</div>
                    ${repliesHtml}
                    <div class="comment-actions-row">
                        <button class="btn btn-sm btn-link p-0 text-decoration-none ${c.isResolved ? 'text-secondary' : 'text-success'}" onclick="toggleResolveComment('${c.id}')">
                            ${c.isResolved ? '↺ Reopen' : '✓ Resolve'}
                        </button>
                        <button class="btn btn-sm btn-link p-0 text-info text-decoration-none" onclick="promptReplyComment('${c.id}')">
                            Reply
                        </button>
                        <button class="btn btn-sm btn-link p-0 text-danger text-decoration-none" onclick="deleteComment('${c.id}')">
                            Delete
                        </button>
                    </div>
                `;

                container.appendChild(card);
            });
        }

        function filterComments(filterType) {
            document.querySelectorAll('.filter-btn').forEach(function(b) {
                b.classList.toggle('active', b.dataset.filter === filterType);
            });
            renderCommentsList(filterType);
        }

        function openCommentModalForCurrentFile(targetLine) {
            if (!activeTab) {
                alert('Please open a file to add comments.');
                return;
            }

            var selection = monacoEditor ? monacoEditor.getSelection() : null;
            var line = targetLine || (selection ? selection.startLineNumber : 1);
            var selectedText = selection && monacoEditor ? monacoEditor.getModel().getValueInRange(selection) : '';

            commentTarget = {
                filePath: activeTab.path,
                lineNumber: line,
                selectedText: selectedText.trim()
            };

            document.getElementById('comment-target-file').innerText = `${activeTab.path}:${line}`;
            var snippetBox = document.getElementById('comment-target-snippet');
            if (commentTarget.selectedText) {
                snippetBox.style.display = 'block';
                snippetBox.innerText = commentTarget.selectedText;
            } else {
                snippetBox.style.display = 'none';
            }

            document.getElementById('comment-input-content').value = '';
            openModal('modal-add-comment');
            document.getElementById('comment-input-content').focus();
        }

        function openCommentModalForFile(filePath) {
            commentTarget = {
                filePath: filePath,
                lineNumber: 1,
                selectedText: ''
            };
            document.getElementById('comment-target-file').innerText = `${filePath}:1`;
            document.getElementById('comment-target-snippet').style.display = 'none';
            document.getElementById('comment-input-content').value = '';
            openModal('modal-add-comment');
            document.getElementById('comment-input-content').focus();
        }

        async function submitAddComment() {
            var content = document.getElementById('comment-input-content').value.trim();
            if (!content) return;

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/comments', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        filePath: commentTarget.filePath,
                        lineNumber: commentTarget.lineNumber,
                        selectedText: commentTarget.selectedText,
                        content: content
                    })
                });

                if (res.ok) {
                    closeModal('modal-add-comment');
                    await loadComments();
                    logTerminal(`Added comment on ${commentTarget.filePath}:${commentTarget.lineNumber}`, 'info');
                } else {
                    alert('Could not save comment.');
                }
            } catch (e) {
                alert('Error saving comment.');
            }
        }

        async function promptReplyComment(commentId) {
            var text = prompt('Enter your reply:');
            if (!text || !text.trim()) return;

            try {
                var res = await fetch('/organization/merospace/' + projectId + '/comments/' + commentId + '/reply', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({ content: text.trim() })
                });

                if (res.ok) {
                    await loadComments();
                } else {
                    alert('Could not add reply.');
                }
            } catch (e) {
                alert('Error replying.');
            }
        }

        async function toggleResolveComment(commentId) {
            try {
                var res = await fetch('/organization/merospace/' + projectId + '/comments/' + commentId + '/toggle-resolve', {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token }
                });
                if (res.ok) await loadComments();
            } catch (e) {}
        }

        async function deleteComment(commentId) {
            if (!confirm('Delete this comment?')) return;
            try {
                var res = await fetch('/organization/merospace/' + projectId + '/comments/' + commentId, {
                    method: 'DELETE',
                    headers: { 'RequestVerificationToken': token }
                });
                if (res.ok) await loadComments();
            } catch (e) {}
        }

        function jumpToComment(filePath, lineNumber) {
            openFile(filePath, filePath.split('/').pop());
            setTimeout(function() {
                if (monacoEditor) {
                    monacoEditor.revealLineInCenter(lineNumber);
                    monacoEditor.setPosition({ lineNumber: lineNumber, column: 1 });
                }
            }, 250);
        }

        function updateEditorCommentDecorations() {
            if (!monacoEditor || !activeTab) return;

            var fileComments = projectComments.filter(function(c) {
                return c.filePath === activeTab.path && !c.isResolved;
            });

            var newDecorations = fileComments.map(function(c) {
                return {
                    range: new monaco.Range(c.lineNumber, 1, c.lineNumber, 1),
                    options: {
                        isWholeLine: true,
                        glyphMarginClassName: 'comment-glyph-margin',
                        glyphMarginHoverMessage: { value: `**Comment by ${c.authorName}**: ${c.content}` },
                        className: 'comment-line-highlight'
                    }
                };
            });

            editorDecorations = monacoEditor.deltaDecorations(editorDecorations, newDecorations);
        }

        // ==========================================
        // 9. Quick Open (<Ctrl+P>) & Search
        // ==========================================
        var quickInput = document.getElementById('quick-open-input');
        var quickBox = document.getElementById('quick-open-results');

        quickInput.addEventListener('focus', function() {
            renderQuickOpenResults(quickInput.value.trim());
        });

        quickInput.addEventListener('input', function() {
            renderQuickOpenResults(this.value.trim());
        });

        function renderQuickOpenResults(query) {
            quickBox.innerHTML = '';
            var matches = allProjectFiles.filter(function(f) {
                return !query || f.path.toLowerCase().includes(query.toLowerCase());
            }).slice(0, 15);

            if (matches.length === 0) {
                quickBox.innerHTML = '<div class="p-2 text-muted small">No files matched.</div>';
                quickBox.classList.add('open');
                return;
            }

            matches.forEach(function(f, idx) {
                var item = document.createElement('div');
                item.className = 'quick-item' + (idx === 0 ? ' selected' : '');
                item.innerHTML = `
                    <span>${getModernFileIconSvg(f.name, false)}</span>
                    <strong>${f.name}</strong>
                    <span class="quick-item-path">${f.path}</span>
                `;
                item.onclick = function() {
                    openFile(f.path, f.name);
                    quickBox.classList.remove('open');
                    quickInput.value = '';
                };
                quickBox.appendChild(item);
            });
            quickBox.classList.add('open');
        }

        document.addEventListener('click', function(e) {
            if (!quickInput.contains(e.target) && !quickBox.contains(e.target)) {
                quickBox.classList.remove('open');
            }
        });

        quickInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                var selected = quickBox.querySelector('.quick-item.selected') || quickBox.querySelector('.quick-item');
                if (selected) selected.click();
            } else if (e.key === 'Escape') {
                quickBox.classList.remove('open');
            }
        });

        // Search in Files
        var searchTimeout = null;
        document.getElementById('search-input').addEventListener('input', function() {
            clearTimeout(searchTimeout);
            var q = this.value.trim();
            if (q.length < 2) return;
            searchTimeout = setTimeout(async function() {
                var res = await fetch('/organization/merospace/' + projectId + '/search?q=' + encodeURIComponent(q));
                var items = await res.json();
                var box = document.getElementById('search-results-container');
                box.innerHTML = '';
                if (items.length === 0) {
                    box.innerHTML = '<div class="text-muted small">No results found.</div>';
                    return;
                }
                items.forEach(function(item) {
                    var el = document.createElement('div');
                    el.className = 'search-result-item';
                    el.innerHTML = `<div class="search-result-file"><span>📄</span> ${item.filePath}:${item.lineNumber}</div><div class="search-result-snippet">${escapeHtml(item.lineContent)}</div>`;
                    el.onclick = function() {
                        openFile(item.filePath, item.filePath.split('/').pop());
                        setTimeout(function() {
                            if (monacoEditor) {
                                monacoEditor.revealLineInCenter(item.lineNumber);
                                monacoEditor.setPosition({ lineNumber: item.lineNumber, column: 1 });
                            }
                        }, 200);
                    };
                    box.appendChild(el);
                });
            }, 250);
        });

        // ==========================================
        // 10. Menus, Activity Bar & Shortcuts
        // ==========================================
        function toggleMenu(menuId) {
            var menu = document.getElementById(menuId);
            var isOpen = menu.classList.contains('open');
            closeMenus();
            if (!isOpen) menu.classList.add('open');
        }

        function closeMenus() {
            document.querySelectorAll('.menu-dropdown').forEach(function(m) { m.classList.remove('open'); });
        }

        document.querySelectorAll('.activity-btn[data-view]').forEach(function(btn) {
            btn.addEventListener('click', function() {
                document.querySelectorAll('.activity-btn').forEach(function(b) { b.classList.remove('active'); });
                btn.classList.add('active');

                var targetView = btn.dataset.view;
                document.querySelectorAll('.sidebar-view').forEach(function(view) {
                    view.classList.toggle('active', view.id === 'view-' + targetView);
                });

                if (targetView === 'comments') renderCommentsList();
            });
        });

        function toggleTheme() {
            if (monacoEditor) {
                var current = monacoEditor._themeService._theme.themeName;
                var next = current.includes('dark') ? 'vs' : 'vs-dark';
                monaco.editor.setTheme(next);
            }
        }

        function simulateGitCommit() {
            var msg = document.getElementById('git-commit-msg').value.trim();
            if (!msg) {
                alert('Please enter a commit message.');
                return;
            }
            document.getElementById('git-commit-msg').value = '';
            logTerminal(`[Git] Committed "${msg}" to main branch.`, 'success');
        }

        // Global Keyboard Shortcuts
        window.addEventListener('keydown', function(e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                var apitesterView = document.getElementById('view-apitester');
                if (apitesterView && apitesterView.classList.contains('active')) {
                    e.preventDefault();
                    sendApiRequest();
                }
            } else if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'W' || e.key === 'w')) {
                e.preventDefault();
                closeAllTabs();
            } else if ((e.ctrlKey || e.metaKey) && (e.key === 'w' || e.key === 'W')) {
                e.preventDefault();
                closeActiveTab();
            } else if ((e.ctrlKey || e.metaKey) && e.key === 's') {
                e.preventDefault();
                saveCurrentFile();
            } else if ((e.ctrlKey || e.metaKey) && e.key === 'p') {
                e.preventDefault();
                quickInput.focus();
                quickInput.select();
            } else if ((e.ctrlKey || e.metaKey) && e.key === '`') {
                e.preventDefault();
                toggleTerminal();
            } else if (e.key === 'F5') {
                e.preventDefault();
                if (executionState === 'STOPPED') startExecution();
                else restartExecution();
            }
        });

        // Helper Utilities
        function openModal(id) { document.getElementById(id).classList.add('active'); }
        function closeModal(id) { document.getElementById(id).classList.remove('active'); }

        function getCurrentTime() {
            var now = new Date();
            return now.toTimeString().split(' ')[0];
        }

        function formatRelativeTime(dateStr) {
            try {
                var d = new Date(dateStr);
                var diff = (Date.now() - d.getTime()) / 1000;
                if (diff < 60) return 'Just now';
                if (diff < 3600) return Math.floor(diff / 60) + 'm ago';
                if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
                return d.toLocaleDateString();
            } catch (e) {
                return 'recently';
            }
        }

        function escapeHtml(str) {
            return (str || '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#039;');
        }
