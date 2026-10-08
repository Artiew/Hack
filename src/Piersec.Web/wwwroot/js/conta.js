(function () {
    "use strict";
    var conta = document.querySelector(".conta-menu");
    if (!conta) return;
    document.addEventListener("click", function (e) {
        if (conta.open && !conta.contains(e.target)) conta.open = false;
    });
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && conta.open) {
            e.preventDefault();
            conta.open = false;
            conta.querySelector("summary").focus();
        }
    });
})();
