/* ==========================================================================
   InkMath · ui-modal.js
   Reemplazo visual de alert / confirm / prompt. No toca la lógica de negocio.

   Uso:
     await UI.alert('Perfil actualizado', { type: 'success' });
     if (!(await UI.confirm('¿Eliminar 3 aulas?', { danger: true, confirmText: 'Eliminar' }))) return;
     const motivo = await UI.prompt('Justificación', { min: 10, max: 500, multiline: true });
     UI.toast('Guardado', 'success');

   Opcional (cero cambios en tus JS para los alert): UI.installAlertShim();
   Todo el texto se inserta con textContent (nunca innerHTML) → sin riesgo de XSS.
   ========================================================================== */
(function () {
    'use strict';

    const ICONS = {
        info:     '<path d="M12 10v7M12 7h.01" />',
        success:  '<path d="M5 12.5l4.5 4.5L19 7.5" />',
        warning:  '<path d="M12 8v5M12 16.5h.01" />',
        danger:   '<path d="M7 7l10 10M17 7L7 17" />',
        question: '<path d="M9.5 9.2a2.7 2.7 0 1 1 3.9 2.4c-.9.5-1.4 1-1.4 2M12 17h.01" />'
    };
    const TITLES = { info: 'Aviso', success: '¡Listo!', warning: 'Atención', danger: 'Ocurrió un problema', question: 'Confirma, por favor' };

    function svg(type) {
        const ns = 'http://www.w3.org/2000/svg';
        const el = document.createElementNS(ns, 'svg');
        el.setAttribute('viewBox', '0 0 24 24');
        el.setAttribute('fill', 'none');
        el.setAttribute('stroke', 'currentColor');
        el.setAttribute('stroke-width', '2.6');
        el.setAttribute('stroke-linecap', 'round');
        el.setAttribute('stroke-linejoin', 'round');
        el.setAttribute('aria-hidden', 'true');
        el.innerHTML = ICONS[type] || ICONS.info; // constante interna, no viene del usuario
        return el;
    }

    function make(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    // Infiere el tipo a partir del mensaje cuando migras un alert() sin cambiar nada más
    function guessType(msg) {
        const m = String(msg).toLowerCase();
        if (/(error|no se pudo|no pudieron|inv[aá]lid|no tiene|permiso|expirad)/.test(m)) return 'danger';
        if (/(correctamente|con [eé]xito|obtenida|realizada|desactivada|¡)/.test(m)) return 'success';
        if (/(selecciona|debe|obligatori|ingresa|al menos|se requieren)/.test(m)) return 'warning';
        return 'info';
    }

    let openCount = 0;

    function dialog({ type = 'info', title, message, buttons, field }) {
        return new Promise(resolve => {
            const previouslyFocused = document.activeElement;
            const overlay = make('div', 'ui-overlay');
            const box = make('div', `ui-dialog ui-dialog--${type}`);
            box.setAttribute('role', type === 'danger' ? 'alertdialog' : 'dialog');
            box.setAttribute('aria-modal', 'true');

            const titleId = 'ui-t-' + Math.random().toString(36).slice(2, 8);
            const msgId = 'ui-m-' + Math.random().toString(36).slice(2, 8);
            box.setAttribute('aria-labelledby', titleId);
            box.setAttribute('aria-describedby', msgId);

            const icon = make('div', 'ui-icon'); icon.appendChild(svg(type));
            const h = make('h2', 'ui-title', title || TITLES[type]); h.id = titleId;
            const p = make('p', 'ui-message', message); p.id = msgId;
            box.append(icon, h, p);

            let input, errEl, countEl;
            if (field) {
                const wrap = make('div', 'ui-field');
                input = field.multiline ? make('textarea', 'ui-input') : make('input', 'ui-input');
                if (field.multiline) input.rows = 4; else input.type = 'text';
                input.placeholder = field.placeholder || '';
                if (field.max) input.maxLength = field.max;
                input.setAttribute('aria-label', title || message);
                const meta = make('div', 'ui-counter');
                errEl = make('span', 'ui-error'); errEl.setAttribute('role', 'alert');
                countEl = make('span');
                meta.append(errEl, countEl);
                wrap.append(input, meta);
                box.appendChild(wrap);
                const upd = () => {
                    const n = input.value.trim().length;
                    countEl.textContent = field.max ? `${n}/${field.max}` : `${n}`;
                    if (input.classList.contains('is-invalid')) { input.classList.remove('is-invalid'); errEl.textContent = ''; }
                };
                input.addEventListener('input', upd); upd();
            }

            const actions = make('div', 'ui-actions');
            let cleanupDone = false;
            const close = value => {
                if (cleanupDone) return; cleanupDone = true;
                overlay.classList.remove('is-open');
                document.removeEventListener('keydown', onKey, true);
                setTimeout(() => {
                    overlay.remove();
                    if (--openCount === 0) document.body.style.overflow = '';
                    if (previouslyFocused && previouslyFocused.focus) previouslyFocused.focus();
                    resolve(value);
                }, 180);
            };

            const btnEls = buttons.map(b => {
                const el = make('button', b.cls, b.label);
                el.type = 'button';
                el.addEventListener('click', () => {
                    if (b.validate && field) {
                        const v = input.value.trim();
                        if (field.min && v.length < field.min) { input.classList.add('is-invalid'); errEl.textContent = `Escribe al menos ${field.min} caracteres.`; input.focus(); return; }
                        if (field.max && v.length > field.max) { input.classList.add('is-invalid'); errEl.textContent = `Máximo ${field.max} caracteres.`; input.focus(); return; }
                        if (field.required && !v) { input.classList.add('is-invalid'); errEl.textContent = 'Este campo es obligatorio.'; input.focus(); return; }
                        return close(v);
                    }
                    close(b.value);
                });
                actions.appendChild(el);
                return el;
            });
            box.appendChild(actions);
            overlay.appendChild(box);

            const cancelValue = buttons.find(b => b.isCancel)?.value ?? null;
            function onKey(e) {
                if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(cancelValue); }
                else if (e.key === 'Tab') { // atrapa el foco dentro del modal
                    const f = [...box.querySelectorAll('button, input, textarea')];
                    if (!f.length) return;
                    const first = f[0], last = f[f.length - 1];
                    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
                    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
                } else if (e.key === 'Enter' && input && !field.multiline && document.activeElement === input) {
                    e.preventDefault(); btnEls.find((_, i) => buttons[i].validate)?.click();
                }
            }
            document.addEventListener('keydown', onKey, true);
            overlay.addEventListener('mousedown', e => { if (e.target === overlay) close(cancelValue); });

            document.body.appendChild(overlay);
            openCount++; document.body.style.overflow = 'hidden';
            requestAnimationFrame(() => {
                overlay.classList.add('is-open');
                (input || btnEls[btnEls.length - 1]).focus();
            });
        });
    }

    const UI = {
        alert(message, opts = {}) {
            const type = opts.type || guessType(message);
            return dialog({
                type, title: opts.title, message: String(message ?? ''),
                buttons: [{ label: opts.okText || 'Entendido', value: true, cls: 'btn-primary' }]
            });
        },
        confirm(message, opts = {}) {
            const danger = !!opts.danger;
            return dialog({
                type: danger ? 'danger' : (opts.type || 'question'), title: opts.title, message: String(message ?? ''),
                buttons: [
                    { label: opts.cancelText || 'Cancelar', value: false, cls: 'btn-secondary', isCancel: true },
                    { label: opts.confirmText || 'Aceptar', value: true, cls: danger ? 'btn-primary btn-danger' : 'btn-primary' }
                ]
            });
        },
        /** Devuelve el texto (trim) o null si se cancela. Valida min/max/required dentro del modal. */
        prompt(message, opts = {}) {
            return dialog({
                type: opts.type || 'question', title: opts.title, message: String(message ?? ''),
                field: { min: opts.min, max: opts.max, required: opts.required !== false, multiline: !!opts.multiline, placeholder: opts.placeholder },
                buttons: [
                    { label: opts.cancelText || 'Cancelar', value: null, cls: 'btn-secondary', isCancel: true },
                    { label: opts.confirmText || 'Aceptar', cls: 'btn-primary', validate: true }
                ]
            });
        },
        toast(message, type = 'info', ms = 4200) {
            let host = document.querySelector('.ui-toasts');
            if (!host) { host = make('div', 'ui-toasts'); host.setAttribute('aria-live', 'polite'); document.body.appendChild(host); }
            const t = make('div', `ui-toast ui-toast--${type}`);
            t.setAttribute('role', type === 'danger' ? 'alert' : 'status');
            const icon = make('div', 'ui-icon'); icon.appendChild(svg(type));
            t.append(icon, make('span', null, String(message ?? '')));
            host.appendChild(t);
            const remove = () => { t.classList.add('is-leaving'); setTimeout(() => t.remove(), 260); };
            setTimeout(remove, ms);
            t.addEventListener('click', remove);
        },
        /** Hace que los alert('...') existentes usen el modal bonito sin editar cada llamada. */
        installAlertShim() { window.alert = msg => { UI.alert(msg); }; }
    };

    window.UI = UI;

    // <script src="js/ui-modal.js" data-alert-shim></script>  -> los alert() existentes usan el modal
    if (document.currentScript && document.currentScript.hasAttribute('data-alert-shim')) UI.installAlertShim();
})();
