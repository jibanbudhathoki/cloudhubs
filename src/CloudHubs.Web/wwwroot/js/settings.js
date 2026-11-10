// CloudHubs - Organization Settings & Subscription Scripts

// API Key Visibility Toggle
var isKeyVisible = false;

function toggleApiKeyVisibility() {
    var display = document.getElementById('api-key-display');
    var realEl = document.getElementById('api-key-real');
    var real = realEl ? realEl.innerText.trim() : '';
    var btn = document.getElementById('btn-toggle-key');
    if (!real || !display) return;

    if (isKeyVisible) {
        display.innerText = '••••••••••••••••••••••••••••••••••••••••••••';
        display.classList.add('api-key-masked');
        if (btn) btn.innerText = '👁️ Show Key';
        isKeyVisible = false;
    } else {
        display.innerText = real;
        display.classList.remove('api-key-masked');
        if (btn) btn.innerText = '🙈 Hide Key';
        isKeyVisible = true;
    }
}

// Copy Master API Key
function copyMasterKey() {
    var realEl = document.getElementById('api-key-real');
    var real = realEl ? realEl.innerText.trim() : '';
    if (real) {
        navigator.clipboard.writeText(real).then(function() {
            var btn = document.getElementById('btn-copy-key');
            if (btn) {
                var orig = btn.innerText;
                btn.innerText = '✓ Copied!';
                setTimeout(function() { btn.innerText = orig; }, 1600);
            }
        });
    }
}

// Copy Base URL
function copyBaseUrl() {
    var input = document.getElementById('platform-base-url');
    if (input && input.value) {
        navigator.clipboard.writeText(input.value).then(function() {
            var btn = document.getElementById('btn-copy-base');
            if (btn) {
                var orig = btn.innerText;
                btn.innerText = '✓ Copied!';
                setTimeout(function() { btn.innerText = orig; }, 1600);
            }
        });
    }
}

// Copy Code Snippets
function copySnippet(elementId) {
    var el = document.getElementById(elementId);
    if (el) {
        navigator.clipboard.writeText(el.innerText).then(function() {
            alert('Code snippet copied to clipboard!');
        });
    }
}

