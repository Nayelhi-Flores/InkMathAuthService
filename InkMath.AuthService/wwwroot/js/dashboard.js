document.addEventListener('DOMContentLoaded', () => {
    'use strict';

    const $ = id => document.getElementById(id);
    // Acepta camelCase o snake_case por resiliencia (igual que admin.js)
    const pick = (o, ...keys) => { for (const k of keys) if (o && o[k] !== undefined && o[k] !== null) return o[k]; return undefined; };
    const fmtNum = n => Number(n || 0).toLocaleString('es-GT');

    async function api(url, options = {}) {
        const res = await fetch(url, {
            credentials: 'same-origin',
            ...options,
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(options.headers || {}) }
        });
        if (res.status === 401) { window.location.href = '/'; throw new Error('401'); }
        return res;
    }

    // ---------- Mensajes ----------
    const msg = $('adminMsg');
    function mostrarMensaje(texto, tipo = 'info') {
        msg.textContent = texto;
        msg.className = `admin-msg admin-msg--${tipo}`;
        msg.hidden = false;
    }
    const ocultarMensaje = () => { msg.hidden = true; msg.textContent = ''; };

    async function pedirJson(url) {
        const res = await api(url);
        if (res.status === 403) throw new Error('403');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
    }

    // =====================================================================
    //  KPIs  ·  GET /api/Dashboard/kpis
    //  { maestrosActivos, estudiantesTotales, testsActivos, suscripcionesPremium }
    // =====================================================================
    const KPI_IDS = { maestrosActivos: 'kpiMaestros', estudiantesTotales: 'kpiEstudiantes', testsActivos: 'kpiTests', suscripcionesPremium: 'kpiPremium' };

    async function cargarKpis() {
        const d = await pedirJson('/api/Dashboard/kpis');
        Object.entries(KPI_IDS).forEach(([campo, id]) => {
            const el = $(id);
            el.textContent = fmtNum(pick(d, campo));
            el.classList.remove('is-empty');
        });
    }

    // =====================================================================
    //  BARRAS  ·  GET /api/Dashboard/intentos-diarios  ->  [{ fecha, cantidad }]
    //  El API solo devuelve los días con intentos: aquí se rellenan los 7 días con 0.
    //  Se usa UTC porque el backend agrupa con UtcNow.
    // =====================================================================
    const DIAS = 7;
    const DAY_MS = 86400000;
    const claveDia = d => d.toISOString().slice(0, 10);
    const etiquetaDia = d => {
        const s = d.toLocaleDateString('es-GT', { weekday: 'short', timeZone: 'UTC' }).replace('.', '');
        return s.charAt(0).toUpperCase() + s.slice(1);
    };

    function ultimosDias() {
        const h = new Date();
        const hoy = Date.UTC(h.getUTCFullYear(), h.getUTCMonth(), h.getUTCDate());
        return Array.from({ length: DIAS }, (_, i) => new Date(hoy - (DIAS - 1 - i) * DAY_MS));
    }

    function pintarBarras(datos) {
        const porDia = new Map(datos.map(r => [String(pick(r, 'fecha') ?? '').slice(0, 10), Number(pick(r, 'cantidad') ?? 0)]));
        const dias = ultimosDias();
        const valores = dias.map(d => porDia.get(claveDia(d)) || 0);
        const max = Math.max(...valores);
        const total = valores.reduce((a, b) => a + b, 0);

        const chart = $('barsChart'), labels = $('barsLabels'), vacio = $('barsEmpty');
        chart.replaceChildren(); labels.replaceChildren();
        $('barsTotal').textContent = `${fmtNum(total)} intento${total === 1 ? '' : 's'}`;

        const sinDatos = total === 0;
        chart.hidden = labels.hidden = sinDatos;
        vacio.hidden = !sinDatos;
        if (sinDatos) { chart.setAttribute('aria-label', 'Sin intentos de test en los últimos 7 días'); return; }

        dias.forEach((d, i) => {
            const v = valores[i];
            const bar = document.createElement('div');
            bar.className = 'bar' + (v === max ? ' bar--top' : '');
            bar.style.setProperty('--h', `${Math.max((v / max) * 100, v > 0 ? 6 : 2)}%`);
            bar.title = `${etiquetaDia(d)} ${claveDia(d)}: ${v} intento(s)`;
            const num = document.createElement('span');
            num.className = 'bar-value'; num.textContent = v;
            bar.appendChild(num);
            chart.appendChild(bar);

            const lbl = document.createElement('span');
            lbl.textContent = etiquetaDia(d);
            labels.appendChild(lbl);
        });
        chart.setAttribute('aria-label', 'Intentos de test por día: ' + dias.map((d, i) => `${etiquetaDia(d)} ${valores[i]}`).join(', '));
    }

    async function cargarBarras() { pintarBarras(await pedirJson('/api/Dashboard/intentos-diarios')); }

    // =====================================================================
    //  DONA  ·  GET /api/Dashboard/usuarios-por-rol  ->  [{ roleId, total }]
    //  Orden del degradado (ver admin.css .donut): azul -> naranja -> amarillo
    // =====================================================================
    const ROLES = [
        { id: 3, nombre: 'Estudiantes',     color: 'var(--blue-500)' },
        { id: 2, nombre: 'Maestros',        color: 'var(--orange-400)' },
        { id: 1, nombre: 'Administradores', color: 'var(--yellow-400)' }
    ];

    function pintarDona(datos) {
        const porRol = new Map(datos.map(r => [Number(pick(r, 'roleId', 'RoleId', 'role_id')), Number(pick(r, 'total') ?? 0)]));
        const cantidades = ROLES.map(r => porRol.get(r.id) || 0);
        const total = cantidades.reduce((a, b) => a + b, 0);

        const dona = $('donutRoles'), lista = $('legendRoles');
        lista.replaceChildren();
        dona.dataset.total = fmtNum(total);
        dona.classList.toggle('donut--empty', total === 0);
        dona.setAttribute('aria-label', 'Usuarios por rol: ' + ROLES.map((r, i) => `${r.nombre} ${cantidades[i]}`).join(', '));

        if (total > 0) {
            const a = (cantidades[0] / total) * 100;
            const b = a + (cantidades[1] / total) * 100;
            dona.style.setProperty('--a', `${a}%`);
            dona.style.setProperty('--b', `${b}%`);
        }

        ROLES.forEach((r, i) => {
            const li = document.createElement('li');
            const dot = document.createElement('i'); dot.style.setProperty('--c', r.color);
            const nombre = document.createElement('span'); nombre.textContent = r.nombre;
            const cuenta = document.createElement('span'); cuenta.className = 'legend-count'; cuenta.textContent = fmtNum(cantidades[i]);
            const pct = document.createElement('span'); pct.className = 'legend-pct';
            pct.textContent = total ? `${Math.round((cantidades[i] / total) * 100)}%` : '—';
            li.append(dot, nombre, cuenta, pct);
            lista.appendChild(li);
        });
    }

    async function cargarRoles() { pintarDona(await pedirJson('/api/Dashboard/usuarios-por-rol')); }

    // =====================================================================
    //  Carga de todo el dashboard: si un bloque falla, los demás se muestran igual
    // =====================================================================
    async function cargarDashboard() {
        ocultarMensaje();
        const btn = $('btnActualizarDashboard');
        btn.disabled = true;
        ['panelBarras', 'panelRoles'].forEach(id => $(id).classList.add('is-loading'));

        const resultados = await Promise.allSettled([cargarKpis(), cargarBarras(), cargarRoles()]);

        ['panelBarras', 'panelRoles'].forEach(id => $(id).classList.remove('is-loading'));
        btn.disabled = false;

        const fallos = resultados.filter(r => r.status === 'rejected');
        if (fallos.length) {
            fallos.forEach(f => console.error(f.reason));
            const prohibido = fallos.some(f => f.reason?.message === '403');
            mostrarMensaje(prohibido
                ? 'Tu rol no tiene permiso para ver estos indicadores.'
                : 'No se pudieron cargar algunos indicadores. Pulsa "Actualizar datos" para reintentar.', 'error');
        }
    }

    $('btnActualizarDashboard').addEventListener('click', cargarDashboard);

    // =====================================================================
    //  Control de UI por rol (ergonomía; la seguridad real es el 403 del backend)
    // =====================================================================
    async function iniciar() {
        try {
            const res = await api('/api/Usuarios/me');
            if (!res.ok) { window.location.href = '/'; return; }
            const me = await res.json();
            const esAdmin = Number(me.rolId) === 1 || /administrador/i.test(me.rolNombre || '');
            if (!esAdmin) {
                $('accesoDenegado').hidden = false;
                const destino = Number(me.rolId) === 3 ? 'juego.html' : 'aulas';
                setTimeout(() => { window.location.href = destino; }, 3000);
                return;
            }
            $('panelAdmin').hidden = false;
            cargarDashboard();
        } catch (err) {
            console.error(err);
        }
    }

    iniciar();
});
