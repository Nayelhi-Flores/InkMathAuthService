document.addEventListener('DOMContentLoaded', () => {
    initLayout();
});

function initLayout() {
    const currentPage = window.location.pathname.split('/').pop() || 'aulas.html';

    // Inyectar Sidebar
    const sidebarContainer = document.querySelector('.sidebar');
    if (sidebarContainer) {
        sidebarContainer.innerHTML = `
            <div class="brand">
                <img src="icons/V2.svg" alt="InkMath Logo" class="brand-logo">
            </div>
            <nav class="sidebar-nav">
                <a href="aulas.html" class="nav-item ${currentPage === 'aulas.html' ? 'active' : ''}">
                    <img src="icons/bx-school.svg" alt="" class="nav-icon">
                    <span>Aulas</span>
                </a>
                <a href="evaluaciones.html" class="nav-item ${currentPage === 'evaluaciones.html' ? 'active' : ''}">
                    <img src="icons/bx-rocket.svg" alt="" class="nav-icon">
                    <span>Evaluaciones</span>
                </a>
                <a href="recursos.html" class="nav-item ${currentPage === 'recursos.html' ? 'active' : ''}">
                    <img src="icons/bx-folder.svg" alt="" class="nav-icon">
                    <span>Recursos</span>
                </a>
                <a href="configuracion.html" class="nav-item ${currentPage === 'configuracion.html' ? 'active' : ''}">
                    <img src="icons/bx-cog.svg" alt="" class="nav-icon">
                    <span>Configuración</span>
                </a>
            </nav>
            <div class="sidebar-footer">
                <button id="btnLogout" class="btn-logout">
                    <img src="icons/bx-power.svg" alt="" class="logout-icon">
                    <span>Cerrar Sesión</span>
                </button>
            </div>
        `;

        document.getElementById('btnLogout')?.addEventListener('click', cerrarSesion);
    }

    // Cargar datos de Perfil Global en Topbar
    cargarPerfilTopbar();
}

async function cargarPerfilTopbar() {
    const token = localStorage.getItem('token');
    if (!token) {
        window.location.href = 'login.html';
        return;
    }

    try {
        const response = await fetch('/api/Usuarios/me', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.status === 401) {
            cerrarSesion();
            return;
        }

        if (response.ok) {
            const user = await response.json();
            const profileName = document.getElementById('profileName');
            const profileInitials = document.getElementById('profileInitials');
            const profileRole = document.querySelector('.user-role');

            if (profileName) profileName.textContent = user.nombre;
            if (profileRole) profileRole.textContent = user.rolNombre;
            if (profileInitials) {
                const iniciales = user.nombre.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
                profileInitials.textContent = iniciales;
            }
        }
    } catch (err) {
        console.error('Error cargando perfil del topbar:', err);
    }
}

function cerrarSesion() {
    localStorage.removeItem('token');
    window.location.href = 'login.html';
}