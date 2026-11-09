// Portal interactions
document.addEventListener('DOMContentLoaded', function () {
    // Auto-close top workspace dropdown on click outside
    document.addEventListener('click', function (e) {
        document.querySelectorAll('.workspace-top-dropdown[open]').forEach(function (dropdown) {
            if (!dropdown.contains(e.target)) {
                dropdown.removeAttribute('open');
            }
        });
    });

    // Auto-close on Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            document.querySelectorAll('.workspace-top-dropdown[open]').forEach(function (dropdown) {
                dropdown.removeAttribute('open');
            });
        }
    });
});

