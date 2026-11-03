document.addEventListener('DOMContentLoaded', () => {
    const LIMITE = 5;
    let pagina = 1, totalPaginas = 1, total = 0, hayMas = false;
    let cursores = [null];
    let debounce, aulas = [];
    let testActualPreview = null;

    const $ = id => document.getElementById(id);
    const tbody = $('testsTableBody'), searchInput = $('searchInput'), selectAll = $('selectAllCheckbox');
    const filterAula = $('filterAula'), filterFecha = $('filterFecha');

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = d => new Date(d).toLocaleDateString('es-GT');

    async function api(url, options = {}) {
        const res = await fetch(url, {
            credentials: 'same-origin',
            ...options,
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(options.headers || {}) }
        });
        if (res.status === 401) { window.location.href = ''; throw new Error('401'); }
        return res;
    }
    const mensajeDe = async (res, def) => (await res.json().catch(() => null))?.mensaje || def;

    // ---------- Carga con cursores + filtros ----------
    async function fetchPagina(cursor) {
        const p = new URLSearchParams({ Limite: LIMITE });
        const q = searchInput.value.trim();
        if (q) p.set('Busqueda', q);
        if (filterAula.value) p.set('AulaId', filterAula.value);
        if (filterFecha.value) p.set('Fecha', filterFecha.value); // yyyy-MM-dd
        if (cursor) { p.set('UltimoId', cursor.ultimoId); p.set('UltimaFecha', cursor.ultimaFecha); }
        const res = await api(`/api/Tests/paginados?${p}`);
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
            tbody.innerHTML = '<tr><td colspan="8" class="text-center" style="padding:30px;color:#EF4444;">Error al cargar las evaluaciones.</td></tr>';
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

    function estado(t) {
        const ahora = new Date();
        if (t.fechaDisponibleDesde && ahora < new Date(t.fechaDisponibleDesde)) return ['Programado', 'badge-warning'];
        if (t.fechaDisponibleHasta && ahora > new Date(t.fechaDisponibleHasta)) return ['Finalizado', 'badge-danger'];
        return ['Activo', 'badge-success'];
    }

    function renderTabla(tests) {
        selectAll.checked = false;
        if (!tests.length) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center" style="padding:30px;color:#94A3B8;">No se encontraron evaluaciones.</td></tr>';
            return;
        }
        tbody.innerHTML = tests.map(t => {
            const [txt, cls] = estado(t);
            const disp = (t.fechaDisponibleDesde || t.fechaDisponibleHasta)
                ? `${t.fechaDisponibleDesde ? fmt(t.fechaDisponibleDesde) : '—'} – ${t.fechaDisponibleHasta ? fmt(t.fechaDisponibleHasta) : '—'}`
                : 'Sin límite';
            return `
            <tr class="table-row-animated">
                <td class="col-checkbox"><input type="checkbox" class="test-checkbox" value="${t.testId}"></td>
                <td><strong>${esc(t.nombre)}</strong></td>
                <td>${t.aulasAsignadas ?? 0}</td>
                <td>Personalizado</td>
                <td class="text-center">${t.totalPreguntas ?? 0}</td>
                <td class="text-center">${disp}</td>
                <td class="text-center"><span class="badge ${cls}">${txt}</span></td>
                <td class="text-center">
                    <div class="action-buttons">
                        <button class="btn-action view" data-action="ver" data-id="${t.testId}" title="Ver"><img src="icons/bx-eye.svg" alt="Ver"></button>
                        <button class="btn-action delete" data-action="eliminar" data-id="${t.testId}" title="Eliminar"><img src="icons/bx-trash-alt.svg" alt="Eliminar"></button>
                    </div>
                </td>
            </tr>`;
        }).join('');
    }

    function renderPaginador(n) {
        $('paginationInfo').textContent = `Mostrando ${n} de ${total} Evaluaciones existentes`;
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

    // ---------- Filtros ----------
    searchInput.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => cargar(true), 300); });
    $('btnSearch').addEventListener('click', () => cargar(true));
    filterAula.addEventListener('change', () => cargar(true));
    filterFecha.addEventListener('change', () => cargar(true));

    // ---------- Selección y acciones por fila ----------
    selectAll.addEventListener('change', e => document.querySelectorAll('.test-checkbox').forEach(cb => cb.checked = e.target.checked));
    const testsSeleccionados = () => [...document.querySelectorAll('.test-checkbox:checked')].map(cb => Number(cb.value));

    // DELETE /api/Tests/{id} está restringido a Roles="1" en el backend
    async function eliminarTests(ids) {
        // La anulación exige una justificación (10 a 500 caracteres) para la auditoría en MongoDB.
        const justificacion = (prompt('Justificación de la anulación (10 a 500 caracteres):') || '').trim();
        if (justificacion.length < 10 || justificacion.length > 500) return alert('La justificación debe tener entre 10 y 500 caracteres.');
        const resultados = await Promise.allSettled(ids.map(id => api(`/api/Tests/${id}`, { method: 'DELETE', body: JSON.stringify({ justificacion }) })));
        const fallidos = resultados.filter(r => r.status !== 'fulfilled' || !r.value.ok);
        if (fallidos.length) {
            const prohibido = fallidos.some(r => r.status === 'fulfilled' && r.value.status === 403);
            alert(prohibido ? 'Tu rol no tiene permiso para eliminar tests.' : `${fallidos.length} test(s) no pudieron eliminarse.`);
        }
        cargar(true);
    }

    // ---------- Vista Preliminar (Botón "Ver" de cada fila) ----------
    tbody.addEventListener('click', async e => {
        const btn = e.target.closest('button[data-action]');
        if (!btn) return;

        const action = btn.dataset.action;
        const testId = Number(btn.dataset.id);

        if (action === 'ver') {
            try {
                // Reutilizamos el endpoint por lote enviando un solo ID para obtener su estructura completa
                const res = await api('/api/Tests/detalle-lote', {
                    method: 'POST',
                    body: JSON.stringify([testId])
                });

                if (!res.ok) return alert(await mensajeDe(res, 'No se pudo cargar la vista preliminar del test.'));
                const data = await res.json();

                if (!data.length) return alert('No se encontró información para este test.');
                testActualPreview = data[0];

                // Renderizar la vista previa dentro del modal
                const contenedor = $('previewContenidoImprimible');
                $('previewTituloTest').textContent = `Vista Preliminar: ${testActualPreview.nombre || testActualPreview.Nombre}`;

                const preguntasList = testActualPreview.preguntas || testActualPreview.Preguntas || [];

                contenedor.innerHTML = `
                <div style="text-align: center; margin-bottom: 20px;">
                    <h3 style="margin: 0; color: #1E293B;">INKMATH - PLATAFORMA EDUCATIVA</h3>
                    <p style="margin: 5px 0 0; color: #64748B; font-size: 13px;">Evaluación: <strong>${esc(testActualPreview.nombre || testActualPreview.Nombre)}</strong></p>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 20px; font-size: 13px; border: 1px solid #CBD5E1; padding: 8px 12px; border-radius: 4px;">
                    <span><strong>Estudiante:</strong> _________ </span>
                    <span><strong>Fecha:</strong> ____/____/2026</span>
                </div>
                <div style="font-style: italic; font-size: 12px; margin-bottom: 15px; color: #475569; background: #F8FAFC; padding: 6px 10px; border-left: 3px solid #6F4FB8;">
                    Instrucciones: Lee atentamente cada enunciado y marca con una "X" o escribe la respuesta correcta.
                </div>
                <div>
                    ${preguntasList.map((p, idx) => {
                        const tipo = Number(p.tipoPreguntaId || p.TipoPreguntaId);
                        const opciones = p.opciones || p.Opciones || [];

                        // Si es tipo 3 (Respuesta abierta), mostramos líneas de escritura en lugar de opciones
                        if (tipo === 3) {
                            return `
                                <div style="margin-bottom: 20px;">
                                    <div style="font-weight: 600; font-size: 14px; margin-bottom: 10px;">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                    <div style="margin-top: 12px; border-bottom: 1px solid #94A3B8; height: 25px; width: 100%;"></div>
                                    <div style="margin-top: 8px; border-bottom: 1px solid #94A3B8; height: 25px; width: 100%;"></div>
                                </div>
                            `;
                        }

                        // Para opción múltiple o Verdadero/Falso (Tipos 1 y 2)
                        return `
                            <div style="margin-bottom: 15px;">
                                <div style="font-weight: 600; font-size: 14px; margin-bottom: 6px;">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 6px; padding-left: 12px;">
                                    ${opciones.map((op, oIdx) => `
                                        <div style="font-size: 13px;">
                                            <span>(${String.fromCharCode(97 + oIdx)})</span> ${esc(op.textoOpcion || op.TextoOpcion)}
                                        </div>
                                    `).join('')}
                                </div>
                            </div>
                        `;
                    }).join('')}
                </div>
            `;

                $('modalVistaPreliminar').classList.add('active');

            } catch (err) {
                console.error(err);
                alert('Error de red al obtener el detalle del test.');
            }
            return;
        }

        if (confirm('¿Eliminar este test?')) eliminarTests([testId]);
    });

    // Eventos para cerrar el modal de vista preliminar
    $('btnCerrarPreview').addEventListener('click', () => $('modalVistaPreliminar').classList.remove('active'));
    $('btnCerrarPreview2').addEventListener('click', () => $('modalVistaPreliminar').classList.remove('active'));

    // Botón de imprimir desde la misma vista preliminar
    $('btnImprimirIndividual').addEventListener('click', () => {
        if (!testActualPreview) return;
        // Disparamos la misma lógica de exportación pero con este único test
        exportarUnicoTestPdf(testActualPreview);
    });

    function exportarUnicoTestPdf(test) {
        const ventanaImpresion = window.open('', '_blank');
        ventanaImpresion.document.write(`
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8">
            <title>${esc(test.nombre || test.Nombre)} - InkMath</title>
            <style>
                @page { size: legal; margin: 20mm; }
                body { font-family: 'Segoe UI', Arial, sans-serif; color: #1E293B; line-height: 1.5; margin: 0; padding: 20px; }
                .header-institucional { text-align: center; border-bottom: 2px solid #6F4FB8; padding-bottom: 15px; margin-bottom: 25px; }
                .header-institucional h2 { margin: 0; color: #6F4FB8; font-size: 22px; }
                .header-institucional p { margin: 5px 0 0; color: #64748B; font-size: 14px; }
                .datos-alumno { display: flex; justify-content: space-between; margin-bottom: 25px; font-size: 14px; border: 1px solid #CBD5E1; padding: 10px 15px; border-radius: 6px; }
                .instrucciones { font-style: italic; font-size: 13px; margin-bottom: 20px; color: #475569; background: #F8FAFC; padding: 8px 12px; border-left: 3px solid #6F4FB8; }
                .pregunta-item { margin-bottom: 18px; }
                .enunciado { font-weight: 600; margin-bottom: 8px; font-size: 15px; }
                .opciones-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; padding-left: 15px; }
                .opcion { font-size: 14px; }
            </style>
        </head>
        <body>
            <div class="header-institucional">
                <h2>INKMATH - PLATAFORMA EDUCATIVA</h2>
                <p>Evaluación Académica: <strong>${esc(test.nombre || test.Nombre)}</strong></p>
            </div>
            <div class="datos-alumno">
                <span><strong>Nombre del Estudiante:</strong> ________________________________________________________________</span>
                <span><strong>Fecha:</strong> ____/____/2026</span>
            </div>
            <div class="instrucciones">
                Instrucciones: Lee atentamente cada enunciado y marca con una "X" o escribe la respuesta correcta según corresponda.
            </div>
            <div class="preguntas-lista">
                ${(test.preguntas || test.Preguntas).map((p, idx) => `
                    <div class="pregunta-item">
                        <div class="enunciado">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                        <div class="opciones-grid">
                            ${(p.opciones || p.Opciones).map((op, oIdx) => `
                                <div class="opcion">
                                    <span>(${String.fromCharCode(97 + oIdx)})</span> ${esc(op.textoOpcion || op.TextoOpcion)}
                                </div>
                            `).join('')}
                        </div>
                    </div>
                `).join('')}
            </div>
            <script>
                window.onload = function() { window.print(); };
            </script>
        </body>
        </html>
    `);
        ventanaImpresion.document.close();
    }

    $('btnEliminarBloque').addEventListener('click', () => {
        const ids = testsSeleccionados();
        if (!ids.length) return alert('Selecciona al menos un test.');
        if (confirm(`¿Eliminar ${ids.length} test(s) seleccionado(s)?`)) eliminarTests(ids);
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

    // ---------- Asignar tests seleccionados a aulas ----------
    const modal = $('modalAsignarAulas'), btnConfirmar = $('btnConfirmarAsignacionAulas');
    const selector = crearSelector($('asignarAulaInput'), $('asignarAulaDatalist'), $('asignarAulaChips'), n => btnConfirmar.disabled = n === 0);
    let testsDestino = [];

    $('btnAsignarBloque').addEventListener('click', () => {
        testsDestino = testsSeleccionados();
        if (!testsDestino.length) return alert('Selecciona al menos un test.');
        $('lblTestsSeleccionados').innerHTML = `Tests seleccionados: <span style="color:var(--primary-blue);font-weight:700;">${testsDestino.length}</span>`;
        selector.setItems(aulas);
        modal.classList.add('active');
    });

    btnConfirmar.addEventListener('click', async () => {
        const aulaIds = selector.ids();
        if (!aulaIds.length) return;
        try {
            const res = await api('/api/Tests/asignar-test', { method: 'POST', body: JSON.stringify({ testIds: testsDestino, aulaIds }) });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo completar la asignación.'));
            alert((await res.json().catch(() => null))?.mensaje || 'Asignación realizada.');
            modal.classList.remove('active');
            cargar();
        } catch (err) { console.error(err); alert('Error de red al asignar.'); }
    });
    $('btnCancelarAsignacionAulas').addEventListener('click', () => modal.classList.remove('active'));

    // ---------- Aulas del docente (filtro + selector) ----------
    async function cargarAulas() {
        try {
            const res = await api('/api/Aulas/mis-aulas');
            if (!res.ok) return;
            aulas = (await res.json()).map(a => ({ id: a.id, nombre: a.nombre }));
            filterAula.innerHTML = '<option value="">Todas mis aulas</option>' +
                aulas.map(a => `<option value="${a.id}">${esc(a.nombre)}</option>`).join('');
        } catch (err) { console.error(err); }
    }

    // ---------- Vista Nuevo Test ----------
    // Ajustar a los ids reales de la tabla tipos_pregunta
    const TIPOS_PREGUNTA = {
        1: { nombre: 'Opción múltiple', modo: 'multiple' },
        2: { nombre: 'Verdadero / Falso', modo: 'vf' },
        3: { nombre: 'Respuesta abierta', modo: 'abierta' }
    };

    const vistaLista = $('vistaLista'), vistaNuevo = $('vistaNuevo'), testForm = $('testForm'), preguntasBox = $('preguntasContainer');
    const bcLista = $('bcLista'), bcSep2 = $('bcSep2'), bcNuevo = $('bcNuevo');
    const selectorAulasTest = crearSelector($('ntAulaInput'), $('ntAulaDatalist'), $('ntAulaChips'), () => { });
    let uidPregunta = 0;

    function mostrarNuevo(si) {
        vistaLista.hidden = si; vistaNuevo.hidden = !si;
        bcSep2.hidden = !si; bcNuevo.hidden = !si;
        bcLista.classList.toggle('bc-muted', si); bcLista.classList.toggle('bc-active', !si);
        if (si) {
            testForm.reset(); preguntasBox.innerHTML = ''; agregarPregunta();
            selectorAulasTest.setItems(aulas);
            window.scrollTo(0, 0);
        }
    }

    function filaOpcion(nombreRadio, checked = false, texto = '', fija = false) {
        const d = document.createElement('div');
        d.className = 'opcion-row';
        d.innerHTML = `<input type="radio" name="${nombreRadio}" title="Respuesta correcta" ${checked ? 'checked' : ''}>
            <input type="text" class="op-texto" placeholder="Texto de la opción" value="${esc(texto)}" ${fija ? 'readonly' : ''}>
            ${fija ? '' : '<button type="button" class="btn-remove" data-act="quitar-opcion">×</button>'}`;
        return d;
    }

    function configurarOpciones(card) {
        const modo = TIPOS_PREGUNTA[card.querySelector('.q-tipo').value].modo, uid = card.dataset.uid;
        const lista = card.querySelector('.q-lista');
        lista.innerHTML = '';
        card.querySelector('.q-opciones').hidden = modo === 'abierta';
        card.querySelector('[data-act="agregar-opcion"]').hidden = modo !== 'multiple';
        if (modo === 'vf') lista.append(filaOpcion(`c${uid}`, true, 'Verdadero', true), filaOpcion(`c${uid}`, false, 'Falso', true));
        else if (modo === 'multiple') lista.append(filaOpcion(`c${uid}`, true), filaOpcion(`c${uid}`));
    }

    function agregarPregunta() {
        const uid = ++uidPregunta, card = document.createElement('div');
        card.className = 'pregunta-card';
        card.innerHTML = `
            <div class="pregunta-header">
                <strong class="q-num"></strong>
                <button type="button" data-act="quitar-pregunta" class="btn-danger-outline">Quitar</button>
            </div>
            <div class="form-group"><label>Tipo</label>
                <select class="q-tipo">${Object.entries(TIPOS_PREGUNTA).map(([id, t]) => `<option value="${id}">${t.nombre}</option>`).join('')}</select></div>
            <div class="form-group"><label>Enunciado</label><input type="text" class="q-texto" placeholder="Escribe la pregunta"></div>
            <div class="q-opciones"><label style="display:block;margin-bottom:8px;">Opciones (marca la correcta)</label>
                <div class="q-lista"></div>
                <button type="button" data-act="agregar-opcion" class="btn-secondary">+ Opción</button></div>`;
        card.dataset.uid = uid;
        configurarOpciones(card);
        preguntasBox.appendChild(card);
        numerarPreguntas();
    }
    const numerarPreguntas = () => preguntasBox.querySelectorAll('.q-num').forEach((n, i) => n.textContent = `Pregunta ${i + 1}`);

    preguntasBox.addEventListener('click', e => {
        const b = e.target.closest('[data-act]'); if (!b) return;
        const card = b.closest('.pregunta-card');
        if (b.dataset.act === 'quitar-pregunta') {
            if (preguntasBox.children.length === 1) return alert('El test debe tener al menos una pregunta.');
            card.remove(); numerarPreguntas();
        } else if (b.dataset.act === 'agregar-opcion') {
            card.querySelector('.q-lista').appendChild(filaOpcion(`c${card.dataset.uid}`));
        } else if (b.dataset.act === 'quitar-opcion') {
            const lista = card.querySelector('.q-lista');
            if (lista.children.length <= 2) return alert('Se requieren al menos 2 opciones.');
            const fila = b.closest('.opcion-row'), eraCorrecta = fila.querySelector('input[type=radio]').checked;
            fila.remove();
            if (eraCorrecta) lista.querySelector('input[type=radio]').checked = true;
        }
    });
    preguntasBox.addEventListener('change', e => {
        if (!e.target.classList.contains('q-tipo')) return;
        configurarOpciones(e.target.closest('.pregunta-card'));
    });

    function recolectarPreguntas() {
        const out = [];
        for (const [i, card] of [...preguntasBox.querySelectorAll('.pregunta-card')].entries()) {
            const tipo = Number(card.querySelector('.q-tipo').value);
            const pregunta = card.querySelector('.q-texto').value.trim();
            if (!pregunta) throw new Error(`La pregunta ${i + 1} no tiene enunciado.`);
            let opciones = [], correcta = 0;
            if (TIPOS_PREGUNTA[tipo].modo !== 'abierta') {
                const filas = [...card.querySelectorAll('.opcion-row')];
                opciones = filas.map(f => f.querySelector('.op-texto').value.trim());
                if (opciones.some(o => !o)) throw new Error(`La pregunta ${i + 1} tiene opciones vacías.`);
                correcta = filas.findIndex(f => f.querySelector('input[type=radio]').checked);
                if (correcta < 0) throw new Error(`Marca la opción correcta en la pregunta ${i + 1}.`);
            }
            out.push({ tipoPreguntaId: tipo, pregunta, opciones, opcionCorrectaIndex: correcta });
        }
        return out;
    }

    testForm.addEventListener('submit', async e => {
        e.preventDefault();
        const nombre = $('ntNombre').value.trim(), desde = $('ntDesde').value, hasta = $('ntHasta').value;
        if (!nombre) return alert('El nombre del test es obligatorio.');
        if (desde && hasta && new Date(hasta) < new Date(desde)) return alert('"Disponible hasta" no puede ser anterior a "desde".');
        let preguntas;
        try { preguntas = recolectarPreguntas(); } catch (err) { return alert(err.message); }
        const body = {
            maestroId: 0, nombre,
            fechaDisponibleDesde: desde ? new Date(desde).toISOString() : null,
            fechaDisponibleHasta: hasta ? new Date(hasta).toISOString() : null,
            aulaIds: selectorAulasTest.ids(), preguntas
        };
        const btn = $('btnGuardarTest'); btn.disabled = true;
        try {
            const res = await api('/api/Tests', { method: 'POST', body: JSON.stringify(body) });
            if (!res.ok) return alert(await mensajeDe(res, 'No se pudo crear el test.'));
            alert('Test creado correctamente.');
            mostrarNuevo(false); cargar(true);
        } catch (err) { console.error(err); alert('Error de red al guardar el test.'); }
        finally { btn.disabled = false; }
    });

    $('btnNuevoTest').addEventListener('click', () => mostrarNuevo(true));
    $('btnAgregarPregunta').addEventListener('click', agregarPregunta);
    $('btnCancelarNuevo').addEventListener('click', () => mostrarNuevo(false));
    bcLista.addEventListener('click', () => { if (!vistaNuevo.hidden) mostrarNuevo(false); });

    // ---------- Control de UI por rol ----------
    // Solo el Administrador puede anular tests (DELETE = Roles "1"). Para el resto se ocultan los controles de
    // eliminación; aunque se forzara la petición, el backend responde 403 Forbidden.
    async function aplicarControlPorRol() {
        try {
            const res = await api('/api/Usuarios/me');
            if (!res.ok) return;
            const me = await res.json();
            const esAdmin = Number(me.rolId) === 1 || /administrador/i.test(me.rolNombre || '');
            if (esAdmin) return;
            $('btnEliminarBloque').style.display = 'none';
            const estilo = document.createElement('style');
            estilo.textContent = '[data-action="eliminar"] { display: none !important; }';
            document.head.appendChild(estilo);
        } catch (err) { console.error(err); }
    }

    aplicarControlPorRol();
    cargarAulas();
    cargar(true);

    // ---------- Exportar Test Individual a PDF / Impresión ----------
    function exportarUnicoTestPdf(test) {
        const ventanaImpresion = window.open('', '_blank');
        ventanaImpresion.document.write(`
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8">
            <title>${esc(test.nombre || test.Nombre)} - InkMath</title>
            <style>
                @page { size: legal; margin: 15mm; }
                body { font-family: 'Segoe UI', Arial, sans-serif; color: #1E293B; line-height: 1.4; margin: 0; padding: 0; background: #FFF; }
                .evaluacion-page { width: 100%; box-sizing: border-box; page-break-after: always; margin-bottom: 30px; }
                .evaluacion-page:last-child { page-break-after: avoid; }
                
                .header-institucional { text-align: center; border-bottom: 2px solid #6F4FB8; padding-bottom: 10px; margin-bottom: 20px; }
                .header-institucional h2 { margin: 0; color: #6F4FB8; font-size: 20px; }
                .header-institucional p { margin: 4px 0 0; color: #64748B; font-size: 13px; }
                
                .datos-alumno { width: 100%; margin-bottom: 20px; font-size: 13px; border: 1px solid #CBD5E1; padding: 8px 12px; border-radius: 4px; box-sizing: border-box; }
                .datos-alumno table { width: 100%; border-collapse: collapse; }
                .datos-alumno td { padding: 0; }
                
                .instrucciones { font-style: italic; font-size: 12px; margin-bottom: 15px; color: #475569; background: #F8FAFC; padding: 6px 10px; border-left: 3px solid #6F4FB8; }
                
                .pregunta-item { margin-bottom: 16px; page-break-inside: avoid; }
                .enunciado { font-weight: 600; font-size: 14px; margin-bottom: 6px; }
                
                /* Tabla de opciones segura para impresión (reemplaza flexbox) */
                .opciones-table { width: 100%; border-collapse: collapse; margin-left: 10px; }
                .opciones-table td { width: 50%; padding: 3px 6px; font-size: 13px; vertical-align: top; }
                
                .linea-respuesta { margin-top: 10px; border-bottom: 1px solid #94A3B8; height: 22px; width: 100%; }
            </style>
        </head>
        <body>
            <div class="evaluacion-page">
                <div class="header-institucional">
                    <h2>INKMATH - PLATAFORMA EDUCATIVA</h2>
                    <p>Evaluación Académica: <strong>${esc(test.nombre || test.Nombre)}</strong></p>
                </div>
                
                <div class="datos-alumno">
                    <table>
                        <tr>
                            <td><strong>Nombre del Estudiante:</strong> _________________________________________</td>
                            <td style="text-align: right;"><strong>Fecha:</strong> ____/____/2026</td>
                        </tr>
                    </table>
                </div>

                <div class="instrucciones">
                    Instrucciones: Lee atentamente cada enunciado y marca con una "X" o escribe la respuesta correcta según corresponda.
                </div>

                <div class="preguntas-lista">
                    ${(test.preguntas || test.Preguntas).map((p, idx) => {
            const tipo = Number(p.tipoPreguntaId || p.TipoPreguntaId);
            const opciones = p.opciones || p.Opciones || [];

            if (tipo === 3) {
                return `
                                <div class="pregunta-item">
                                    <div class="enunciado">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                    <div class="linea-respuesta"></div>
                                    <div class="linea-respuesta"></div>
                                </div>
                            `;
            }

            // Agrupar opciones en pares de columnas usando filas de tabla HTML para impresión perfecta
            let filasOpciones = '';
            for (let i = 0; i < opciones.length; i += 2) {
                const op1 = opciones[i];
                const op2 = opciones[i + 1];
                filasOpciones += `
                                <tr>
                                    <td>(${String.fromCharCode(97 + i)}) ${esc(op1.textoOpcion || op1.TextoOpcion)}</td>
                                    <td>${op2 ? `(${String.fromCharCode(97 + i + 1)}) ${esc(op2.textoOpcion || op2.TextoOpcion)}` : ''}</td>
                                </tr>
                            `;
            }

            return `
                            <div class="pregunta-item">
                                <div class="enunciado">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                <table class="opciones-table">
                                    ${filasOpciones}
                                </table>
                            </div>
                        `;
        }).join('')}
                </div>
            </div>
            <script>
                window.onload = function() { window.print(); };
            </script>
        </body>
        </html>
    `);
        ventanaImpresion.document.close();
    }


    // ---------- Exportar Tests Seleccionados por Lote a PDF / Impresión ----------
    async function exportarTestsPdfSeleccionados() {
        const ids = testsSeleccionados();
        if (!ids.length) return alert('Selecciona al menos un test para exportar.');

        try {
            const res = await api('/api/Tests/detalle-lote', {
                method: 'POST',
                body: JSON.stringify(ids)
            });

            if (!res.ok) return alert(await mensajeDe(res, 'No se pudieron obtener los datos para exportar.'));
            const testsData = await res.json();

            const ventanaImpresion = window.open('', '_blank');
            ventanaImpresion.document.write(`
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta charset="UTF-8">
                <title>Evaluaciones InkMath - Formato Oficial</title>
                <style>
                    @page { size: legal; margin: 15mm; }
                    body { font-family: 'Segoe UI', Arial, sans-serif; color: #1E293B; line-height: 1.4; margin: 0; padding: 0; background: #FFF; }
                    .evaluacion-page { width: 100%; box-sizing: border-box; page-break-after: always; margin-bottom: 30px; }
                    .evaluacion-page:last-child { page-break-after: avoid; }
                    
                    .header-institucional { text-align: center; border-bottom: 2px solid #6F4FB8; padding-bottom: 10px; margin-bottom: 20px; }
                    .header-institucional h2 { margin: 0; color: #6F4FB8; font-size: 20px; }
                    .header-institucional p { margin: 4px 0 0; color: #64748B; font-size: 13px; }
                    
                    .datos-alumno { width: 100%; margin-bottom: 20px; font-size: 13px; border: 1px solid #CBD5E1; padding: 8px 12px; border-radius: 4px; box-sizing: border-box; }
                    .datos-alumno table { width: 100%; border-collapse: collapse; }
                    .datos-alumno td { padding: 0; }
                    
                    .instrucciones { font-style: italic; font-size: 12px; margin-bottom: 15px; color: #475569; background: #F8FAFC; padding: 6px 10px; border-left: 3px solid #6F4FB8; }
                    
                    .pregunta-item { margin-bottom: 16px; page-break-inside: avoid; }
                    .enunciado { font-weight: 600; font-size: 14px; margin-bottom: 6px; }
                    
                    .opciones-table { width: 100%; border-collapse: collapse; margin-left: 10px; }
                    .opciones-table td { width: 50%; padding: 3px 6px; font-size: 13px; vertical-align: top; }
                    
                    .linea-respuesta { margin-top: 10px; border-bottom: 1px solid #94A3B8; height: 22px; width: 100%; }
                </style>
            </head>
            <body>
                ${testsData.map((test) => `
                    <div class="evaluacion-page">
                        <div class="header-institucional">
                            <h2>INKMATH - PLATAFORMA EDUCATIVA</h2>
                            <p>Evaluación Académica: <strong>${esc(test.nombre || test.Nombre)}</strong></p>
                        </div>
                        
                        <div class="datos-alumno">
                            <table>
                                <tr>
                                    <td><strong>Nombre del Estudiante:</strong> _________________________________________</td>
                                    <td style="text-align: right;"><strong>Fecha:</strong> ____/____/2026</td>
                                </tr>
                            </table>
                        </div>

                        <div class="instrucciones">
                            Instrucciones: Lee atentamente cada enunciado y marca con una "X" o escribe la respuesta correcta según corresponda.
                        </div>

                        <div class="preguntas-lista">
                            ${(test.preguntas || test.Preguntas).map((p, idx) => {
                const tipo = Number(p.tipoPreguntaId || p.TipoPreguntaId);
                const opciones = p.opciones || p.Opciones || [];

                if (tipo === 3) {
                    return `
                                        <div class="pregunta-item">
                                            <div class="enunciado">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                            <div class="linea-respuesta"></div>
                                            <div class="linea-respuesta"></div>
                                        </div>
                                    `;
                }

                let filasOpciones = '';
                for (let i = 0; i < opciones.length; i += 2) {
                    const op1 = opciones[i];
                    const op2 = opciones[i + 1];
                    filasOpciones += `
                                        <tr>
                                            <td>(${String.fromCharCode(97 + i)}) ${esc(op1.textoOpcion || op1.TextoOpcion)}</td>
                                            <td>${op2 ? `(${String.fromCharCode(97 + i + 1)}) ${esc(op2.textoOpcion || op2.TextoOpcion)}` : ''}</td>
                                        </tr>
                                    `;
                }

                return `
                                    <div class="pregunta-item">
                                        <div class="enunciado">${idx + 1}. ${esc(p.enunciado || p.Enunciado)}</div>
                                        <table class="opciones-table">
                                            ${filasOpciones}
                                        </table>
                                    </div>
                                `;
            }).join('')}
                        </div>
                    </div>
                `).join('')}
                <script>
                    window.onload = function() { window.print(); };
                </script>
            </body>
            </html>
        `);
            ventanaImpresion.document.close();

        } catch (err) {
            console.error(err);
            alert('Error de red al intentar exportar las evaluaciones.');
        }
    }

    // Enlazar al evento del nuevo botón (asegúrate de que el id exista en tu HTML)
    $('btnExportarPdfBloque')?.addEventListener('click', exportarTestsPdfSeleccionados);
});