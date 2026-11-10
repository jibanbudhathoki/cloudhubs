// Superuser Organization Form Scripts
document.addEventListener('DOMContentLoaded', function () {
    const adminMode = document.getElementById("admin-mode");
    if (!adminMode) return;
    const adminExisting = document.getElementById("admin-existing-fields");
    const adminNew = document.getElementById("admin-new-fields");
    const memberRows = document.getElementById("member-rows");
    const memberTemplate = document.getElementById("member-row-template");

    function updateAdminFields() {
        if (adminExisting) adminExisting.hidden = adminMode.value !== "existing";
        if (adminNew) adminNew.hidden = adminMode.value !== "new";
    }

    function updateMemberFields(row) {
        if (!row) return;
        const modeEl = row.querySelector(".member-mode");
        if (!modeEl) return;
        const isNew = modeEl.value === "new";
        const existingEl = row.querySelector(".member-existing-fields");
        const newEl = row.querySelector(".member-new-fields");
        if (existingEl) existingEl.hidden = isNew;
        if (newEl) newEl.hidden = !isNew;
    }

    function reindexMemberRows() {
        if (!memberRows) return;
        memberRows.querySelectorAll(".member-row").forEach((row, index) => {
            row.querySelectorAll("[name], [id], [for]").forEach(element => {
                for (const attribute of ["name", "id", "for"]) {
                    if (element.hasAttribute(attribute)) {
                        const value = element.getAttribute(attribute)
                            .replace(/Members\[\d+\]/g, `Members[${index}]`)
                            .replace(/Members_\d+__/g, `Members_${index}__`);
                        element.setAttribute(attribute, value);
                    }
                }
            });
        });
    }

    adminMode.addEventListener("change", updateAdminFields);

    const addMemberBtn = document.getElementById("add-member");
    if (addMemberBtn && memberRows && memberTemplate) {
        addMemberBtn.addEventListener("click", () => {
            const index = memberRows.querySelectorAll(".member-row").length;
            const row = memberTemplate.content.cloneNode(true);
            row.querySelectorAll("[name], [id], [for]").forEach(element => {
                for (const attribute of ["name", "id", "for"]) {
                    if (element.hasAttribute(attribute)) {
                        element.setAttribute(attribute, element.getAttribute(attribute).replaceAll("__index__", index));
                    }
                }
            });
            memberRows.appendChild(row);
            reindexMemberRows();
            updateMemberFields(memberRows.lastElementChild);
        });
    }

    if (memberRows) {
        memberRows.addEventListener("change", event => {
            if (event.target.classList.contains("member-mode")) {
                updateMemberFields(event.target.closest(".member-row"));
            }
        });
        memberRows.addEventListener("click", event => {
            if (event.target.closest(".remove-member")) {
                event.target.closest(".member-row").remove();
                reindexMemberRows();
            }
        });
    }

    updateAdminFields();
    if (memberRows) {
        memberRows.querySelectorAll(".member-row").forEach(updateMemberFields);
    }
});

