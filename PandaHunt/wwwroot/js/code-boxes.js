// Segmented code input (.ph-code > .ph-code-box): typing moves to the next box, Backspace on an
// empty box goes back, and pasting the whole code fills all boxes.
// Uses event delegation on document, so it also works when Blazor's enhanced navigation swaps the page in.
(() => {
    const BOX = ".ph-code-box";

    const isBox = el => el instanceof HTMLInputElement && el.matches(BOX);
    const boxesOf = box => [...box.closest(".ph-code").querySelectorAll(BOX)];

    // Writes text into consecutive boxes starting at `box`, then focuses the box after the last one written.
    function fill(box, text) {
        const group = box.closest(".ph-code");
        text = text.replace(/\s+/g, "").toUpperCase();
        if (group.dataset.numeric) text = text.replace(/\D/g, "");

        const boxes = boxesOf(box);
        let i = boxes.indexOf(box);
        box.value = "";
        for (const ch of text) {
            if (i >= boxes.length) break;
            boxes[i++].value = ch;
        }
        boxes[Math.min(i, boxes.length - 1)].focus();
    }

    document.addEventListener("input", e => {
        const box = e.target;
        if (!isBox(box) || e.inputType?.startsWith("delete")) return;

        // A single typed character comes in e.data; paste/autofill may put several characters in the value.
        fill(box, e.inputType === "insertText" && e.data ? e.data : box.value);
    });

    document.addEventListener("keydown", e => {
        const box = e.target;
        if (!isBox(box)) return;

        const boxes = boxesOf(box);
        const i = boxes.indexOf(box);
        if (e.key === "Backspace" && box.value === "" && i > 0) {
            e.preventDefault();
            boxes[i - 1].value = "";
            boxes[i - 1].focus();
        } else if (e.key === "ArrowLeft" && i > 0) {
            e.preventDefault();
            boxes[i - 1].focus();
        } else if (e.key === "ArrowRight" && i < boxes.length - 1) {
            e.preventDefault();
            boxes[i + 1].focus();
        }
    });

    // Select the box content on focus so typing overwrites it.
    document.addEventListener("focusin", e => {
        if (isBox(e.target)) e.target.select();
    });
})();
