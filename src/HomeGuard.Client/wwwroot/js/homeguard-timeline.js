// homeguard-timeline.js
// Self-contained pan/zoom timeline — no external charting library.
// One instance per host element; state lives in a closure per instance (see _createInstance).

window.homeGuardTimeline = {
    _instances: {},

    LW: 170,   // label column width
    RH: 50,    // row height
    BTN: 28,   // event button size (square)
    MS: 86400000,
    MIN_MPP: 86400000 / 8,
    MAX_MPP: 400 * 86400000,
    ZOOM_FACTOR: 1.2,

    create(elementId, rowsJson, dotNetRef) {
        const host = document.getElementById(elementId);
        if (!host) return;

        const inst = this._buildInstance(host);
        inst.dotNetRef = dotNetRef;
        inst.rows = this._parseRows(rowsJson);
        this._instances[elementId] = inst;
        this._fitToData(inst);
        inst.render();
    },

    update(elementId, rowsJson) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.rows = this._parseRows(rowsJson);
        inst.render();
    },

    fit(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        this._fitToData(inst);
        inst.render();
    },

    focusToday(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.vs = Date.now() - inst.tw * inst.mpp * 0.65;
        inst.render();
    },

    destroy(elementId) {
        const inst = this._instances[elementId];
        if (!inst) return;
        inst.resizeObserver.disconnect();
        window.removeEventListener('mousemove', inst.onWindowMouseMove);
        window.removeEventListener('mouseup', inst.onWindowMouseUp);
        inst.host.innerHTML = '';
        delete this._instances[elementId];
    },

    // ── Data ─────────────────────────────────────────────────────────────────

    _parseRows(json) {
        return JSON.parse(json).map(r => ({
            ...r,
            events: r.events.map(e => ({ ...e, dateObj: this._parseDate(e.date) })),
            marks: (r.marks || []).map(m => ({ ...m, dateObj: this._parseDate(m.date) })),
        }));
    },

    _parseDate(iso) {
        const [y, m, d] = iso.split('-').map(Number);
        return new Date(y, m - 1, d);
    },

    _fitToData(inst) {
        const dates = inst.rows.flatMap(r => r.events.map(e => e.dateObj.getTime()));
        if (dates.length === 0) {
            inst.vs = new Date().getTime() - 365 * this.MS;
            inst.mpp = (730 * this.MS) / Math.max(1, inst.tw);
            return;
        }
        const mn = Math.min(...dates), mx = Math.max(...dates);
        const pad = Math.max((mx - mn) * 0.08, 30 * this.MS);
        const span = Math.max(mx - mn + pad * 2, this.MS);
        inst.mpp = Math.max(this.MIN_MPP, Math.min(this.MAX_MPP, span / Math.max(1, inst.tw)));
        inst.vs = mn - pad;
    },

    // ── HTML helpers ─────────────────────────────────────────────────────────

    _esc(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    },

    _fmtSpan(days) {
        if (days <= 0) return '';
        if (days < 7) return `${days}d`;
        if (days < 30) return `${Math.round(days / 7)}w`;
        if (days < 365) return `${Math.round(days / 30)}mo`;
        const y = Math.floor(days / 365), m = Math.round((days % 365) / 30);
        return m ? `${y}y ${m}mo` : `${y}y`;
    },

    _fmtDate(d) {
        return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
    },

    _fmtNum(n, digits) {
        return Number(n).toLocaleString('en-GB', digits ? { minimumFractionDigits: digits, maximumFractionDigits: digits } : {});
    },

    // ── Instance construction ───────────────────────────────────────────────

    _buildInstance(host) {
        const LW = this.LW, BTN = this.BTN;
        host.classList.add('hg-tl');
        host.innerHTML = `
            <div class="hg-tl-grid">
                <div class="hg-tl-axis-row">
                    <div class="hg-tl-label-header"></div>
                    <div class="hg-tl-axis-area"></div>
                </div>
                <div class="hg-tl-rows"></div>
                <div class="hg-tl-cursor-overlay">
                    <div class="hg-tl-cursor-line"></div>
                    <div class="hg-tl-date-badge"></div>
                </div>
            </div>
            <div class="hg-tl-card">
                <div class="hg-tl-card-hdr">
                    <div class="hg-tl-card-hdr-text">
                        <div class="hg-tl-card-title"></div>
                        <div class="hg-tl-card-sub"></div>
                    </div>
                    <button type="button" class="hg-tl-card-close">✕</button>
                </div>
                <div class="hg-tl-card-body">
                    <div class="hg-tl-card-fields"></div>
                    <div class="hg-tl-card-actions"></div>
                </div>
            </div>`;

        const inst = {
            host,
            grid: host.querySelector('.hg-tl-grid'),
            labelHeader: host.querySelector('.hg-tl-label-header'),
            axisArea: host.querySelector('.hg-tl-axis-area'),
            rowsContainer: host.querySelector('.hg-tl-rows'),
            cursorOverlay: host.querySelector('.hg-tl-cursor-overlay'),
            cursorLine: host.querySelector('.hg-tl-cursor-line'),
            dateBadge: host.querySelector('.hg-tl-date-badge'),
            card: host.querySelector('.hg-tl-card'),
            cardTitle: host.querySelector('.hg-tl-card-title'),
            cardSub: host.querySelector('.hg-tl-card-sub'),
            cardFields: host.querySelector('.hg-tl-card-fields'),
            cardActions: host.querySelector('.hg-tl-card-actions'),
            cardClose: host.querySelector('.hg-tl-card-close'),
            rows: [],
            vs: 0, mpp: 1, tw: 900,
            selEv: null, hovId: null,
            dragging: false, dragAnchor: null, mousePos: null,
        };

        inst.labelHeader.style.width = inst.labelHeader.style.minWidth = LW + 'px';

        inst.render = () => this._render(inst);
        inst.updateOverlay = () => this._updateOverlay(inst);
        inst.updateBadge = () => this._updateBadge(inst);
        inst.updateCard = () => this._updateCard(inst);

        inst.resizeObserver = new ResizeObserver(() => inst.render());
        inst.resizeObserver.observe(inst.grid);

        inst.rowsContainer.addEventListener('mousedown', e => {
            if (e.button !== 0) return;
            const rect = inst.rowsContainer.getBoundingClientRect();
            const tx = e.clientX - rect.left - LW;
            if (tx < 0) return;
            inst.dragging = true;
            inst.dragAnchor = { x: e.clientX, vs: inst.vs };
            inst.rowsContainer.style.cursor = 'grabbing';
            e.preventDefault();
        });

        inst.onWindowMouseMove = e => {
            const rect = inst.rowsContainer.getBoundingClientRect();
            const tx = e.clientX - rect.left - LW;
            const inGrid = tx >= 0 && tx <= inst.tw && e.clientY >= rect.top && e.clientY <= rect.bottom;
            inst.mousePos = inGrid ? { tx } : null;
            if (inst.dragging && inst.dragAnchor) {
                inst.vs = inst.dragAnchor.vs - (e.clientX - inst.dragAnchor.x) * inst.mpp;
                inst.render();
            } else if (!inst.hovId) {
                inst.updateOverlay();
            }
        };
        window.addEventListener('mousemove', inst.onWindowMouseMove);

        inst.onWindowMouseUp = () => {
            if (inst.dragging) {
                inst.dragging = false;
                inst.dragAnchor = null;
                inst.rowsContainer.style.cursor = 'grab';
            }
        };
        window.addEventListener('mouseup', inst.onWindowMouseUp);

        inst.rowsContainer.addEventListener('mouseleave', () => {
            inst.mousePos = null;
            inst.updateOverlay();
        });

        inst.grid.addEventListener('wheel', e => {
            e.preventDefault();
            const rect = inst.rowsContainer.getBoundingClientRect();
            const tx = e.clientX - rect.left - LW;
            if (tx < 0 || tx > inst.tw) return;
            const anchor = inst.vs + tx * inst.mpp;
            const f = e.deltaY > 0 ? this.ZOOM_FACTOR : 1 / this.ZOOM_FACTOR;
            const nm = Math.max(this.MIN_MPP, Math.min(this.MAX_MPP, inst.mpp * f));
            inst.mpp = nm;
            inst.vs = anchor - tx * nm;
            inst.render();
        }, { passive: false });

        inst.cardClose.addEventListener('click', () => {
            inst.selEv = null;
            inst.updateCard();
            inst.render();
        });

        return inst;
    },

    // ── Rendering ────────────────────────────────────────────────────────────

    _toX(inst, date) {
        return (date.getTime() - inst.vs) / inst.mpp;
    },

    _adjPos(inst, events) {
        const pos = events.map(ev => this._toX(inst, ev.dateObj));
        for (let i = 1; i < pos.length; i++)
            if (pos[i] < pos[i - 1] + this.BTN + 1) pos[i] = pos[i - 1] + this.BTN + 1;
        return pos;
    },

    _getTicks(inst) {
        const ve = inst.vs + inst.mpp * inst.tw;
        const dv = (ve - inst.vs) / this.MS;
        const iv = dv < 30 ? 7 : dv < 90 ? 14 : dv < 200 ? 30 : dv < 400 ? 60 : dv < 800 ? 90 : dv < 1500 ? 180 : 365;
        const ivMs = iv * this.MS;
        const s = Math.ceil(inst.vs / ivMs) * ivMs;
        const raw = [];
        for (let t = s; t <= ve + ivMs; t += ivMs) raw.push(new Date(t));
        const filtered = [];
        let lastX = -Infinity;
        raw.forEach(t => {
            const x = this._toX(inst, t);
            if (x >= -20 && x <= inst.tw + 20 && x - lastX >= 64) {
                filtered.push(t);
                lastX = x;
            }
        });
        return filtered;
    },

    _fmtTick(inst, d) {
        const dv = inst.mpp * inst.tw / this.MS;
        if (dv > 1200) return `'${String(d.getFullYear()).slice(2)}`;
        if (dv > 400) return d.toLocaleDateString('en-GB', { month: '2-digit', year: '2-digit' });
        if (dv > 100) return d.toLocaleDateString('en-GB', { month: 'short', year: '2-digit' });
        return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
    },

    _render(inst) {
        const LW = this.LW, BTN = this.BTN, RH = this.RH;
        inst.tw = inst.grid.clientWidth - LW;
        if (inst.tw <= 0) return;

        const ticks = this._getTicks(inst);
        const today = new Date();
        const todayX = this._toX(inst, today);

        // axis
        inst.axisArea.innerHTML = '';
        ticks.forEach(t => {
            const x = this._toX(inst, t);
            const el = document.createElement('div');
            el.className = 'hg-tl-axis-tick';
            el.style.left = x + 'px';
            el.innerHTML = `<span>${this._fmtTick(inst, t)}</span><div></div>`;
            inst.axisArea.appendChild(el);
        });
        if (todayX >= 0 && todayX <= inst.tw) {
            const td = document.createElement('div');
            td.className = 'hg-tl-today-line';
            td.style.left = todayX + 'px';
            inst.axisArea.appendChild(td);
        }

        // rows
        inst.rowsContainer.innerHTML = '';

        inst.rows.forEach(row => {
            const rowEl = document.createElement('div');
            rowEl.className = 'hg-tl-row' + (row.group ? ' hg-tl-row-gstart' : '');

            const lc = document.createElement('div');
            lc.className = 'hg-tl-label-cell';
            lc.style.cssText = `width:${LW}px;min-width:${LW}px`;
            if (row.group) {
                const g = document.createElement('div');
                g.className = 'hg-tl-lgroup';
                g.textContent = row.group;
                lc.appendChild(g);
            }
            const n = document.createElement('div');
            n.className = 'hg-tl-lname';
            n.textContent = row.label;
            lc.appendChild(n);
            rowEl.appendChild(lc);

            const cell = document.createElement('div');
            cell.className = 'hg-tl-tcell';

            ticks.forEach(t => {
                const x = this._toX(inst, t);
                if (x < 0 || x > inst.tw) return;
                const tl = document.createElement('div');
                tl.className = 'hg-tl-tick-line';
                tl.style.left = x + 'px';
                cell.appendChild(tl);
            });
            if (todayX >= 0 && todayX <= inst.tw) {
                const td = document.createElement('div');
                td.className = 'hg-tl-today-line hg-tl-today-line-row';
                td.style.left = todayX + 'px';
                cell.appendChild(td);
            }

            // meter-reading ticks — thin marks along the bottom edge of the row
            row.marks.forEach((mk, mi) => {
                const x = this._toX(inst, mk.dateObj);
                if (x < -2 || x > inst.tw + 2) return;

                const mkEl = document.createElement('div');
                mkEl.className = 'hg-tl-meter-mark';
                mkEl.style.left = (x - 2) + 'px';
                mkEl.addEventListener('mouseenter', () => {
                    inst.hovId = `mk-${row.id}-${mi}`;
                    mkEl.classList.add('hg-tl-meter-mark-hov');
                    const src = mk.source === 'Auto' ? ' · auto' : '';
                    inst.dateBadge.textContent =
                        `${this._fmtDate(mk.dateObj)} · ${this._fmtNum(mk.value)}${mk.unit ? ' ' + mk.unit : ''}${src}`;
                    inst.dateBadge.style.display = 'block';
                });
                mkEl.addEventListener('mouseleave', () => {
                    inst.hovId = null;
                    mkEl.classList.remove('hg-tl-meter-mark-hov');
                    inst.updateBadge();
                });
                cell.appendChild(mkEl);
            });

            const pos = this._adjPos(inst, row.events);

            // interval blocks — strictly between consecutive buttons
            row.events.slice(0, -1).forEach((ev, i) => {
                const nev = row.events[i + 1];
                const x1 = pos[i] + BTN, x2 = pos[i + 1];
                const cx = Math.max(0, x1), ce = Math.min(inst.tw, x2), cw = ce - cx;
                if (cw < 1) return;

                const days = (nev.dateObj - ev.dateObj) / this.MS;
                const ts = this._fmtSpan(days);
                const delta = (ev.meterReading != null && nev.meterReading != null && nev.meterReading > ev.meterReading)
                    ? `+${this._fmtNum(nev.meterReading - ev.meterReading)}${nev.meterUnit ? ' ' + this._esc(nev.meterUnit) : ''}`
                    : null;
                const label = [ts, delta].filter(Boolean).join(' · ');
                const pred = nev.isPredicted;

                const ib = document.createElement('div');
                ib.className = 'hg-tl-iblock';
                ib.style.cssText = `left:${cx}px;width:${cw}px;top:${(RH - BTN) / 2}px;height:${BTN}px;`;
                const getBg = h => pred ? (h ? '#bbb' : '#e0e0e0') : (h ? row.color + '88' : row.color + '28');
                ib.style.background = getBg(false);

                if (cw > 44) {
                    const sp = document.createElement('span');
                    sp.className = 'hg-tl-ilabel';
                    sp.textContent = label;
                    ib.appendChild(sp);
                }
                ib.addEventListener('mouseenter', e => {
                    e.stopPropagation();
                    ib.style.background = getBg(true);
                    const sp = ib.querySelector('span');
                    if (sp) sp.style.fontWeight = '600';
                });
                ib.addEventListener('mouseleave', () => {
                    ib.style.background = getBg(false);
                    const sp = ib.querySelector('span');
                    if (sp) sp.style.fontWeight = '400';
                });
                cell.appendChild(ib);
            });

            // event buttons
            row.events.forEach((ev, i) => {
                const x = pos[i];
                if (x + BTN < -4 || x > inst.tw + 4) return;

                const isExpiry = ev.isExpiry;
                const isPlanned = ev.status === 'Planned';
                const isPredicted = ev.isPredicted;
                const isSel = inst.selEv && inst.selEv.ev.id === ev.id;
                const base = isPredicted ? '#aaa' : isExpiry ? '#ef4444' : row.color;

                const btn = document.createElement('div');
                btn.className = 'hg-tl-evbtn' + (isPredicted ? ' hg-tl-evbtn-pred' : '');
                btn.style.cssText = `left:${x}px;top:${(RH - BTN) / 2}px;width:${BTN}px;height:${BTN}px;border-color:${base};`;
                const fillAlpha = isSel ? '' : (isPredicted || isPlanned ? '88' : 'cc');
                btn.style.background = base + fillAlpha;
                if (isSel) {
                    btn.style.outline = '2px solid white';
                    btn.style.boxShadow = `0 0 0 3px ${base}55`;
                    btn.style.transform = 'scale(1.1)';
                }
                if (isExpiry) btn.textContent = '✕';

                btn.addEventListener('mouseenter', e => {
                    e.stopPropagation();
                    inst.hovId = ev.id;
                    btn.style.background = base;
                    btn.style.transform = 'scale(1.15)';
                    btn.style.boxShadow = `0 2px 8px ${base}66`;
                    inst.dateBadge.textContent = this._fmtDate(ev.dateObj) +
                        (ev.meterReading != null ? ` · ${this._fmtNum(ev.meterReading)}${ev.meterUnit ? ' ' + this._esc(ev.meterUnit) : ''}` : '');
                    inst.dateBadge.style.display = 'block';
                });
                btn.addEventListener('mouseleave', () => {
                    inst.hovId = null;
                    btn.style.background = isSel ? base : base + fillAlpha;
                    btn.style.transform = isSel ? 'scale(1.1)' : 'scale(1)';
                    btn.style.boxShadow = isSel ? `0 0 0 3px ${base}55` : 'none';
                    inst.updateBadge();
                });
                btn.addEventListener('mousedown', e => e.stopPropagation());
                btn.addEventListener('click', () => {
                    inst.hovId = null;
                    inst.selEv = (inst.selEv && inst.selEv.ev.id === ev.id) ? null : { ev, row };
                    inst.updateCard();
                    inst.render();
                });
                cell.appendChild(btn);
            });

            rowEl.appendChild(cell);
            inst.rowsContainer.appendChild(rowEl);
        });

        this._updateOverlay(inst);
    },

    _updateOverlay(inst) {
        const h = inst.rowsContainer.offsetHeight;
        inst.cursorOverlay.style.height = h + 'px';
        if (inst.mousePos && !inst.hovId) {
            inst.cursorLine.style.left = (this.LW + inst.mousePos.tx) + 'px';
            inst.cursorLine.style.height = h + 'px';
            inst.cursorLine.style.display = 'block';
        } else {
            inst.cursorLine.style.display = 'none';
        }
        this._updateBadge(inst);
    },

    _updateBadge(inst) {
        if (inst.hovId) return;
        if (inst.mousePos) {
            inst.dateBadge.style.display = 'block';
            inst.dateBadge.textContent = this._fmtDate(new Date(inst.vs + inst.mousePos.tx * inst.mpp));
        } else {
            inst.dateBadge.style.display = 'none';
        }
    },

    _updateCard(inst) {
        if (!inst.selEv) {
            inst.card.style.display = 'none';
            return;
        }
        const { ev, row } = inst.selEv;
        inst.card.style.display = 'block';
        inst.cardTitle.textContent = ev.title || row.label;
        inst.cardSub.textContent = ev.equipmentName || row.group || '';
        inst.cardSub.style.borderLeftColor = row.color;

        const items = [
            ['Date', this._fmtDate(ev.dateObj)],
        ];
        if (ev.isPredicted) items.push(['Status', 'Predicted']);
        else if (ev.status) items.push(['Status', ev.status]);
        if (ev.meterReading != null) items.push(['Meter reading', this._fmtNum(ev.meterReading) + (ev.meterUnit ? ' ' + ev.meterUnit : '')]);
        if (ev.cost != null) items.push(['Cost', this._fmtNum(ev.cost, 2) + ' €']);
        if (ev.serviceProvider) items.push(['Provider', ev.serviceProvider]);
        if (ev.isStart) items.push(['Event', 'Warranty start']);
        if (ev.isExpiry) items.push(['Event', 'Warranty expiry']);
        if (ev.notes) items.push(['Notes', ev.notes]);

        inst.cardFields.innerHTML = items.map(([k, v]) =>
            `<div class="hg-tl-cf"><div class="hg-tl-cf-lbl">${this._esc(k)}</div><div class="hg-tl-cf-val">${this._esc(v)}</div></div>`
        ).join('');

        inst.cardActions.innerHTML = '';

        if (ev.isPredicted && ev.ruleId) {
            this._addCardAction(inst, '⚡', 'Materialize now', () => {
                if (inst.dotNetRef) inst.dotNetRef.invokeMethodAsync('NotifyMaterializeRequested', ev.ruleId);
            });
            if (ev.equipmentId) {
                this._addCardAction(inst, '↻', 'Open recurring rule', `/equipment/${ev.equipmentId}?editRule=${ev.ruleId}`);
            }
        } else if (ev.recordId && ev.equipmentId) {
            this._addCardAction(inst, '✎', 'Edit record', `/equipment/${ev.equipmentId}?editService=${ev.recordId}`);
        }
    },

    // hrefOrClick: a URL string (renders an <a>) or a click handler function (renders a <button>).
    _addCardAction(inst, glyph, tooltip, hrefOrClick) {
        const isLink = typeof hrefOrClick === 'string';
        const el = document.createElement(isLink ? 'a' : 'button');
        if (isLink) el.href = hrefOrClick;
        else { el.type = 'button'; el.addEventListener('click', hrefOrClick); }
        el.className = 'hg-tl-card-action';
        el.title = tooltip;
        el.setAttribute('aria-label', tooltip);
        el.textContent = glyph;
        inst.cardActions.appendChild(el);
    },
};
