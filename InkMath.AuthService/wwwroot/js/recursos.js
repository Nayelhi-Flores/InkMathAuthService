document.addEventListener('DOMContentLoaded', () => {
    const LIMITE = 5;
    const TIPOS = { 1: 'Documento', 2: 'Enlace', 3: 'Video' };
    let pagina = 1, hayMas = false, cursores = [null], cache = null, aulas = [], debounce;

    const $ = id => document.getElementById(id);
    const tbody = $('recursosTableBody'), searchInput = $('searchInput'), selectAll = $('selectAllCheckbox'), filterTipo = $('filterTipo');

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = d => new Date(d).toLocaleDateString('es-GT');

    async function api(url, options = {}) {
        const token = localStorage.getItem('token');
        const json = options.body instanceof FormData ? {} : { 'Content-Type': 'application/json' };
        const res = await fetch(url, {
            ...options,
            headers: { ...json, Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(options.headers || {}) }
        });
        if (res.status === 401) { localStorage.removeItem('token'); window.location.href = 'index.html'; throw new Error('401'); }
        return res;
    }
    const mensajeDe = async (res, def) => (await res.json().catch(() => null))?.mensaje || def;

    // ---------- Carga con cursores ----------
    async function fetchPagina(cursor, limite = LIMITE) {
        const p = new URLSearchParams({ Limite: limite });
        if (cursor) { p.set('UltimoId', cursor.ultimoId); p.set('UltimaFecha', cursor.ultimaFecha); }
        const res = await api(`/api/Aulas/recursos/mis-recursos?${p}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
    }

    // El backend no filtra recursos: al buscar/filtrar se trae todo (páginas de 50) y se filtra en cliente.
    async function cargarTodos() {
        if (cache) return cache;
        const todos = []; let cursor = null;
        do {
            const r = await fetchPagina(cursor, 50);
            todos.push(...(r.datos || []));
            cursor = r.tieneMasPaginas ? { ultimoId: r.siguienteUltimoId, ultimaFecha: r.siguienteUltimaFecha } : null;
        } while (cursor);
        return (cache = todos);
    }

    const filtrando = () => !!(searchInput.value.trim() || filterTipo.value);

    async function cargar(reset = false) {
        if (reset) { cursores = [null]; pagina = 1; }
        try {
            if (filtrando()) {
                const q = searchInput.value.trim().toLowerCase(), tipo = filterTipo.value;
                const lista = (await cargarTodos()).filter(r => (!q || r.titulo.toLowerCase().includes(q)) && (!tipo || String(r.tipoRecursoId) === tipo));
                hayMas = false; pagina = 1;
                renderTabla(lista); renderPaginador(lista.length, true);
                return;
            }
            const r = await fetchPagina(cursores[pagina - 1]);
            const datos = r.datos || [];
            if (!datos.length && pagina > 1) { pagina--; return cargar(); }
            cursores.length = pagina;
            hayMas = !!r.tieneMasPaginas;
            if (hayMas) cursores[pagina] = { ultimoId: r.siguienteUltimoId, ultimaFecha: r.siguienteUltimaFecha };
            renderTabla(datos); renderPaginador(datos.length, false);
        } catch (err) {
            console.error(err);
            tbody.innerHTML = '<tr><td colspan="6" class="text-center" style="padding:30px;color:#EF4444;">Error al cargar los recursos.</td></tr>';
        }
    }

    function renderTabla(lista) {
        selectAll.checked = false;
        if (!lista.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center" style="padding:30px;color:#94A3B8;">No se encontraron recursos.</td></tr>';
            return;
        }
        tbody.innerHTML = lista.map(r => `
            <tr class="table-row-animated">
                <td class="col-checkbox"><input type="checkbox" class="recurso-checkbox" value="${r.id}"></td>
                <td><strong>${esc(r.titulo)}</strong></td>
                <td class="text-center">${TIPOS[r.tipoRecursoId] || '-'}</td>
                <td class="cell-ellipsis" title="${esc(r.referencia)}">${esc(r.tipoRecursoId === 1 ? r.referencia.split('/').pop() : r.referencia)}</td>
                <td class="text-center">${r.creadoEn ? fmt(r.creadoEn) : '-'}</td>
                <td class="text-center">
                    <div class="action-buttons">
                        <button class="btn-action view" data-ref="${esc(r.referencia)}" title="Abrir"><img src="icons/bx-eye.svg" alt="Abrir"></button>
                    </div>
                </td>
            </tr>`).join('');
    }

    function renderPaginador(n, filtro) {
        $('paginationInfo').textContent = filtro ? `${n} resultado(s) encontrados` : `Mostrando ${n} recursos`;
        $('btnPageNum').textContent = pagina;
        $('btnPrevPage').disabled = filtro || pagina === 1;
        $('btnNextPage').disabled = filtro || !hayMas;
    }

    $('btnPrevPage').addEventListener('click', () => { if (pagina > 1) { pagina--; cargar(); } });
    $('btnNextPage').addEventListener('click', () => { if (hayMas) { pagina++; cargar(); } });

    // ---------- Filtros ----------
    searchInput.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => cargar(true), 300); });
    $('btnSearch').addEventListener('click', () => cargar(true));
    filterTipo.addEventListener('change', () => cargar(true));

    // ---------- Selección y acciones por fila ----------
    selectAll.addEventListener('change', e => document.querySelectorAll('.recurso-checkbox').forEach(cb => cb.checked = e.target.checked));
    const seleccionados = () => [...document.querySelectorAll('.recurso-checkbox:checked')].map(cb => Number(cb.value));

    tbody.addEventListener('click', e => {
        const btn = e.target.closest('button[data-ref]');
        if (btn) window.open(btn.dataset.ref, '_blank', 'noopener');
    });

    // ---------- Selector con datalist + chips ----------
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

    // ---------- Asignar recursos seleccionados a aulas ----------
    const modalAsignar = $('modalAsignar'), btnConfirmar = $('btnConfirmarAsignar');
    const selAsignar = crearSelector($('asignarInput'), $('asignarDatalist'), $('asignarChips'), n => btnConfirmar.disabled = n === 0);
    let recursosDestino = [];

    $('btnAsignarBloque').addEventListener('click', () => {
        recursosDestino = seleccionados();
        if (!recursosDestino.length) return alert('Selecciona al menos un recurso.');
        $('asignarResumen').innerHTML = `Recursos seleccionados: <span style="color:var(--primary-blue);font-weight:700;">${recursosDestino.length}</span>`;
        selAsignar.setItems(aulas);
        modalAsignar.classList.add('active');
    });

    btnConfirmar.addEventListener('click', async () => {
        const aulaIds = selAsignar.ids();
        if (!aulaIds.length) return;
        try {
            const res = await api('/api/Aulas/recursos/asignar-aulas', { method: 'POST', body: JSON.stringify({ recursoId: recursosDestino, aulaIds }) });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo completar la asignación.'));
            alert((await res.json().catch(() => null))?.mensaje || 'Asignación realizada.');
            modalAsignar.classList.remove('active');
        } catch (err) { console.error(err); alert('Error de red al asignar.'); }
    });
    $('btnCancelarAsignar').addEventListener('click', () => modalAsignar.classList.remove('active'));

    // ---------- Nuevo recurso ----------
    const modalRecurso = $('modalRecurso'), recTipo = $('recTipo');
    const selRecurso = crearSelector($('recAulaInput'), $('recAulaDatalist'), $('recAulaChips'), () => { });

    function ajustarTipo() {
        const t = recTipo.value;
        $('grpArchivo').hidden = t !== '1';
        $('grpUrl').hidden = t === '1';
        $('lblUrl').textContent = t === '3' ? 'URL del video de YouTube' : 'URL del enlace';
        $('recUrl').placeholder = t === '3' ? 'https://www.youtube.com/watch?v=...' : 'https://';
    }
    recTipo.addEventListener('change', ajustarTipo);

    $('btnNuevoRecurso').addEventListener('click', () => {
        $('recursoForm').reset(); ajustarTipo();
        selRecurso.setItems(aulas);
        modalRecurso.classList.add('active');
    });
    $('btnCancelarRecurso').addEventListener('click', () => modalRecurso.classList.remove('active'));

    $('recursoForm').addEventListener('submit', async e => {
        e.preventDefault();
        const titulo = $('recTitulo').value.trim(), tipo = recTipo.value;
        if (!titulo) return alert('El título es obligatorio.');
        const fd = new FormData();
        fd.append('Titulo', titulo);
        fd.append('TipoRecursoId', tipo);
        if (tipo === '1') {
            const file = $('recArchivo').files[0];
            if (!file) return alert('Selecciona un archivo.');
            fd.append('Archivo', file);
        } else {
            const url = $('recUrl').value.trim();
            if (!url) return alert('Ingresa la URL.');
            fd.append('UrlExterna', url);
        }
        selRecurso.ids().forEach(id => fd.append('AulaIds', id));

        const btn = $('btnGuardarRecurso'); btn.disabled = true;
        try {
            const res = await api('/api/Aulas/recursos', { method: 'POST', body: fd });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo guardar el recurso.'));
            modalRecurso.classList.remove('active');
            cache = null;
            cargar(true);
        } catch (err) { console.error(err); alert('Error de red al guardar el recurso.'); }
        finally { btn.disabled = false; }
    });

    // ---------- Aulas del docente ----------
    async function cargarAulas() {
        try {
            const res = await api('/api/Aulas/mis-aulas');
            if (res.ok) aulas = (await res.json()).map(a => ({ id: a.id, nombre: a.nombre }));
        } catch (err) { console.error(err); }
    }

    cargarAulas();
    cargar(true);
});
