document.addEventListener('DOMContentLoaded', () => {
    cargarDatosPerfil();

    document.getElementById('formPerfil').addEventListener('submit', guardarPerfil);
    document.getElementById('btnSimularSuscripcion').addEventListener('click', simularSuscripcion);
    document.getElementById('btnDarseDeBaja').addEventListener('click', darseDeBaja);
});

async function cargarDatosPerfil() {
    try {
        const response = await fetch('/api/Usuarios/me');

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
    const nuevoNombre = document.getElementById('txtNombre').value;
    const nuevoApellido = document.getElementById('txtApellido').value;

    try {
        const response = await fetch('/api/Usuarios/me', {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ nombre: nuevoNombre, apellido: nuevoApellido })
        });

        if (response.ok) {
            UI.toast('Perfil actualizado con éxito', 'success');
            cargarDatosPerfil();
            if (typeof cargarPerfilTopbar === 'function') cargarPerfilTopbar();
        } else {
            const err = await response.json();
            UI.alert(err.mensaje || 'Error al actualizar perfil', { type: 'danger' });
        }
    } catch (err) {
        console.error('Error:', err);
    }
}

async function simularSuscripcion() {
    if (!(await UI.confirm('¿Deseas activar una suscripción de prueba por 30 días?', { confirmText: 'Activar' }))) return;

    try {
        const response = await fetch('/api/Usuarios/prueba-suscripcion', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ planId: 3 })
        });

        if (response.ok) {
            UI.toast('¡Suscripción de prueba obtenida correctamente!', 'success');
            cargarDatosPerfil();
        } else {
            const err = await response.json();
            UI.alert(err.mensaje || 'Error al obtener la suscripción', { type: 'danger' });
        }
    } catch (err) {
        console.error('Error:', err);
    }
}

async function darseDeBaja() {
    const confirmacion = await UI.confirm('¿Estás seguro de que deseas darte de baja? Esta acción desactivará tu acceso a la plataforma.', { danger: true, confirmText: 'Darme de baja', title: 'Darse de baja' });
    if (!confirmacion) return;

    try {
        const response = await fetch('/api/Usuarios/darse-de-baja', {
            method: 'DELETE'});

        if (response.ok) {
            await UI.alert('Tu cuenta ha sido desactivada.', { type: 'success', title: 'Cuenta desactivada' });
            window.location.href = '/';
        } else {
            const err = await response.json();
            UI.alert(err.mensaje || 'Error al procesar la baja', { type: 'danger' });
        }
    } catch (err) {
        console.error('Error:', err);
    }
}