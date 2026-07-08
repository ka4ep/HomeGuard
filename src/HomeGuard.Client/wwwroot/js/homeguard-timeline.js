// homeguard-timeline.js
// Thin wrapper around vis-timeline for Blazor JS interop.

window.homeGuardTimeline = {
    _instances: {},

    create(elementId, itemsJson, optionsJson, groupsJson) {
        const container = document.getElementById(elementId);
        if (!container) return;

        const rawItems = this._cleanItems(itemsJson);
        const options = JSON.parse(optionsJson);

        // ↓ Диагностика — убрать когда всё заработает
        console.log('[timeline] items:', rawItems.length, rawItems);

        const items = new vis.DataSet(rawItems);

        let timeline, groups = null;

        if (groupsJson) {
            const rawGroups = JSON.parse(groupsJson).map(g => ({
                ...g,
                showNested: true,   // ← разворачиваем вложенные группы сразу
            }));
            console.log('[timeline] groups:', rawGroups.length, rawGroups);
            groups = new vis.DataSet(rawGroups);
            timeline = new vis.Timeline(container, items, groups, options);
        } else {
            timeline = new vis.Timeline(container, items, options);
        }

        this._instances[elementId] = { timeline, items, groups };
    },

    updateItemsAndGroups(elementId, itemsJson, groupsJson) {
        const inst = this._instances[elementId];
        if (!inst) return;

        inst.items.clear();
        inst.items.add(this._cleanItems(itemsJson));

        if (inst.groups) {
            inst.groups.clear();
            if (groupsJson) inst.groups.add(JSON.parse(groupsJson));
        }
    },

    fit(elementId) {
        this._instances[elementId]?.timeline.fit();
    },

    focusToday(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.timeline.moveTo(new Date());
    },

    destroy(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.timeline.destroy();
        delete this._instances[elementId];
    },

    // Убирает null/undefined из каждого item-объекта
    _cleanItems(json) {
        return JSON.parse(json).map(item => {
            const clean = {};
            for (const [k, v] of Object.entries(item))
                if (v !== null && v !== undefined) clean[k] = v;
            return clean;
        });
    },
};
