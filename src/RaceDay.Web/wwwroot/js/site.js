// RaceDay front-end helpers. Progressive enhancement only: every page works without JavaScript.
(function () {
    "use strict";

    // <form data-confirm="Are you sure?"> asks before submitting (deletes, publishing results, cancelling).
    document.addEventListener("submit", function (event) {
        var form = event.target;
        var message = form.getAttribute && form.getAttribute("data-confirm");
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // <input type="file" data-preview="#img-id"> shows the chosen image before it is uploaded.
    document.addEventListener("change", function (event) {
        var input = event.target;
        var selector = input.getAttribute && input.getAttribute("data-preview");
        if (!selector || !input.files || !input.files[0]) {
            return;
        }

        var preview = document.querySelector(selector);
        if (preview) {
            preview.src = URL.createObjectURL(input.files[0]);
            preview.hidden = false;
        }
    });

    // Results grid: positions only apply to Finished results.
    document.addEventListener("change", function (event) {
        var select = event.target;
        if (!select.matches || !select.matches("[data-finish-status]")) {
            return;
        }

        var row = select.closest("form, tr");
        var finished = select.value === "Finished";
        row.querySelectorAll("[data-finished-only]").forEach(function (field) {
            field.disabled = !finished;
            if (!finished) {
                field.value = "";
            }
        });
    });
})();
