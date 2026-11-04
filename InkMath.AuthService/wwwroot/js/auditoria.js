document.addEventListener('DOMContentLoaded', () => {
    'use strict';

    const $ = id => document.getElementById(id);
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
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

    const ENDPOINT = '/api/Dashboard/auditoria-mongo';
    let auditoriaDatos = [];

    // El log no tiene campo de resultado: se deduce de la acción
    function esFallido(r) {
        const acc = String(pick(r, 'accion', 'Accion') || '').toUpperCase();
        return acc.includes('FALLIDO') || acc.includes('DENEGADO') || acc.includes('ERROR');
    }

    function pintarFila(r) {
        const fecha = pick(r, 'fechaEvento', 'FechaEvento', 'fecha');
        const usuario = pick(r, 'jwtUsuarioEjecutor', 'JwtUsuarioEjecutor', 'usuario') || 'Sistema';
        const rol = pick(r, 'rolAuth', 'RolAuth', 'rol') || 'Admin';
        const accion = pick(r, 'accion', 'Accion') || 'EVENTO';
        const modulo = pick(r, 'modulo', 'Modulo') || 'GENERAL';
        const detalle = pick(r, 'justificacionCambio', 'JustificacionCambio', 'detalle') || 'Sin detalles adicionales';
        const exito = !esFallido(r);

        return `
        <tr class="table-row-animated">
            <td>${esc(fmtFecha(fecha))}</td>
            <td><strong>${esc(usuario)}</strong></td>
            <td class="text-center">${esc(rol)}</td>
            <td>${esc(accion)}</td>
            <td>${esc(modulo)}</td>
            <td class="cell-ellipsis" title="${esc(detalle)}">${esc(detalle)}</td>
            <td class="text-center"><span class="badge ${exito ? 'badge-success' : 'badge-danger'}">${exito ? 'Exitoso' : 'Fallido'}</span></td>
        </tr>`;
    }

    function renderizarTabla(datos) {
        const tbody = $('auditoriaBody');
        if (!datos.length) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7">
                        <div class="empty-state">
                            No se encontraron registros de auditoría para este periodo.
                        </div>
                    </td>
                </tr>`;
            return;
        }
        tbody.innerHTML = datos.map(pintarFila).join('');
    }

    function actualizarMetricas(datos) {
        const audEventos = $('audEventos');
        if (audEventos) {
            audEventos.textContent = datos.length;
            audEventos.classList.remove('is-empty');
        }

        const fallidos = datos.filter(d => {
            const acc = String(pick(d, 'accion', 'Accion') || '').toUpperCase();
            return acc.includes('FALLIDO') || acc.includes('LOGIN_FAIL');
        }).length;

        const audFallidos = $('audFallidos');
        if (audFallidos) {
            audFallidos.textContent = fallidos;
            audFallidos.classList.remove('is-empty');
        }

        const anulacionesTests = datos.filter(d => {
            const acc = String(pick(d, 'accion', 'Accion') || '').toUpperCase();
            const mod = String(pick(d, 'modulo', 'Modulo') || '').toUpperCase();
            return (acc.includes('ANULACION') || acc.includes('ANUL')) && (mod.includes('TEST') || mod.includes('EVALUACION'));
        }).length;

        const audAnulaciones = $('audAnulaciones');
        if (audAnulaciones) {
            audAnulaciones.textContent = anulacionesTests;
            audAnulaciones.classList.remove('is-empty');
        }
    }

    // Llena el select de módulos con los que realmente existen en los datos
    function poblarModulos(datos) {
        const sel = $('audModulo');
        if (!sel) return;
        const actual = sel.value;
        const mods = [...new Set(datos.map(d => String(pick(d, 'modulo', 'Modulo') || '').trim()).filter(Boolean))].sort();
        sel.innerHTML = '<option value="">Todos</option>' + mods.map(m => `<option value="${esc(m)}">${esc(m)}</option>`).join('');
        if (mods.includes(actual)) sel.value = actual;
    }

    // Filtros locales adicionales (Usuario, Acción, Módulo) sobre los datos ya descargados del servidor
    function aplicarFiltrosLocales() {
        const usuarioFiltro = $('audUsuario').value.trim();
        const accionTipo = $('audAccion').value;
        const moduloFiltro = $('audModulo').value;

        const filtrados = auditoriaDatos.filter(d => {
            if (usuarioFiltro) {
                const usuId = String(pick(d, 'jwtUsuarioEjecutor', 'JwtUsuarioEjecutor', 'usuarioId', 'UsuarioId') || '');
                if (!usuId.includes(usuarioFiltro)) return false;
            }

            if (accionTipo) {
                const acc = String(pick(d, 'accion', 'Accion') || '').toUpperCase();
                if (accionTipo === 'login' && !acc.includes('LOGIN')) return false;
                if (accionTipo === 'creacion' && !acc.includes('CREAR') && !acc.includes('CREACION')) return false;
                if (accionTipo === 'modificacion' && !acc.includes('MODIFICA') && !acc.includes('ACTUALIZA')) return false;
                if (accionTipo === 'anulacion' && !['ANUL', 'ELIMINAR', 'SALIR', 'BAJA'].some(k => acc.includes(k))) return false;
                if (accionTipo === 'acceso_denegado' && !acc.includes('DENEGADO')) return false;
            }

            if (moduloFiltro) {
                const mod = String(pick(d, 'modulo', 'Modulo') || '').trim();
                if (mod !== moduloFiltro) return false;
            }

            return true;
        });

        renderizarTabla(filtrados);
    }

    // Consulta al backend con el rango de fechas (yyyy-MM-dd, día de Guatemala).
    // Sin fechas, el backend devuelve los logs de hoy.
    async function cargarAuditoria() {
        const tbody = $('auditoriaBody');
        tbody.innerHTML = '<tr><td colspan="7" class="text-center" style="padding:30px;color:#94A3B8;">Consultando auditoría...</td></tr>';

        try {
            const desde = $('audDesde').value;
            const hasta = $('audHasta').value;

            const params = new URLSearchParams();
            if (desde) params.append('fechaInicio', desde);
            if (hasta) params.append('fechaFin', hasta);

            const url = `${ENDPOINT}${params.toString() ? '?' + params.toString() : ''}`;
            const res = await api(url);

            if (!res.ok) {
                auditoriaDatos = [];
                tbody.innerHTML = '<tr><td colspan="7" class="text-center" style="padding:30px;color:#EF4444;">Error al cargar la auditoría.</td></tr>';
                return;
            }

            auditoriaDatos = await res.json();
            actualizarMetricas(auditoriaDatos);
            poblarModulos(auditoriaDatos);
            aplicarFiltrosLocales(); // Respeta usuario/acción/módulo ya seleccionados

        } catch (err) {
            console.error(err);
            tbody.innerHTML = '<tr><td colspan="7" class="text-center" style="padding:30px;color:#EF4444;">Error de red al conectar con el servidor.</td></tr>';
        }
    }

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

            const adminMsg = $('adminMsg');
            if (adminMsg) adminMsg.hidden = true;

            const fieldset = document.querySelector('#formAuditoria fieldset');
            if (fieldset) fieldset.disabled = false;

            // Al buscar: vuelve a consultar el servidor por rango de fechas y aplica los filtros locales
            const formAuditoria = $('formAuditoria');
            if (formAuditoria) {
                formAuditoria.addEventListener('submit', async e => {
                    e.preventDefault();
                    await cargarAuditoria();
                });
            }

            // Carga inicial sin fechas: trae los registros del día actual
            await cargarAuditoria();

        } catch (err) {
            console.error(err);
        }
    }

    iniciar();
});