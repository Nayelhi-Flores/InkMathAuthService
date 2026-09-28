document.addEventListener('DOMContentLoaded', () => {
    const LIMITE = 5; // el backend limita a 5 por página
    let pagina = 1, totalPaginas = 1, total = 0, hayMas = false;
    let cursores = [null]; // cursores[i] = cursor para pedir la página i+1
    let debounce;

    const $ = id => document.getElementById(id);
    const tbody = $('aulasTableBody'), searchInput = $('searchInput'), selectAll = $('selectAllCheckbox');
    const aulaModal = $('aulaModal'), modalTitle = $('modalTitle'), aulaIdInput = $('aulaIdInput'), nombreAulaInput = $('nombreAulaInput');

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    async function api(url, options = {}) {
        const token = localStorage.getItem('token');
        const res = await fetch(url, {
            ...options,
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(options.headers || {}) }
        });
        if (res.status === 401) { localStorage.removeItem('token'); window.location.href = 'index.html'; throw new Error('401'); }
        return res;
    }
    const mensajeDe = async (res, def) => (await res.json().catch(() => null))?.mensaje || def;

    // ---------- Carga con cursores ----------
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
            // descubrir cursores intermedios si el destino aún no se conoce
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
                        <button class="btn-action view" data-action="ver" data-id="${a.id}" title="Ver"><img src="icons/bx-eye.svg" alt="Ver"></button>
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
    $('btnFirstPage').addEventListener('click', () => irAPagina(1));
    $('btnLastPage').addEventListener('click', () => irAPagina(totalPaginas));
    $('pageSelect').addEventListener('change', e => irAPagina(Number(e.target.value)));

    // ---------- Búsqueda en tiempo real (backend) ----------
    searchInput.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => cargar(true), 300); });
    $('btnSearch').addEventListener('click', () => cargar(true));

    // ---------- Selección y acciones por fila ----------
    selectAll.addEventListener('change', e => document.querySelectorAll('.aula-checkbox').forEach(cb => cb.checked = e.target.checked));
    const aulasSeleccionadas = () => [...document.querySelectorAll('.aula-checkbox:checked')].map(cb => Number(cb.value));

    tbody.addEventListener('click', e => {
        const btn = e.target.closest('button[data-action]');
        if (!btn) return;
        if (btn.dataset.action === 'ver') return alert('Vista de detalle del aula: pendiente de desarrollo.');
        modalTitle.textContent = 'Editar Aula';
        aulaIdInput.value = btn.dataset.id;
        nombreAulaInput.value = btn.dataset.nombre;
        aulaModal.classList.add('active');
    });

    // ---------- Crear / editar ----------
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

    // ---------- Eliminar por lote ----------
    $('btnEliminar').addEventListener('click', async () => {
        const ids = aulasSeleccionadas();
        if (!ids.length) return alert('Selecciona al menos un aula.');
        if (!confirm(`¿Eliminar ${ids.length} aula(s) seleccionada(s)?`)) return;
        const resultados = await Promise.allSettled(ids.map(id => api(`/api/Aulas/${id}`, { method: 'DELETE' })));
        const fallidas = resultados.filter(r => r.status !== 'fulfilled' || !r.value.ok).length;
        if (fallidas) alert(`${fallidas} aula(s) no pudieron eliminarse.`);
        cargar(true);
    });

    // ---------- Selector con datalist + chips ----------
    function crearSelector(inputEl, listEl, chipsEl, onChange) {
        let items = [];
        const sel = new Map();
        const pintar = () => {
            listEl.innerHTML = items.filter(i => !sel.has(i.id)).map(i => `<option value="${esc(i.etiqueta)}"></option>`).join('');
            chipsEl.innerHTML = [...sel].map(([id, et]) => `<span style="display:inline-flex;align-items:center;gap:6px;background:#EEF2FF;color:var(--primary-blue);border-radius:16px;padding:4px 10px;font-size:.82rem;margin:0 6px 6px 0;">${esc(et)}<button type="button" data-id="${id}" style="border:0;background:none;cursor:pointer;color:inherit;font-weight:700;">×</button></span>`).join('');
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

    // ---------- Asignación por lote (recursos / tests) ----------
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
