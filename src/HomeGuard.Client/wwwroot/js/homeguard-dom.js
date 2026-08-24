// Small generic DOM helpers that don't warrant their own JS module.
window.homeGuardDom = {
    // Used when a card expands to reveal content taller than the viewport (e.g. the
    // document/camera capture panel) — without this the newly-revealed preview can end
    // up below the fold with nothing telling the user to scroll for it.
    scrollIntoView(el) {
        el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    },
};
