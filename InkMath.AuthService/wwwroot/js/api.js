async function fetchConAuth(url, options = {}) {
    const esForm = options.body instanceof FormData;
    const response = await fetch(url, {
        credentials: 'same-origin',
        ...options,
        headers: { ...(esForm ? {} : { 'Content-Type': 'application/json' }), ...(options.headers || {}) }
    });
    if (response.status === 401) {
        window.location.href = '/index.html';
        throw new Error('Sesión no autorizada o expirada.');
    }
    return response;
}

/**
 * Muestra mensajes de error en consola o interfaz
 */
function mostrarMensajeError(mensaje) {
    console.error(mensaje);
    const contenedorError = document.getElementById('mensajeError');
    if (contenedorError) {
        contenedorError.textContent = mensaje;
        contenedorError.style.display = 'block';
    }
}