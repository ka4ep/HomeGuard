// homeguard-timeline.js
// Thin wrapper around vis-timeline for Blazor JS interop.
// vis-timeline is loaded from CDN in index.html.

window.homeGuardTimeline = {
    _instances: {},

    // Create a timeline in the element with the given id.
    create(elementId, itemsJson, optionsJson) {
        const container = document.getElementById(elementId);
        if (!container) return;

        const items   = new vis.DataSet(JSON.parse(itemsJson));
        const options = JSON.parse(optionsJson);

        const timeline = new vis.Timeline(container, items, options);
        this._instances[elementId] = { timeline, items };
    },

    // Replace all items in an existing timeline.
    updateItems(elementId, itemsJson) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.items.clear();
        inst.items.add(JSON.parse(itemsJson));
        inst.timeline.fit();
    },

    // Fit the visible window to the items.
    fit(elementId) {
        this._instances[elementId]?.timeline.fit();
    },

    // Move window to today.
    focusToday(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        const now = new Date();
        inst.timeline.moveTo(now);
    },

    destroy(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.timeline.destroy();
        delete this._instances[elementId];
    },
};
