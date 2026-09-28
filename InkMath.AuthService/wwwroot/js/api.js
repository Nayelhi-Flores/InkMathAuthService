async function fetchConAuth(url, options = {}) {
    const token = localStorage.getItem('token');

    const headers = {
        'Content-Type': 'application/json',
        ...(options.headers || {}),
        ...(token ? { 'Authorization': `Bearer ${token}` } : {})
    };

    const response = await fetch(url, { ...options, headers });

    // Si el token expiró o no es válido (HTTP 401), redirigir al login
    if (response.status === 401) {
        localStorage.removeItem('token');
        localStorage.removeItem('usuario_id');
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