document.addEventListener('DOMContentLoaded', () => {
    // Estado de Paginación
    let limite = 5;
    let paginaActual = 1;
    let totalPaginasGlobal = 1;

    // Historial de cursores para Keyset Pagination
    let historialCursores = [{ ultimoId: null, ultimaFecha: null }];
    let siguienteCursor = { ultimoId: null, ultimaFecha: null };
    let tieneMasPaginas = false;

    // Referencias DOM
    const aulasTableBody = document.getElementById('aulasTableBody');
    const selectAllCheckbox = document.getElementById('selectAllCheckbox');
    const paginationInfo = document.getElementById('paginationInfo');
    const searchInput = document.getElementById('searchInput');
    const btnSearch = document.getElementById('btnSearch');

    // Controles de Paginación UI
    const btnPrevPage = document.getElementById('btnPrevPage');
    const btnNextPage = document.getElementById('btnNextPage');
    const btnFirstPage = document.getElementById('btnFirstPage');
    const btnLastPage = document.getElementById('btnLastPage');
    const pageSelect = document.getElementById('pageSelect');
    const totalPagesLabel = document.getElementById('totalPagesLabel');

    // Modales y Botones
    const btnNuevaAula = document.getElementById('btnNuevaAula');
    const btnEliminar = document.getElementById('btnEliminar');
    const aulaModal = document.getElementById('aulaModal');
    const btnCloseModal = document.getElementById('btnCloseModal');
    const aulaForm = document.getElementById('aulaForm');
    const modalTitle = document.getElementById('modalTitle');
    const aulaIdInput = document.getElementById('aulaIdInput');
    const nombreAulaInput = document.getElementById('nombreAulaInput');
    const btnLogout = document.getElementById('btnLogout');

    // Inicializar la carga de datos
    cargarAulas(true);

    // 1. CARGAR AULAS MEDIANTE CURSOR
    async function cargarAulas(resetPaginacion = false) {
        if (resetPaginacion) {
            paginaActual = 1;
            historialCursores = [{ ultimoId: null, ultimaFecha: null }];
            siguienteCursor = { ultimoId: null, ultimaFecha: null };
        }

        const cursorActual = historialCursores[paginaActual - 1] || { ultimoId: null, ultimaFecha: null };

        let url = `/api/Aulas/paginadas?Limite=${limite}`;
        if (cursorActual.ultimoId && cursorActual.ultimaFecha) {
            url += `&UltimoId=${cursorActual.ultimoId}&UltimaFecha=${encodeURIComponent(cursorActual.ultimaFecha)}`;
        }

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Content-Type': 'application/json' }
            });

            if (response.ok) {
                const result = await response.json();

                tieneMasPaginas = result.tieneMasPaginas;
                siguienteCursor = {
                    ultimoId: result.siguienteUltimoId,
                    ultimaFecha: result.siguienteUltimaFecha
                };

                const listaAulas = result.datos || [];
                renderTabla(listaAulas);
                actualizarPaginador(result);
            } else if (response.status === 401) {
                window.location.href = 'index.html';
            } else {
                console.error('Error al obtener las aulas');
            }
        } catch (err) {
            console.error('Error de red:', err);
        }
    }

    // 2. RENDERIZAR TABLA CON CONTADORES
    function renderTabla(aulas) {
        aulasTableBody.innerHTML = '';
        if (selectAllCheckbox) selectAllCheckbox.checked = false;

        if (!aulas || aulas.length === 0) {
            aulasTableBody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center" style="padding: 30px; color: #94A3B8;">
                        No se encontraron aulas registradas.
                    </td>
                </tr>`;
            if (paginationInfo) paginationInfo.textContent = 'Mostrando 0 de 0 aulas existentes';
            return;
        }

        const query = searchInput ? searchInput.value.trim().toLowerCase() : '';
        const aulasFiltradas = query
            ? aulas.filter(a => a.nombre.toLowerCase().includes(query) || (a.codigoAcceso && a.codigoAcceso.toLowerCase().includes(query)))
            : aulas;

        aulasFiltradas.forEach((aula, index) => {
            const tr = document.createElement('tr');
            tr.classList.add('table-row-animated');
            tr.style.animationDelay = `${index * 0.04}s`;

            const numEstudiantes = aula.totalEstudiantes ?? aula.estudiantesCount ?? aula.cantidadEstudiantes ?? 0;
            const numRecursos = aula.totalRecursos ?? aula.recursosCount ?? aula.cantidadRecursos ?? 0;
            const numEvaluaciones = aula.totalTests ?? aula.totalEvaluaciones ?? aula.evaluacionesCount ?? 0;

            tr.innerHTML = `
                <td class="col-checkbox">
                    <input type="checkbox" class="aula-checkbox" value="${aula.id}">
                </td>
                <td><strong>${aula.codigoAcceso || aula.codigo || '---'}</strong></td>
                <td>${aula.nombre}</td>
                <td class="text-center">${numEstudiantes}</td>
                <td class="text-center">${numRecursos}</td>
                <td class="text-center">${numEvaluaciones}</td>
                <td class="text-center">
                    <div class="action-buttons">
                        <button class="btn-action view" onclick="verDetalle(${aula.id})" title="Ver">
                            <img src="icons/bx-eye.svg" alt="Ver">
                        </button>
                        <button class="btn-action edit" onclick="editarAula(${aula.id}, '${aula.nombre.replace(/'/g, "\\'")}')" title="Editar">
                            <img src="icons/bx-edit.svg" alt="Editar">
                        </button>
                    </div>
                </td>
            `;
            aulasTableBody.appendChild(tr);
        });
    }

    // 3. ACTUALIZAR CONTROLES Y NÚMEROS EN LA UI
    function actualizarPaginador(result) {
        const cantidadObtenida = result.datos ? result.datos.length : 0;
        const totalRegistros = result.totalRegistros ?? cantidadObtenida;
        totalPaginasGlobal = result.totalPaginas ?? 1;

        // Texto izquierdo: "Mostrando 5 de 7 aulas existentes"
        if (paginationInfo) {
            paginationInfo.textContent = `Mostrando ${cantidadObtenida} de ${totalRegistros} aulas existentes`;
        }

        // Botón "1" (Primera página)
        if (btnFirstPage) {
            btnFirstPage.textContent = "1";
            if (paginaActual === 1) {
                btnFirstPage.classList.add('page-badge');
                btnFirstPage.classList.remove('page-badge-secondary');
            } else {
                btnFirstPage.classList.remove('page-badge');
                btnFirstPage.classList.add('page-badge-secondary');
            }
        }

        // Botón de última página ("2", "3", etc.)
        if (btnLastPage) {
            btnLastPage.textContent = totalPaginasGlobal;
            if (paginaActual === totalPaginasGlobal) {
                btnLastPage.classList.add('page-badge');
                btnLastPage.classList.remove('page-badge-secondary');
            } else {
                btnLastPage.classList.remove('page-badge');
                btnLastPage.classList.add('page-badge-secondary');
            }
        }

        if (btnLastPage.textContent === "1") {
            btnLastPage.classList.remove('page-badge');
            btnLastPage.classList.add('page-badge-secondary');
        }

        // Texto "de X" en el selector
        if (totalPagesLabel) totalPagesLabel.textContent = totalPaginasGlobal;

        // Llenar el <select> para las páginas intermedias
        if (pageSelect) {
            pageSelect.innerHTML = '';
            for (let i = 1; i <= totalPaginasGlobal; i++) {
                const option = document.createElement('option');
                option.value = i;
                option.textContent = i;
                if (i === paginaActual) option.selected = true;
                pageSelect.appendChild(option);
            }
        }

        // Estado habilitado/deshabilitado de las flechas (< >)
        if (btnPrevPage) btnPrevPage.disabled = (paginaActual === 1);
        if (btnNextPage) btnNextPage.disabled = !tieneMasPaginas;
    }

    // 4. LÓGICA DE NAVEGACIÓN
    async function irAPagina(destinoPagina) {
        if (destinoPagina === paginaActual || destinoPagina < 1 || destinoPagina > totalPaginasGlobal) return;

        if (destinoPagina === 1) {
            // Para la página 1 no hace falta iterar; reseteamos el cursor
            await cargarAulas(true);
            return;
        }

        if (destinoPagina > paginaActual) {
            // Avanzar recopilando los cursores paso a paso si es necesario
            while (paginaActual < destinoPagina && tieneMasPaginas) {
                if (historialCursores.length === paginaActual) {
                    historialCursores.push(siguienteCursor);
                }
                paginaActual++;
            }
        } else {
            paginaActual = destinoPagina;
        }
        await cargarAulas(false);
    }

    // Flechas de navegación
    if (btnPrevPage) {
        btnPrevPage.addEventListener('click', () => {
            if (paginaActual > 1) {
                paginaActual--;
                cargarAulas(false);
            }
        });
    }

    if (btnNextPage) {
        btnNextPage.addEventListener('click', () => {
            if (tieneMasPaginas) {
                if (historialCursores.length === paginaActual) {
                    historialCursores.push(siguienteCursor);
                }
                paginaActual++;
                cargarAulas(false);
            }
        });
    }

    // Botón directo "1" (Ir a la primera página)
    if (btnFirstPage) {
        btnFirstPage.addEventListener('click', () => {
            if (paginaActual !== 1) {
                irAPagina(1);
            }
        });
    }

    // Botón directo "Última página" (Ir al total de páginas)
    if (btnLastPage) {
        btnLastPage.addEventListener('click', () => {
            if (paginaActual !== totalPaginasGlobal) {
                irAPagina(totalPaginasGlobal);
            }
        });
    }

    // Desplegable SELECT (Ir a cualquier página intermedia)
    if (pageSelect) {
        pageSelect.addEventListener('change', (e) => {
            const seleccionada = parseInt(e.target.value);
            irAPagina(seleccionada);
        });
    }

    // 5. BÚSQUEDA
    if (btnSearch) btnSearch.addEventListener('click', () => cargarAulas(true));
    if (searchInput) {
        searchInput.addEventListener('keyup', (e) => {
            if (e.key === 'Enter') cargarAulas(true);
        });
    }

    // 6. ELIMINACIÓN MASIVA
    if (btnEliminar) {
        btnEliminar.addEventListener('click', async () => {
            const seleccionados = Array.from(document.querySelectorAll('.aula-checkbox:checked'))
                .map(cb => parseInt(cb.value));

            if (seleccionados.length === 0) {
                alert('Por favor, selecciona al menos un aula para eliminar.');
                return;
            }

            if (!confirm(`¿Estás seguro de que deseas eliminar las ${seleccionados.length} aula(s) seleccionada(s)?`)) {
                return;
            }

            try {
                const promesas = seleccionados.map(id =>
                    fetch(`/api/Aulas/${id}`, {
                        method: 'DELETE',
                        headers: { 'Content-Type': 'application/json' }
                    })
                );

                await Promise.all(promesas);
                cargarAulas(true);
            } catch (err) {
                console.error('Error durante la eliminación masiva:', err);
                alert('Ocurrió un error al intentar eliminar las aulas.');
            }
        });
    }

    // 7. MODAL CREAR / EDITAR
    if (btnNuevaAula) {
        btnNuevaAula.addEventListener('click', () => {
            modalTitle.textContent = 'Nueva Aula';
            aulaIdInput.value = '';
            nombreAulaInput.value = '';
            aulaModal.classList.add('active');
        });
    }

    if (btnCloseModal) {
        btnCloseModal.addEventListener('click', () => {
            aulaModal.classList.remove('active');
        });
    }

    if (aulaForm) {
        aulaForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const id = aulaIdInput.value;
            const nombre = nombreAulaInput.value.trim();

            if (!nombre) return;

            const isEdit = Boolean(id);
            const endpoint = isEdit ? `/api/Aulas/${id}` : '/api/Aulas/crear';
            const method = isEdit ? 'PUT' : 'POST';

            try {
                const response = await fetch(endpoint, {
                    method: method,
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ nombre: nombre })
                });

                if (response.ok) {
                    aulaModal.classList.remove('active');
                    cargarAulas(true);
                } else if (response.status === 401) {
                    window.location.href = 'index.html';
                } else {
                    alert('No se pudo guardar el aula.');
                }
            } catch (err) {
                console.error('Error al guardar el aula:', err);
            }
        });
    }

    // Variables globales
    let recursosLista = [];
    let recursoSeleccionadoId = null;
    const btnAsignarRecurso = document.getElementById('btnAsignarRecurso');

    // Helper para obtener las aulas marcadas en la tabla
    function obtenerAulasSeleccionadas() {
        const checkboxes = document.querySelectorAll('.aula-checkbox:checked');
        return Array.from(checkboxes).map(cb => parseInt(cb.value));
    }

    if (btnAsignarRecurso) {
        btnAsignarRecurso.addEventListener('click', abrirModalAsignarRecurso);
    }

    // 1. Abrir modal y cargar recursos
    async function abrirModalAsignarRecurso() {
        const aulasIds = obtenerAulasSeleccionadas();

        if (aulasIds.length === 0) {
            alert("Por favor, selecciona al menos un aula de la tabla.");
            return;
        }

        // Actualizar indicador de cantidad de aulas de forma segura
        const spanContador = document.querySelector('#lblAulasSeleccionadas span');
        if (spanContador) {
            spanContador.textContent = aulasIds.length;
        }

        // Resetear formulario con comprobación nula
        const txtBuscar = document.getElementById('txtBuscarRecursoModal');
        if (txtBuscar) txtBuscar.value = '';

        recursoSeleccionadoId = null;

        const btnConfirmar = document.getElementById('btnConfirmarAsignacion');
        if (btnConfirmar) btnConfirmar.disabled = true;

        // Mostrar modal
        const modal = document.getElementById('modalAsignarRecurso');
        if (modal) {
            modal.classList.add('active');
        } else {
            console.error("No se encontró el elemento HTML con id 'modalAsignarRecurso'");
            return;
        }

        // Cargar recursos desde el API
        await cargarMisRecursos();
    }

    // 2. Fetch al endpoint GET mis-recursos
    async function cargarMisRecursos() {
        const contenedor = document.getElementById('listaRecursosModal');
        if (!contenedor) return;

        contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0;">Cargando recursos...</p>';

        try {
            // Obtener el token de autenticación (Ajusta la clave según como guardes el token en el login)
            const token = localStorage.getItem('token') || sessionStorage.getItem('token');

            const headers = {
                'Accept': 'application/json'
            };

            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            console.log(headers);

            const response = await fetch('/api/Aulas/recursos/mis-recursos', {
                method: 'GET',
                headers: headers
            });

            if (!response.ok) {
                throw new Error(`HTTP Error: ${response.status}`);
            }

            const result = await response.json();
            console.log("Respuesta obtenida de mis-recursos:", result);

            if (result && Array.isArray(result.datos)) {
                recursosLista = result.datos;
            } else if (Array.isArray(result)) {
                recursosLista = result;
            } else {
                recursosLista = [];
            }

            renderizarListaRecursos(recursosLista);

        } catch (error) {
            console.error("Error en cargarMisRecursos:", error);
            contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0; color: var(--danger-red);">Error al cargar recursos</p>';
        }
    }

    // 3. Renderizar items de lista con opción de selección
    function renderizarListaRecursos(lista) {
        const contenedor = document.getElementById('listaRecursosModal');
        if (!contenedor) return;

        contenedor.innerHTML = '';

        if (!lista || lista.length === 0) {
            contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0;">No se encontraron recursos</p>';
            return;
        }

        lista.forEach(r => {
            const item = document.createElement('div');

            // Declaración explícita de la variable
            const esSeleccionado = (recursoSeleccionadoId === r.id);

            item.className = `recurso-item ${esSeleccionado ? 'selected' : ''}`;

            const fechaFormateada = r.creadoEn ? new Date(r.creadoEn).toLocaleDateString() : '';

            item.innerHTML = `
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
                <div>
                    <div style="font-weight: 600;">${r.titulo || 'Sin título'}</div>
                    <div class="subtitle" style="font-size: 0.78rem;">${fechaFormateada}</div>
                </div>
                ${esSeleccionado ? '<span style="color: var(--primary-blue); font-weight: 700;">✓</span>' : ''}
            </div>
        `;

            item.addEventListener('click', () => {
                recursoSeleccionadoId = r.id;

                const btnConfirmar = document.getElementById('btnConfirmarAsignacion');
                if (btnConfirmar) btnConfirmar.disabled = false;

                // Re-renderizar la lista para actualizar el ícono de verificación visual (✓)
                renderizarListaRecursos(lista);
            });

            contenedor.appendChild(item);
        });
    }

    // 4. Búsqueda y filtrado por coincidencias
    document.getElementById('txtBuscarRecursoModal')?.addEventListener('input', (e) => {
        const termino = e.target.value.toLowerCase().trim();
        const filtrados = recursosLista.filter(r => r.titulo.toLowerCase().includes(termino));
        renderizarListaRecursos(filtrados);
    });

    // 5. Enviar asignación al endpoint POST
    document.getElementById('btnConfirmarAsignacion')?.addEventListener('click', async () => {
        const aulaIds = obtenerAulasSeleccionadas();

        if (!recursoSeleccionadoId || aulaIds.length === 0) return;

        const payload = {
            recursoId: [recursoSeleccionadoId], // Array con 1 recurso según especificación de tu backend
            aulaIds: aulaIds
        };

        try {
            const response = await fetch('/api/Aulas/recursos/asignar-aulas', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                alert('¡Recurso asignado exitosamente a las aulas!');
                cerrarModalAsignacion();
                if (typeof cargarAulas === 'function') cargarAulas(true);
            } else {
                const err = await response.json();
                alert(`Error: ${err.mensaje || 'No se pudo asignar el recurso.'}`);
            }
        } catch (error) {
            console.error(error);
            alert('Ocurrió un error al procesar la solicitud.');
        }
    });

    // Cerrar Modal
    function cerrarModalAsignacion() {
        const modal = document.getElementById('modalAsignarRecurso');
        if (modal) {
            modal.classList.remove('active');
        }
    }

    document.getElementById('btnCancelarAsignacion')?.addEventListener('click', cerrarModalAsignacion);

    // Asignar evento al botón "Asignar Recurso" superior
    document.getElementById('btnAbrirAsignarRecurso')?.addEventListener('click', abrirModalAsignarRecurso);

    // Variables globales para Tests
    let testsLista = [];
    let testSeleccionadoId = null;

    // 1. Abrir Modal de Asignación de Tests
    async function abrirModalAsignarTest() {
        const aulasIds = obtenerAulasSeleccionadas();

        if (aulasIds.length === 0) {
            alert("Por favor, selecciona al menos un aula de la tabla.");
            return;
        }

        const spanContador = document.querySelector('#lblAulasSeleccionadasTest span');
        if (spanContador) spanContador.textContent = aulasIds.length;

        const txtBuscar = document.getElementById('txtBuscarTestModal');
        if (txtBuscar) txtBuscar.value = '';

        testSeleccionadoId = null;

        const btnConfirmar = document.getElementById('btnConfirmarAsignacionTest');
        if (btnConfirmar) btnConfirmar.disabled = true;

        const modal = document.getElementById('modalAsignarTest');
        if (modal) {
            modal.classList.add('active');
        }

        await cargarMisTests();
    }

    // 2. Fetch al nuevo endpoint GET mis-tests
    async function cargarMisTests() {
        const contenedor = document.getElementById('listaTestsModal');
        if (!contenedor) return;

        contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0;">Cargando tests...</p>';

        try {
            const token = localStorage.getItem('token') || sessionStorage.getItem('token');
            const headers = { 'Accept': 'application/json' };
            if (token) headers['Authorization'] = `Bearer ${token}`;

            const response = await fetch('/api/Tests/mis-tests', {
                method: 'GET',
                headers: headers
            });

            if (!response.ok) throw new Error(`HTTP Error: ${response.status}`);

            const result = await response.json();

            if (result && Array.isArray(result.datos)) {
                testsLista = result.datos;
            } else if (Array.isArray(result)) {
                testsLista = result;
            } else {
                testsLista = [];
            }

            renderizarListaTests(testsLista);

        } catch (error) {
            console.error("Error al cargar tests:", error);
            contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0; color: var(--danger-red);">Error al cargar tests</p>';
        }
    }

    // 3. Renderizar items de la lista de tests
    function renderizarListaTests(lista) {
        const contenedor = document.getElementById('listaTestsModal');
        if (!contenedor) return;

        contenedor.innerHTML = '';

        if (!lista || lista.length === 0) {
            contenedor.innerHTML = '<p class="subtitle text-center" style="padding: 16px 0;">No se encontraron tests</p>';
            return;
        }

        lista.forEach(t => {
            const item = document.createElement('div');
            const esSeleccionado = (testSeleccionadoId === t.testId);

            item.className = `recurso-item ${esSeleccionado ? 'selected' : ''}`;

            const fechaFormateada = t.creadoEn ? new Date(t.creadoEn).toLocaleDateString() : '';

            item.innerHTML = `
            <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
                <div>
                    <div style="font-weight: 600;">${t.nombre || 'Sin nombre'}</div>
                    <div class="subtitle" style="font-size: 0.78rem;">Preguntas: ${t.totalPreguntas || 0} | Creado: ${fechaFormateada}</div>
                </div>
                ${esSeleccionado ? '<span style="color: var(--primary-blue); font-weight: 700;">✓</span>' : ''}
            </div>
        `;

            item.addEventListener('click', () => {
                testSeleccionadoId = t.testId;

                const btnConfirmar = document.getElementById('btnConfirmarAsignacionTest');
                if (btnConfirmar) btnConfirmar.disabled = false;

                renderizarListaTests(lista);
            });

            contenedor.appendChild(item);
        });
    }

    // 4. Búsqueda y filtrado de tests
    document.getElementById('txtBuscarTestModal')?.addEventListener('input', (e) => {
        const termino = e.target.value.toLowerCase().trim();
        const filtrados = testsLista.filter(t => t.nombre.toLowerCase().includes(termino));
        renderizarListaTests(filtrados);
    });

    // 5. Enviar asignación al endpoint POST /api/Tests/asignar-test
    document.getElementById('btnConfirmarAsignacionTest')?.addEventListener('click', async () => {
        const aulaIds = obtenerAulasSeleccionadas();

        if (!testSeleccionadoId || aulaIds.length === 0) return;

        const payload = {
            testId: testSeleccionadoId,
            aulaIds: aulaIds
        };

        try {
            const token = localStorage.getItem('token') || sessionStorage.getItem('token');
            const headers = {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            };
            if (token) headers['Authorization'] = `Bearer ${token}`;

            const response = await fetch('/api/Tests/asignar-test', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                alert('¡Test asignado exitosamente a las aulas!');
                cerrarModalAsignarTest();

                if (typeof cargarAulas === 'function') {
                    cargarAulas(true);
                }
            } else {
                const err = await response.json();
                alert(`Error: ${err.mensaje || 'No se pudo asignar el test.'}`);
            }
        } catch (error) {
            console.error(error);
            alert('Ocurrió un error al procesar la solicitud.');
        }
    });

    // 6. Cerrar Modal
    function cerrarModalAsignarTest() {
        const modal = document.getElementById('modalAsignarTest');
        if (modal) {
            modal.classList.remove('active');
        }
    }

    // Event Listeners para abrir/cerrar modal
    document.getElementById('btnCancelarAsignacionTest')?.addEventListener('click', cerrarModalAsignarTest);
    document.getElementById('btnAsignarTest')?.addEventListener('click', abrirModalAsignarTest);
    document.getElementById('btnAbrirAsignarTest')?.addEventListener('click', abrirModalAsignarTest);

    // 8. ACCIONES DE FILA GLOBALES
    window.editarAula = function (id, nombreActual) {
        modalTitle.textContent = 'Editar Aula';
        aulaIdInput.value = id;
        nombreAulaInput.value = nombreActual;
        aulaModal.classList.add('active');
    };

    window.verDetalle = function (id) {
        window.location.href = `aula-detalle.html?id=${id}`;
    };

    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', (e) => {
            const checkboxes = document.querySelectorAll('.aula-checkbox');
            checkboxes.forEach(cb => cb.checked = e.target.checked);
        });
    }

    // 9. CERRAR SESIÓN
    if (btnLogout) {
        btnLogout.addEventListener('click', async () => {
            try {
                await fetch('/api/Auth/logout', { method: 'POST' });
            } catch (err) {
                console.error('Error al cerrar sesión:', err);
            } finally {
                window.location.href = 'index.html';
            }
        });
    }
});