document.addEventListener('DOMContentLoaded', () => {
    const LIMITE = 5;
    let pagina = 1, totalPaginas = 1, total = 0, hayMas = false;
    let cursores = [null];
    let debounce;

    const $ = id => document.getElementById(id);
    const tbody = $('aulasTableBody'), searchInput = $('searchInput'), selectAll = $('selectAllCheckbox');
    const aulaModal = $('aulaModal'), modalTitle = $('modalTitle'), aulaIdInput = $('aulaIdInput'), nombreAulaInput = $('nombreAulaInput');

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    async function api(url, options = {}) {
        const res = await fetch(url, {
            credentials: 'same-origin',
            ...options,
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(options.headers || {}) }
        });
        if (res.status === 401) { window.location.href = '/'; throw new Error('401'); }
        return res;
    }
    const mensajeDe = async (res, def) => (await res.json().catch(() => null))?.mensaje || def;

    async function fetchPagina(cursor) {
        const p = new URLSearchParams({ Limite: LIMITE });
        const q = searchInput.value.trim();
        if (q) p.set('Busqueda', q);
        if (cursor) { p.set('UltimoId', cursor.ultimoId); p.set('UltimaFecha', cursor.ultimaFecha); }
        const res = await api(`/api/Aulas/paginadas?${p}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
    }

    async function cargar(reset = false) {
        if (reset) { cursores = [null]; pagina = 1; }
        try {
            const r = await fetchPagina(cursores[pagina - 1]);
            const datos = r.datos || [];
            if (!datos.length && pagina > 1) { pagina--; return cargar(); }
            cursores.length = pagina;
            hayMas = !!r.tieneMasPaginas;
            if (hayMas) cursores[pagina] = { ultimoId: r.siguienteUltimoId, ultimaFecha: r.siguienteUltimaFecha };
            total = r.totalRegistros ?? datos.length;
            totalPaginas = Math.max(1, r.totalPaginas ?? 1);
            renderTabla(datos);
            renderPaginador(datos.length);
        } catch (err) {
            console.error(err);
            tbody.innerHTML = '<tr><td colspan="7" class="text-center" style="padding:30px;color:#EF4444;">Error al cargar las aulas.</td></tr>';
        }
    }

    async function irAPagina(n) {
        n = Math.min(Math.max(1, n), totalPaginas);
        try {
            while (cursores.length < n) {
                const ult = cursores.length;
                const r = await fetchPagina(cursores[ult - 1]);
                if (!r.tieneMasPaginas) break;
                cursores[ult] = { ultimoId: r.siguienteUltimoId, ultimaFecha: r.siguienteUltimaFecha };
            }
        } catch (err) { console.error(err); return; }
        pagina = Math.min(n, cursores.length);
        cargar();
    }

    function renderTabla(aulas) {
        selectAll.checked = false;
        if (!aulas.length) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-center" style="padding:30px;color:#94A3B8;">No se encontraron aulas.</td></tr>';
            return;
        }
        tbody.innerHTML = aulas.map(a => `
            <tr class="table-row-animated">
                <td class="col-checkbox"><input type="checkbox" class="aula-checkbox" value="${a.id}"></td>
                <td><strong>${esc(a.codigoAcceso || '---')}</strong></td>
                <td>${esc(a.nombre)}</td>
                <td class="text-center">${a.totalEstudiantes ?? 0}</td>
                <td class="text-center">${a.totalRecursos ?? 0}</td>
                <td class="text-center">${a.totalTests ?? 0}</td>
                <td class="text-center">
                    <div class="action-buttons">
                        <button class="btn-action view" data-action="ver" data-id="${a.id}" data-nombre="${esc(a.nombre)}" title="Ver"><img src="icons/bx-eye.svg" alt="Ver"></button>
                        <button class="btn-action edit" data-action="editar" data-id="${a.id}" data-nombre="${esc(a.nombre)}" title="Editar"><img src="icons/bx-edit.svg" alt="Editar"></button>
                    </div>
                </td>
            </tr>`).join('');
    }

    function renderPaginador(n) {
        $('paginationInfo').textContent = `Mostrando ${n} de ${total} aulas existentes`;
        $('btnFirstPage').className = pagina === 1 ? 'page-badge active' : 'page-badge-secondary';
        $('btnLastPage').textContent = totalPaginas;
        $('btnLastPage').className = (pagina === totalPaginas && totalPaginas > 1) ? 'page-badge active' : 'page-badge-secondary';
        $('totalPagesLabel').textContent = totalPaginas;
        $('pageSelect').innerHTML = Array.from({ length: totalPaginas }, (_, i) =>
            `<option value="${i + 1}" ${i + 1 === pagina ? 'selected' : ''}>${i + 1}</option>`).join('');
        $('btnPrevPage').disabled = pagina === 1;
        $('btnNextPage').disabled = !hayMas;
    }

    $('btnPrevPage').addEventListener('click', () => { if (pagina > 1) { pagina--; cargar(); } });
    $('btnNextPage').addEventListener('click', () => { if (hayMas) { pagina++; cargar(); } });
    $('btnFirstPage').addEventListener('click', () => irAPaginas(1));
    $('btnLastPage').addEventListener('click', () => irAPaginas(totalPaginas));
    $('pageSelect').addEventListener('change', e => irAPaginas(Number(e.target.value)));

    searchInput.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => cargar(true), 300); });
    $('btnSearch').addEventListener('click', () => cargar(true));

    selectAll.addEventListener('change', e => document.querySelectorAll('.aula-checkbox').forEach(cb => cb.checked = e.target.checked));
    const aulasSeleccionadas = () => [...document.querySelectorAll('.aula-checkbox:checked')].map(cb => Number(cb.value));

    // Referencias para la vista de página tipo Dashboard
    const vistaLista = $('vistaLista');
    const vistaDashboard = $('vistaDashboard');
    const bcListaAulas = $('bcListaAulas');
    const bcSepAula = $('bcSepAula');
    const bcDetalleAula = $('bcDetalleAula');

    const dashboardAulaNombre = $('dashboardAulaNombre');
    const dashboardAulaSub = $('dashboardAulaSub');
    const statTotalAlumnos = $('statTotalAlumnos');
    const statTotalTests = $('statTotalTests');
    const statTotalRecursos = $('statTotalRecursos');
    const tablaAlumnosDashboard = $('tablaAlumnosDashboard');
    const listaTestsDashboard = $('listaTestsDashboard');
    const listaRecursosDashboard = $('listaRecursosDashboard');

    let alumnosGlobal = [];
    let tieneTestsGlobal = false;

    // Definición correcta de la función que faltaba
    function renderTablaAlumnosPaginada() {
        tablaAlumnosDashboard.innerHTML = alumnosGlobal.length ? alumnosGlobal.map(a => {
            const badgeProgreso = (!tieneTestsGlobal || a.progreso === -1)
                ? '<span class="badge" style="background: #F1F5F9; color: #64748B;">Sin tests asignados</span>'
                : `<span class="badge badge-success">${a.progreso}%</span>`;

            return `
                <tr class="table-row-animated">
                    <td><strong>${esc(a.nombre || a.Nombre)}</strong></td>
                    <td>${esc(a.correo || a.Correo)}</td>
                    <td class="text-center">${badgeProgreso}</td>
                </tr>
            `;
        }).join('') : `<tr><td colspan="3" class="text-center" style="padding: 20px; color: #94A3B8;">No hay estudiantes inscritos en esta aula.</td></tr>`;
    }

    function mostrarDashboard(si, nombreAula = '') {
        vistaLista.hidden = si;
        vistaDashboard.hidden = !si;
        if (bcSepAula) bcSepAula.hidden = !si;
        if (bcDetalleAula) bcDetalleAula.hidden = !si;
        if (bcListaAulas) {
            bcListaAulas.classList.toggle('bc-muted', si);
            bcListaAulas.classList.toggle('bc-active', !si);
        }
        if (si) {
            dashboardAulaNombre.textContent = nombreAula ? `AULA: ${nombreAula.toUpperCase()}` : 'DASHBOARD DEL AULA';
            window.scrollTo(0, 0);
        }
    }

    if (bcListaAulas) {
        bcListaAulas.addEventListener('click', () => {
            if (!vistaDashboard.hidden) mostrarDashboard(false);
        });
    }

    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', e => {
            document.querySelectorAll('.tab-btn').forEach(b => {
                b.classList.remove('active');
                b.style.color = '#64748B';
                b.style.borderBottom = 'none';
            });
            document.querySelectorAll('.tab-content').forEach(c => c.style.display = 'none');

            const tab = e.currentTarget;
            tab.classList.add('active');
            tab.style.color = '#6F4FB8';
            tab.style.borderBottom = '2px solid #6F4FB8';
            $(tab.dataset.tab).style.display = 'block';
        });
    });

    tbody.addEventListener('click', async e => {
        const btn = e.target.closest('button[data-action]');
        if (!btn) return;

        if (btn.dataset.action === 'ver') {
            const aulaId = Number(btn.dataset.id);
            const nombreAula = btn.dataset.nombre || 'Aula';

            try {
                const [resDashboard, resRanking, resRezago] = await Promise.all([
                    api(`/api/Aulas/${aulaId}/dashboard`),
                    api(`/api/Aulas/aula/${aulaId}/ranking`),
                    api(`/api/Evaluaciones/maestro/mis-alertas/rezago`)
                ]);

                if (!resDashboard.ok) return alert(await mensajeDe(resDashboard, 'No se pudo cargar el dashboard del aula.'));

                const data = await resDashboard.json();
                const ranking = resRanking.ok ? await resRanking.json() : [];
                const rezagos = resRezago.ok ? await resRezago.json() : [];

                dashboardAulaSub.textContent = `Código de acceso: ${data.codigoAcceso || data.CodigoAcceso || 'N/A'}`;

                const alumnos = data.alumnos || data.Alumnos || [];
                const tests = data.tests || data.Tests || [];
                const recursos = data.recursos || data.Recursos || [];

                statTotalAlumnos.textContent = alumnos.length;
                statTotalTests.textContent = tests.length;
                statTotalRecursos.textContent = recursos.length;

                tieneTestsGlobal = tests.length > 0;
                alumnosGlobal = alumnos;
                renderTablaAlumnosPaginada();

                if (ranking && ranking.length) {
                    podioRankingAula.innerHTML = ranking.slice(0, 3).map(r => {
                        const colorFondo = r.posicion === 1 ? '#10B981' : (r.posicion === 2 ? '#6366F1' : '#F59E0B');
                        const medalla = r.posicion === 1 ? '🥇 1st' : (r.posicion === 2 ? '🥈 2nd' : '🥉 3rd');
                        return `
                        <div style="background: ${colorFondo}; color: #FFF; padding: 20px; border-radius: 12px; text-align: center; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                            <div style="font-size: 28px; font-weight: 800; margin-bottom: 5px;">${r.puntajeAcumulado} pts</div>
                            <div style="font-size: 15px; font-weight: 600; opacity: 0.95;">${esc(r.nombreCompleto)}</div>
                            <div style="margin-top: 10px; font-size: 12px; background: rgba(255,255,255,0.25); padding: 4px 8px; border-radius: 20px; display: inline-block;">
                                ${medalla} | Tests: ${r.puntajeTotalTests} | Niveles: ${r.puntajeTotalNiveles}
                            </div>
                        </div>
                    `;
                    }).join('');
                } else {
                    podioRankingAula.innerHTML = '<p style="color: #64748B; padding: 10px;">Aún no se registran puntajes en el ranking de esta aula.</p>';
                }

                const rezagosAula = rezagos.filter(alt => alt.aulaId === aulaId || !alt.aulaId);

                if (!tieneTestsGlobal) {
                    totalAlertasBadge.textContent = '0 Casos';
                    tablaAlertasRezago.innerHTML = '<tr><td colspan="5" class="text-center" style="padding: 20px; color: #64748B;">Esta aula aún no cuenta con evaluaciones o tests asignados para evaluar rezagos.</td></tr>';
                } else if (rezagosAula.length) {
                    totalAlertasBadge.textContent = `${rezagosAula.length} Casos`;
                    tablaAlertasRezago.innerHTML = rezagosAula.map(alt => `
                        <tr class="table-row-animated">
                            <td><strong>${esc(alt.nombreEstudiante)}</strong></td>
                            <td>${esc(alt.nombreAula)}</td>
                            <td class="text-center"><span class="badge" style="background: #FEF3C7; color: #D97706;">${alt.promedioGeneral}%</span></td>
                            <td class="text-center">${alt.nivelesReprobados}</td>
                            <td class="text-center">
                                <span class="badge" style="background: #FEE2E2; color: #DC2626; font-weight: 700;">${esc(alt.nivelRiesgo)}</span>
                            </td>
                        </tr>
                    `).join('');
                } else {
                    totalAlertasBadge.textContent = '0 Casos';
                    tablaAlertasRezago.innerHTML = '<tr><td colspan="5" class="text-center" style="padding: 15px; color: #10B981;">No hay estudiantes con alertas de rezago en este momento.</td></tr>';
                }

                listaTestsDashboard.innerHTML = tests.length ? tests.map(t => `
                    <div style="background: #F8FAFC; border: 1px solid #CBD5E1; padding: 12px 16px; border-radius: 6px; display: flex; justify-content: space-between; align-items: center;">
                        <div>
                            <strong>${esc(t.nombre || t.Nombre)}</strong>
                            <div style="font-size: 12px; color: #64748B;">Preguntas: ${t.totalPreguntas || t.TotalPreguntas || 0}</div>
                        </div>
                        <span class="badge badge-success">Asignado</span>
                    </div>
                `).join('') : `<p style="color: #94A3B8; text-align: center; padding: 20px;">No hay tests asignados a esta aula.</p>`;

                listaRecursosDashboard.innerHTML = recursos.length ? recursos.map(r => `
                    <div style="background: #F8FAFC; border: 1px solid #CBD5E1; padding: 12px 16px; border-radius: 6px; display: flex; justify-content: space-between; align-items: center;">
                        <div>
                            <strong>${esc(r.titulo || r.Titulo)}</strong>
                            <div style="font-size: 12px; color: #64748B;">Recurso Educativo</div>
                        </div>
                        <a href="${esc(r.referencia || r.Referencia)}" target="_blank" class="btn-secondary" style="padding: 6px 12px; font-size: 13px; text-decoration: none;">Abrir</a>
                    </div>
                `).join('') : `<p style="color: #94A3B8; text-align: center; padding: 20px;">No hay recursos compartidos en esta aula.</p>`;

                mostrarDashboard(true, nombreAula);

            } catch (err) {
                console.error(err);
                alert('Error de red al intentar obtener los datos del dashboard.');
            }
            return;
        }

        modalTitle.textContent = 'Editar Aula';
        aulaIdInput.value = btn.dataset.id;
        nombreAulaInput.value = btn.dataset.nombre;
        aulaModal.classList.add('active');
    });

    $('btnNuevaAula').addEventListener('click', () => {
        modalTitle.textContent = 'Nueva Aula';
        aulaIdInput.value = ''; nombreAulaInput.value = '';
        aulaModal.classList.add('active');
    });
    $('btnCloseModal').addEventListener('click', () => aulaModal.classList.remove('active'));

    $('aulaForm').addEventListener('submit', async e => {
        e.preventDefault();
        const id = aulaIdInput.value, nombre = nombreAulaInput.value.trim();
        if (!nombre) return;
        try {
            const res = await api(id ? `/api/Aulas/${id}` : '/api/Aulas/crear', { method: id ? 'PUT' : 'POST', body: JSON.stringify({ nombre }) });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo guardar el aula.'));
            aulaModal.classList.remove('active');
            cargar(id ? false : true);
        } catch (err) { console.error(err); alert('Error de red al guardar el aula.'); }
    });

    $('btnEliminar').addEventListener('click', async () => {
        const ids = aulasSeleccionadas();
        if (!ids.length) return alert('Selecciona al menos un aula.');
        if (!confirm(`¿Eliminar ${ids.length} aula(s) seleccionada(s)?`)) return;
        const resultados = await Promise.allSettled(ids.map(id => api(`/api/Aulas/${id}`, { method: 'DELETE' })));
        const fallidas = resultados.filter(r => r.status !== 'fulfilled' || !r.value.ok).length;
        if (fallidas) alert(`${fallidas} aula(s) no pudieron eliminarse.`);
        cargar(true);
    });

    function crearSelector(inputEl, listEl, chipsEl, onChange) {
        let items = [];
        const sel = new Map();
        const pintar = () => {
            listEl.innerHTML = items.filter(i => !sel.has(i.id)).map(i => `<option value="${esc(i.etiqueta)}"></option>`).join('');
            chipsEl.innerHTML = [...sel].map(([id, et]) => `<span class="chip">${esc(et)}<button type="button" data-id="${id}">×</button></span>`).join('');
            onChange(sel.size);
        };
        const agregar = parcial => {
            const v = inputEl.value.trim().toLowerCase();
            if (!v) return;
            const libres = items.filter(i => !sel.has(i.id));
            let it = libres.find(i => i.etiqueta.toLowerCase() === v);
            if (!it && parcial) { const m = libres.filter(i => i.etiqueta.toLowerCase().includes(v)); if (m.length === 1) it = m[0]; }
            if (it) { sel.set(it.id, it.etiqueta); inputEl.value = ''; pintar(); }
        };
        inputEl.addEventListener('input', e => { if (!e.inputType || e.inputType === 'insertReplacementText') agregar(false); });
        inputEl.addEventListener('change', () => agregar(false));
        inputEl.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); agregar(true); } });
        chipsEl.addEventListener('click', e => { const b = e.target.closest('button[data-id]'); if (b) { sel.delete(Number(b.dataset.id)); pintar(); } });
        return {
            setItems(arr) {
                const cuenta = {}; arr.forEach(i => cuenta[i.nombre] = (cuenta[i.nombre] || 0) + 1);
                items = arr.map(i => ({ id: i.id, etiqueta: cuenta[i.nombre] > 1 ? `${i.nombre} (#${i.id})` : i.nombre }));
                sel.clear(); inputEl.value = ''; pintar();
            },
            ids: () => [...sel.keys()]
        };
    }

    const modalAsignar = $('modalAsignar'), btnConfirmar = $('btnConfirmarAsignar');
    const selector = crearSelector($('asignarInput'), $('asignarDatalist'), $('asignarChips'), n => btnConfirmar.disabled = n === 0);
    let tipoAsignacion = null, aulasDestino = [];

    async function cargarRecursos() {
        const todos = []; let cursor = null;
        do {
            const p = new URLSearchParams({ Limite: 50 });
            if (cursor) { p.set('UltimoId', cursor.id); p.set('UltimaFecha', cursor.fecha); }
            const res = await api(`/api/Aulas/recursos/mis-recursos?${p}`);
            if (!res.ok) throw new Error(res.status);
            const r = await res.json();
            todos.push(...(r.datos || []).map(x => ({ id: x.id, nombre: x.titulo })));
            cursor = r.tieneMasPaginas ? { id: r.siguienteUltimoId, fecha: r.siguienteUltimaFecha } : null;
        } while (cursor);
        return todos;
    }
    async function cargarTests() {
        const res = await api('/api/Tests/mis-tests');
        if (!res.ok) throw new Error(res.status);
        return ((await res.json()).datos || []).map(t => ({ id: t.testId, nombre: t.nombre }));
    }

    async function abrirAsignar(tipo) {
        aulasDestino = aulasSeleccionadas();
        if (!aulasDestino.length) return alert('Selecciona al menos un aula.');
        tipoAsignacion = tipo;
        const esRecurso = tipo === 'recurso';
        $('asignarTitulo').textContent = esRecurso ? 'Asignar recursos a aulas' : 'Asignar tests a aulas';
        $('asignarLabel').textContent = esRecurso ? 'Buscar recurso' : 'Buscar test';
        $('asignarResumen').innerHTML = `Aulas seleccionadas: <span style="color:var(--primary-blue);font-weight:700;">${aulasDestino.length}</span>`;
        selector.setItems([]);
        modalAsignar.classList.add('active');
        try { selector.setItems(esRecurso ? await cargarRecursos() : await cargarTests()); }
        catch (err) { console.error(err); alert('No se pudo cargar la lista.'); }
    }

    btnConfirmar.addEventListener('click', async () => {
        const ids = selector.ids();
        if (!ids.length) return;
        const esRecurso = tipoAsignacion === 'recurso';
        const url = esRecurso ? '/api/Aulas/recursos/asignar-aulas' : '/api/Tests/asignar-test';
        const body = esRecurso ? { recursoId: ids, aulaIds: aulasDestino } : { testIds: ids, aulaIds: aulasDestino };
        try {
            const res = await api(url, { method: 'POST', body: JSON.stringify(body) });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo completar la asignación.'));
            alert((await res.json().catch(() => null))?.mensaje || 'Asignación realizada.');
            modalAsignar.classList.remove('active');
            cargar();
        } catch (err) { console.error(err); alert('Error de red al asignar.'); }
    });
    $('btnCancelarAsignar').addEventListener('click', () => modalAsignar.classList.remove('active'));
    $('btnAsignarRecurso').addEventListener('click', () => abrirAsignar('recurso'));
    $('btnAsignarTest').addEventListener('click', () => abrirAsignar('test'));

    cargar(true);
});