document.addEventListener('DOMContentLoaded', () => {
    cargarDatosPerfil();

    document.getElementById('formPerfil').addEventListener('submit', guardarPerfil);
    document.getElementById('btnSimularSuscripcion').addEventListener('click', simularSuscripcion);
    document.getElementById('btnDarseDeBaja').addEventListener('click', darseDeBaja);
});

async function cargarDatosPerfil() {
    const token = localStorage.getItem('token');
    try {
        const response = await fetch('/api/Usuarios/me', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const user = await response.json();
            document.getElementById('txtNombre').value = user.nombre;
            document.getElementById('txtApellido').value = user.apellido;
            document.getElementById('txtEmail').value = user.email;
            document.getElementById('lblTipoSuscripcion').textContent = user.tipoSuscripcion;
        }
    } catch (err) {
        console.error('Error al cargar datos del perfil:', err);
    }
}

async function guardarPerfil(e) {
    e.preventDefault();
    const token = localStorage.getItem('token');
    const nuevoNombre = document.getElementById('txtNombre').value;

    try {
        const response = await fetch('/api/Usuarios/me', {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ nombre: nuevoNombre })
        });

        if (response.ok) {
            alert('Perfil actualizado con éxito');
            cargarDatosPerfil();
            if (typeof cargarPerfilTopbar === 'function') cargarPerfilTopbar();
        } else {
            const err = await response.json();
            alert(err.mensaje || 'Error al actualizar perfil');
        }
    } catch (err) {
        console.error('Error:', err);
    }
}

async function simularSuscripcion() {
    if (!confirm('¿Deseas activar una suscripción de prueba por 30 días?')) return;

    const token = localStorage.getItem('token');
    try {
        const response = await fetch('/api/Usuarios/simular-suscripcion', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ planId: 1 }) // ID 1 representa el plan premium de demostración
        });

        if (response.ok) {
            alert('¡Suscripción simulada correctamente!');
            cargarDatosPerfil();
        } else {
            const err = await response.json();
            alert(err.mensaje || 'Error al simular la suscripción');
        }
    } catch (err) {
        console.error('Error:', err);
    }
}

async function darseDeBaja() {
    const confirmacion = confirm('¿Estás seguro de que deseas darte de baja? Esta acción desactivará tu acceso a la plataforma.');
    if (!confirmacion) return;

    const token = localStorage.getItem('token');
    try {
        const response = await fetch('/api/Usuarios/darse-de-baja', {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            alert('Tu cuenta ha sido desactivada.');
            localStorage.removeItem('token');
            window.location.href = 'login.html';
        } else {
            const err = await response.json();
            alert(err.mensaje || 'Error al procesar la baja');
        }
    } catch (err) {
        console.error('Error:', err);
    }
}