window.ShaderPlayground = {
    // Set up drag data on snippet buttons
    initSnippetDrag() {
        document.querySelectorAll(".snippet-btn[data-snippet]").forEach(el => {
            el.addEventListener("dragstart", (e) => {
                e.dataTransfer.setData("text/plain", el.getAttribute("data-snippet"));
                e.dataTransfer.effectAllowed = "copy";
            });
        });
    },

    // Set up drop zone on textarea — accepts dragged snippet text
    initDropZone(textareaId) {
        const ta = document.getElementById(textareaId);
        if (!ta) return;

        ta.addEventListener("dragover", (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = "copy";
            ta.classList.add("drop-highlight");
        });

        ta.addEventListener("dragleave", () => {
            ta.classList.remove("drop-highlight");
        });

        ta.addEventListener("drop", (e) => {
            e.preventDefault();
            ta.classList.remove("drop-highlight");
            const text = e.dataTransfer.getData("text/plain");
            if (!text) return;

            // Insert at cursor or at end
            const start = ta.selectionStart;
            const before = ta.value.substring(0, start);
            const after = ta.value.substring(ta.selectionEnd);
            ta.value = before + text + after;

            // Move cursor after inserted text
            const newPos = start + text.length;
            ta.selectionStart = newPos;
            ta.selectionEnd = newPos;
            ta.focus();

            // Fire input event so Blazor picks up the change
            ta.dispatchEvent(new Event("input", { bubbles: true }));
            ta.dispatchEvent(new Event("change", { bubbles: true }));
        });
    },

    // Insert text at cursor position in textarea
    insertAtCursor(textareaId, text) {
        const ta = document.getElementById(textareaId);
        if (!ta) return;

        const start = ta.selectionStart;
        const before = ta.value.substring(0, start);
        const after = ta.value.substring(ta.selectionEnd);
        ta.value = before + text + after;

        const newPos = start + text.length;
        ta.selectionStart = newPos;
        ta.selectionEnd = newPos;
        ta.focus();

        ta.dispatchEvent(new Event("input", { bubbles: true }));
        ta.dispatchEvent(new Event("change", { bubbles: true }));
    },

    // Get the current textarea value (for when Blazor binding lags)
    getValue(textareaId) {
        const ta = document.getElementById(textareaId);
        return ta ? ta.value : "";
    },
};
