document.addEventListener('DOMContentLoaded', () => {
    'use strict';

    const PAGE_SIZE = 10;
    const $ = id => document.getElementById(id);

    // Todo texto que viene del servidor se escapa antes de insertarse en el DOM (OWASP A03).
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    // El API devuelve snake_case; se aceptan ambas variantes por resiliencia.
    const pick = (o, ...keys) => { for (const k of keys) if (o && o[k] !== undefined && o[k] !== null) return o[k]; return undefined; };
    const fmtFecha = d => d ? new Date(d).toLocaleString('es-GT', { dateStyle: 'short', timeStyle: 'short' }) : '—';

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

    async function leerError(res, porDefecto) {
        if (res.status === 403) return 'Tu rol no tiene permiso para realizar esta acción (403 Forbidden).';
        const data = await res.json().catch(() => null);
        const detalle = data?.mensaje
            || (data?.errors ? Object.values(data.errors).flat()[0] : null)
            || data?.title;
        return detalle || porDefecto;
    }

    // ---------- Paginación en cliente (mismos estilos que el resto de pantallas) ----------
    function pintarPaginacion(contenedor, estado, alCambiar) {
        const total = estado.datos.length;
        const paginas = Math.max(1, Math.ceil(total / PAGE_SIZE));
        const ini = total ? (estado.pagina - 1) * PAGE_SIZE + 1 : 0;
        const fin = Math.min(total, estado.pagina * PAGE_SIZE);
        contenedor.innerHTML = `
            <div class="pagination-info">Mostrando ${ini}-${fin} de ${total} registros</div>
            <div class="pagination-controls">
                <button type="button" class="pagination-arrow" data-nav="prev" aria-label="Página anterior" ${estado.pagina <= 1 ? 'disabled' : ''}>&lt;</button>
                <span class="page-text">Página</span>
                <button type="button" class="page-badge active" disabled>${estado.pagina}</button>
                <span class="page-text">de ${paginas}</span>
                <button type="button" class="pagination-arrow" data-nav="next" aria-label="Página siguiente" ${estado.pagina >= paginas ? 'disabled' : ''}>&gt;</button>
            </div>`;
        contenedor.onclick = e => {
            const b = e.target.closest('button[data-nav]');
            if (!b || b.disabled) return;
            estado.pagina += b.dataset.nav === 'next' ? 1 : -1;
            alCambiar();
        };
    }

    const filaVacia = (cols, texto) =>
        `<tr><td colspan="${cols}" class="text-center" style="padding:30px;color:#94A3B8;">${esc(texto)}</td></tr>`;

    // =====================================================================
    //  RANKING: la posición la calcula SQL Server con RANK(); aquí solo se muestra.
    // =====================================================================
    const ranking = { cargado: false };

    function medalla(pos) {
        const clase = pos >= 1 && pos <= 3 ? ` rank-${pos}` : '';
        return `<span class="rank-badge${clase}" aria-label="Posición ${pos}">${pos}</span>`;
    }

    async function cargarRanking() {
        const body = $('rankingBody');
        body.innerHTML = filaVacia(4, 'Cargando ranking...');
        try {
            const top = Number($('rankingTop').value) || 10;
            const res = await api(`/api/Dashboard/top-maestros?top=${top}`);
            if (!res.ok) {
                body.innerHTML = filaVacia(4, 'No se pudo cargar el ranking.');
                return mostrarMensaje(await leerError(res, 'No se pudo cargar el ranking.'), 'error');
            }
            const datos = await res.json();
            ranking.cargado = true;
            if (!datos.length) { body.innerHTML = filaVacia(4, 'Aún no hay maestros con preguntas activas.'); return; }
            body.innerHTML = datos.map(r => {
                const pos = Number(pick(r, 'posicion_ranking', 'posicionRanking'));
                return `
                <tr class="table-row-animated">
                    <td class="text-center">${medalla(pos)}</td>
                    <td><strong>${esc(pick(r, 'nombre'))}</strong></td>
                    <td class="text-center">${esc(pick(r, 'maestro_id', 'maestroId'))}</td>
                    <td class="text-center">${esc(pick(r, 'total_preguntas', 'totalPreguntas'))}</td>
                </tr>`;
            }).join('');
        } catch (err) {
            console.error(err);
            body.innerHTML = filaVacia(4, 'Error de red al cargar el ranking.');
        }
    }

    // =====================================================================
    //  REPORTE TRANSACCIONAL: exige al menos un filtro (también lo valida el backend con 400)
    // =====================================================================
    const reporte = { datos: [], pagina: 1, ultimaConsulta: null };

    function badgeEstado(activo) {
        return activo
            ? '<span class="badge badge-success">CREADO</span>'
            : '<span class="badge badge-danger">ANULADO</span>';
    }

    function filaTest(r, conAcciones) {
        const id = pick(r, 'id', 'testId');
        const nombre = pick(r, 'nombre_test', 'nombreTest', 'nombre');
        const maestro = pick(r, 'nombre_maestro', 'nombreMaestro');
        const maestroId = pick(r, 'maestro_id', 'maestroId');
        const activo = pick(r, 'esta_activo', 'estaActivo') !== false;
        const acciones = conAcciones
            ? `<td class="text-center">${activo
                ? `<button type="button" class="btn-danger-outline" data-action="anular" data-id="${esc(id)}" data-nombre="${esc(nombre)}" title="Anular test">
                        <img src="icons/bx-trash-alt.svg" alt="" class="btn-icon"><span>Anular</span>
                   </button>`
                : '<span class="subtitle">—</span>'}</td>`
            : '';
        return `
            <tr class="table-row-animated">
                <td class="text-center">${esc(id)}</td>
                <td><strong>${esc(nombre)}</strong></td>
                <td>${esc(maestro)} <span class="subtitle">(ID ${esc(maestroId)})</span></td>
                <td class="text-center">${fmtFecha(pick(r, 'creado_en', 'creadoEn'))}</td>
                <td class="text-center">${esc(pick(r, 'total_preguntas', 'totalPreguntas') ?? 0)}</td>
                <td class="text-center">${badgeEstado(activo)}</td>
                ${acciones}
            </tr>`;
    }

    function pintarReporte() {
        const body = $('reporteBody');
        const ini = (reporte.pagina - 1) * PAGE_SIZE;
        const pagina = reporte.datos.slice(ini, ini + PAGE_SIZE);
        body.innerHTML = pagina.length
            ? pagina.map(r => filaTest(r, true)).join('')
            : filaVacia(7, 'No se encontraron tests con los filtros indicados.');
        pintarPaginacion($('reportePag'), reporte, pintarReporte);
    }

    function leerFiltrosReporte() {
        const ini = $('repDesde').value, fin = $('repHasta').value;
        const maestro = $('repMaestro').value.trim(), estado = $('repEstado').value;

        if (!ini && !fin && !maestro && estado === '') {
            return { error: 'Debe indicar al menos un filtro (rango de fechas, ID de maestro o estado) para ejecutar la consulta.' };
        }
        if (ini && fin && fin < ini) return { error: 'La fecha fin no puede ser anterior a la fecha inicio.' };
        if (maestro && !/^[1-9]\d*$/.test(maestro)) return { error: 'El ID de maestro debe ser un entero positivo.' };

        const p = new URLSearchParams();
        if (ini) p.set('FechaInicio', `${ini}T00:00:00`);
        if (fin) p.set('FechaFin', `${fin}T23:59:59`);
        if (maestro) p.set('MaestroId', maestro);
        if (estado !== '') p.set('EstaActivo', estado);
        return { params: p };
    }

    async function ejecutarReporte(params) {
        const btn = $('btnReporteBuscar');
        btn.disabled = true;
        $('reporteBody').innerHTML = filaVacia(7, 'Consultando...');
        try {
            const res = await api(`/api/Dashboard/reporte-tests?${params.toString()}`);
            if (!res.ok) {
                reporte.datos = []; pintarReporte();
                return mostrarMensaje(await leerError(res, 'No se pudo ejecutar el reporte.'), 'error');
            }
            reporte.datos = await res.json();
            reporte.pagina = 1;
            reporte.ultimaConsulta = params;
            ocultarMensaje();
            pintarReporte();
        } catch (err) {
            console.error(err);
            mostrarMensaje('Error de red al ejecutar el reporte.', 'error');
        } finally { btn.disabled = false; }
    }

    $('formReporte').addEventListener('submit', e => {
        e.preventDefault();
        const f = leerFiltrosReporte();
        if (f.error) return mostrarMensaje(f.error, 'error');   // bloqueo en cliente; el backend responde 400 igualmente
        ejecutarReporte(f.params);
    });

    // =====================================================================
    //  AUDITORÍA: solo registros con Soft Delete (el backend fuerza EstaActivo = false)
    // =====================================================================
    const anulados = { datos: [], pagina: 1 };

    function pintarAnulados() {
        const body = $('anuladosBody');
        const ini = (anulados.pagina - 1) * PAGE_SIZE;
        const pagina = anulados.datos.slice(ini, ini + PAGE_SIZE);
        body.innerHTML = pagina.length
            ? pagina.map(r => filaTest(r, false)).join('')
            : filaVacia(6, 'No hay tests anulados.');
        pintarPaginacion($('anuladosPag'), anulados, pintarAnulados);
    }

    async function cargarAnulados() {
        const p = new URLSearchParams();
        const ini = $('anuDesde').value, fin = $('anuHasta').value;
        if (ini && fin && fin < ini) return mostrarMensaje('La fecha fin no puede ser anterior a la fecha inicio.', 'error');
        if (ini) p.set('FechaInicio', `${ini}T00:00:00`);
        if (fin) p.set('FechaFin', `${fin}T23:59:59`);
        $('anuladosBody').innerHTML = filaVacia(6, 'Consultando...');
        try {
            const res = await api(`/api/Dashboard/reporte-anulados${p.toString() ? '?' + p : ''}`);
            if (!res.ok) {
                anulados.datos = []; pintarAnulados();
                return mostrarMensaje(await leerError(res, 'No se pudo cargar el reporte de auditoría.'), 'error');
            }
            anulados.datos = await res.json();
            anulados.pagina = 1;
            anulados.cargado = true;
            ocultarMensaje();
            pintarAnulados();
        } catch (err) {
            console.error(err);
            mostrarMensaje('Error de red al cargar el reporte de auditoría.', 'error');
        }
    }

    $('formAnulados').addEventListener('submit', e => { e.preventDefault(); cargarAnulados(); });

    // =====================================================================
    //  ANULAR (DELETE = Soft Delete, con justificación obligatoria para la auditoría)
    // =====================================================================
    const modal = $('modalAnular'), txt = $('anularJustificacion');
    const btnConfirmar = $('btnConfirmarAnular'), errorBox = $('anularError');
    let testPendiente = null, disparador = null;

    const justificacionValida = () => txt.value.trim().length >= 10 && txt.value.trim().length <= 500;

    function abrirModal(id, nombre, origen) {
        testPendiente = Number(id);
        disparador = origen;
        $('anularInfo').textContent = `Test #${id}: ${nombre}`;
        txt.value = '';
        $('anularContador').textContent = '0 / 500';
        errorBox.hidden = true;
        btnConfirmar.disabled = true;
        modal.classList.add('active');
        txt.focus();
    }

    function cerrarModal() {
        modal.classList.remove('active');
        testPendiente = null;
        disparador?.focus();
    }

    txt.addEventListener('input', () => {
        $('anularContador').textContent = `${txt.value.length} / 500`;
        btnConfirmar.disabled = !justificacionValida();
    });
    $('btnCancelarAnular').addEventListener('click', cerrarModal);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && modal.classList.contains('active')) cerrarModal(); });

    btnConfirmar.addEventListener('click', async () => {
        if (!testPendiente || !justificacionValida()) return;
        btnConfirmar.disabled = true;
        errorBox.hidden = true;
        try {
            const res = await api(`/api/Tests/${testPendiente}`, {
                method: 'DELETE',
                body: JSON.stringify({ justificacion: txt.value.trim() })
            });
            if (!res.ok) {
                errorBox.textContent = await leerError(res, 'No se pudo anular el test.');
                errorBox.hidden = false;
                btnConfirmar.disabled = false;
                return;
            }
            const data = await res.json().catch(() => null);
            cerrarModal();
            mostrarMensaje(data?.mensaje || 'Test anulado correctamente.', 'ok');
            ranking.cargado = false;            // el ranking cambia al anular
            anulados.cargado = false;           // y el reporte de auditoría también
            if (reporte.ultimaConsulta) await ejecutarReporte(reporte.ultimaConsulta);
            mostrarMensaje(data?.mensaje || 'Test anulado correctamente.', 'ok');
        } catch (err) {
            console.error(err);
            errorBox.textContent = 'Error de red al anular el test.';
            errorBox.hidden = false;
            btnConfirmar.disabled = false;
        }
    });

    $('reporteBody').addEventListener('click', e => {
        const b = e.target.closest('button[data-action="anular"]');
        if (b) abrirModal(b.dataset.id, b.dataset.nombre, b);
    });

    // =====================================================================
    //  Pestañas (accesibles con teclado)
    // =====================================================================
    const tabs = [...document.querySelectorAll('.admin-tab')];
    const paneles = { ranking: $('panelRanking'), reporte: $('panelReporte'), anulados: $('panelAnulados') };

    function activarTab(nombre, enfocar = false) {
        tabs.forEach(t => {
            const activa = t.dataset.tab === nombre;
            t.setAttribute('aria-selected', String(activa));
            t.tabIndex = activa ? 0 : -1;
            if (activa && enfocar) t.focus();
        });
        Object.entries(paneles).forEach(([k, el]) => { el.hidden = k !== nombre; });
        if (nombre === 'ranking' && !ranking.cargado) cargarRanking();
        if (nombre === 'anulados' && !anulados.cargado) cargarAnulados();
    }

    tabs.forEach((t, i) => {
        t.addEventListener('click', () => { ocultarMensaje(); activarTab(t.dataset.tab); });
        t.addEventListener('keydown', e => {
            if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
            e.preventDefault();
            const sig = tabs[(i + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
            activarTab(sig.dataset.tab, true);
        });
    });

    $('btnRankingActualizar').addEventListener('click', () => { ocultarMensaje(); cargarRanking(); });
    $('rankingTop').addEventListener('change', cargarRanking);

    // =====================================================================
    //  Control de UI por rol: solo el Administrador ve este panel.
    //  (Es ergonomía; la seguridad real es el 403 que devuelve el backend.)
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
            pintarPaginacion($('reportePag'), reporte, pintarReporte);
            pintarPaginacion($('anuladosPag'), anulados, pintarAnulados);
            $('reporteBody').innerHTML = filaVacia(7, 'Indique al menos un filtro y pulse Buscar.');
            activarTab('ranking');
        } catch (err) {
            console.error(err);
        }
    }

    iniciar();
});
