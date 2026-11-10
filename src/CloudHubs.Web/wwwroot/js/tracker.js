// CloudHubs - Project Tracker & Kanban Board Scripts
var currentIssueId = null;

function openCreateWithStatus(status) {
    var modalEl = document.getElementById('createIssueModal');
    if (modalEl) {
        var modal = new bootstrap.Modal(modalEl);
        modal.show();
    }
}

// Drag and Drop (Kanban)
function handleDragStart(ev, issueId) {
    ev.dataTransfer.setData("text/plain", issueId);
    var card = document.getElementById('card-' + issueId);
    if (card) card.classList.add('dragging');
}

function allowDrop(ev) {
    ev.preventDefault();
}

function handleDrop(ev, newStatus) {
    ev.preventDefault();
    var issueId = ev.dataTransfer.getData("text/plain");
    var card = document.getElementById('card-' + issueId);
    if (card) card.classList.remove('dragging');

    moveStatus(issueId, newStatus);
}

// Move Status (AJAX)
function moveStatus(issueId, newStatus) {
    var csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/organization/tracker/update-status', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': csrfToken || ''
        },
        body: JSON.stringify({ issueId: issueId, status: parseInt(newStatus) })
    })
    .then(res => {
        if (res.ok) {
            window.location.reload();
        } else {
            return res.json().then(data => { alert(data.error || 'Unable to update status'); });
        }
    })
    .catch(err => console.error(err));
}

function updateDetailStatus(newStatus) {
    if (currentIssueId) {
        moveStatus(currentIssueId, newStatus);
    }
}

// View Issue Detail Modal
function viewIssueDetail(issueId) {
    currentIssueId = issueId;
    fetch('/organization/tracker/issue/' + issueId)
        .then(res => res.json())
        .then(data => {
            var issue = data.issue;
            var keyEl = document.getElementById('detail-key');
            if (keyEl) keyEl.innerText = issue.issueKey;
            
            var titleEl = document.getElementById('detail-title');
            if (titleEl) titleEl.innerText = issue.title;

            var descEl = document.getElementById('detail-desc');
            if (descEl) descEl.innerText = issue.description || 'No description provided.';

            var projEl = document.getElementById('detail-project-name');
            if (projEl) projEl.innerText = issue.projectName;

            var assigneeEl = document.getElementById('detail-assignee');
            if (assigneeEl) assigneeEl.innerText = issue.assigneeName || 'Unassigned';

            var reporterEl = document.getElementById('detail-reporter');
            if (reporterEl) reporterEl.innerText = issue.reporterName;

            var dueEl = document.getElementById('detail-due-date');
            if (dueEl) dueEl.innerText = issue.dueDate ? issue.dueDate.substring(0, 10) : 'None';

            var statusSelect = document.getElementById('detail-status-select');
            if (statusSelect) statusSelect.value = issue.status;

            var spentHoursEl = document.getElementById('detail-spent-hours');
            if (spentHoursEl) spentHoursEl.innerText = (issue.spentHours || 0) + 'h';

            var estHoursEl = document.getElementById('detail-est-hours');
            if (estHoursEl) estHoursEl.innerText = issue.estimatedHours ? (issue.estimatedHours + 'h') : '—';

            var hoursBar = document.getElementById('detail-hours-bar');
            if (hoursBar) {
                if (issue.estimatedHours && issue.estimatedHours > 0) {
                    var pct = Math.min(100, Math.round(((issue.spentHours || 0) / issue.estimatedHours) * 100));
                    hoursBar.style.width = pct + '%';
                } else {
                    hoursBar.style.width = '0%';
                }
            }

            // Comments
            var commentsThread = document.getElementById('comments-thread');
            var commentsCountEl = document.getElementById('detail-comments-count');
            if (commentsCountEl) commentsCountEl.innerText = data.comments.length + ' comments';

            if (commentsThread) {
                commentsThread.innerHTML = '';
                if (data.comments.length === 0) {
                    commentsThread.innerHTML = '<div class="text-muted small py-2">No comments yet on this ticket.</div>';
                } else {
                    data.comments.forEach(function(c) {
                        var dateStr = new Date(c.createdAt).toLocaleString();
                        var div = document.createElement('div');
                        div.className = 'p-3 rounded border bg-white';
                        div.innerHTML = '<div class="d-flex justify-content-between align-items-center mb-1">' +
                            '<span class="fw-bold small text-dark">' + escapeHtml(c.userName) + '</span>' +
                            '<small class="text-muted">' + dateStr + '</small>' +
                            '</div><div class="small text-secondary text-break">' + escapeHtml(c.commentText) + '</div>';
                        commentsThread.appendChild(div);
                    });
                }
            }

            // Activities
            var activitiesList = document.getElementById('detail-activities-list');
            if (activitiesList) {
                activitiesList.innerHTML = '';
                if (data.activities.length === 0) {
                    activitiesList.innerHTML = '<div class="text-muted">No activity history.</div>';
                } else {
                    data.activities.forEach(function(a) {
                        var dateStr = new Date(a.createdAt).toLocaleDateString();
                        var p = document.createElement('div');
                        p.className = 'mb-2 pb-2 border-bottom';
                        p.innerHTML = '<div class="fw-bold text-dark" style="font-size: 11.5px;">' + escapeHtml(a.action) + ' <small class="text-muted fw-normal">' + dateStr + '</small></div>' +
                            '<div class="text-muted" style="font-size: 11px;">' + escapeHtml(a.details) + '</div>';
                        activitiesList.appendChild(p);
                    });
                }
            }

            var deleteForm = document.getElementById('delete-issue-form');
            if (deleteForm) deleteForm.action = '/organization/tracker/delete/' + issue.id;

            var detailModalEl = document.getElementById('issueDetailModal');
            if (detailModalEl) {
                var modal = new bootstrap.Modal(detailModalEl);
                modal.show();
            }
        })
        .catch(err => alert('Failed to load issue details.'));
}

function submitComment() {
    var textInput = document.getElementById('new-comment-text');
    var text = textInput ? textInput.value.trim() : '';
    if (!text || !currentIssueId) return;

    var csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/organization/tracker/comment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': csrfToken || ''
        },
        body: JSON.stringify({ issueId: currentIssueId, commentText: text })
    })
    .then(res => res.json())
    .then(data => {
        if (data.success) {
            if (textInput) textInput.value = '';
            viewIssueDetail(currentIssueId);
        } else {
            alert(data.error || 'Failed to post comment');
        }
    })
    .catch(err => console.error(err));
}

function escapeHtml(str) {
    if (!str) return '';
    var p = document.createElement('p');
    p.appendChild(document.createTextNode(str));
    return p.innerHTML;
}

