document.addEventListener('DOMContentLoaded', () => {
    initLayout();
});

const SIDEBAR_STORAGE_KEY = 'sidebarColapsado';

// fg = color del ícono/texto activo, bg = fondo de la "pill" activa (bottom nav móvil)
const NAV_ITEMS = [
    { href: 'aulas.html', icon: 'icons/bx-school.svg', label: 'Aulas', fg: '#D9534F', bg: '#FADADD' },
    { href: 'evaluaciones.html', icon: 'icons/bx-rocket.svg', label: 'Evaluaciones', fg: '#6F4FB8', bg: '#E4DCF7' },
    { href: 'recursos.html', icon: 'icons/bx-folder.svg', label: 'Recursos', fg: '#1A9BAB', bg: '#D2F1F4' },
    { href: 'configuracion.html', icon: 'icons/bx-cog.svg', label: 'Configuración', fg: '#D98A1F', bg: '#FDECC8' },
];

function initLayout() {
    const currentPage = window.location.pathname.split('/').pop() || 'aulas.html';

    initSidebar(currentPage);
    initBottomNav(currentPage);

    // Cargar datos de Perfil Global en Topbar
    cargarPerfilTopbar();
}

// ---------- Sidebar (escritorio) ----------
function initSidebar(currentPage) {
    const appLayout = document.querySelector('.app-layout');
    const sidebarContainer = document.querySelector('.sidebar');
    if (!appLayout || !sidebarContainer) return;

    sidebarContainer.innerHTML = `
        <div class="brand" id="btnToggleLogo" role="button" tabindex="0"
             aria-label="Colapsar o expandir menú" aria-expanded="true" title="Colapsar / Expandir">
            <img src="icons/V2.svg" alt="InkMath" class="brand-logo brand-logo--full">
            <img src="icons/V1.svg" alt="" class="brand-logo brand-logo--mini">
        </div>
        <nav class="sidebar-nav">
            ${NAV_ITEMS.map(i => `
            <a href="${i.href}" class="nav-item ${currentPage === i.href ? 'active' : ''}" title="${i.label}">
                <img src="${i.icon}" alt="" class="nav-icon">
                <span>${i.label}</span>
            </a>`).join('')}
        </nav>
        <div class="sidebar-footer">
            <button id="btnLogout" class="btn-logout" title="Cerrar Sesión">
                <img src="icons/bx-power.svg" alt="" class="logout-icon">
                <span>Cerrar Sesión</span>
            </button>
        </div>
    `;

    document.getElementById('btnLogout')?.addEventListener('click', cerrarSesion);

    const btnToggleLogo = document.getElementById('btnToggleLogo');

    // Restaurar estado guardado SIN animar (evita que el sidebar "se mueva" en cada página)
    appLayout.classList.add('no-transition');
    setSidebarCollapsed(appLayout, btnToggleLogo, localStorage.getItem(SIDEBAR_STORAGE_KEY) === '1');
    requestAnimationFrame(() => {
        requestAnimationFrame(() => appLayout.classList.remove('no-transition'));
    });

    // Colapsar / expandir al hacer clic (o Enter / Espacio) en el logo
    const toggle = () => {
        const colapsado = !appLayout.classList.contains('sidebar-collapsed');
        setSidebarCollapsed(appLayout, btnToggleLogo, colapsado);
        localStorage.setItem(SIDEBAR_STORAGE_KEY, colapsado ? '1' : '0');
    };

    btnToggleLogo?.addEventListener('click', toggle);
    btnToggleLogo?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            toggle();
        }
    });
}

function setSidebarCollapsed(appLayout, btn, collapsed) {
    appLayout.classList.toggle('sidebar-collapsed', collapsed);
    btn?.setAttribute('aria-expanded', String(!collapsed));
}

// ---------- Bottom nav (tablet / móvil) ----------
function initBottomNav(currentPage) {
    let bottomNav = document.querySelector('.bottom-nav');
    if (!bottomNav) {
        bottomNav = document.createElement('nav');
        bottomNav.className = 'bottom-nav';
        bottomNav.setAttribute('aria-label', 'Navegación principal');
        document.body.appendChild(bottomNav);
    }

    // El ícono se pinta con mask-image (inline, para que la URL relativa se
    // resuelva contra el HTML) y así toma el color del ítem activo.
    bottomNav.innerHTML = NAV_ITEMS.map(i => {
        const active = currentPage === i.href;
        return `
            <a href="${i.href}" class="bottom-nav-item ${active ? 'active' : ''}"
               style="--fg:${i.fg}; --bg:${i.bg};"
               aria-label="${i.label}" ${active ? 'aria-current="page"' : ''}>
                <span class="bottom-nav-icon"
                      style="-webkit-mask-image:url('${i.icon}'); mask-image:url('${i.icon}');"></span>
                ${active ? `<span class="bottom-nav-label">${i.label}</span>` : ''}
            </a>`;
    }).join('');
}

async function cargarPerfilTopbar() {
    const token = localStorage.getItem('token');
    if (!token) {
        window.location.href = 'index.html';
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

// ---------- Cerrar sesión llamando al endpoint ----------
async function cerrarSesion() {
    const token = localStorage.getItem('token');

    if (token) {
        try {
            await fetch('/api/Auth/logout', {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
        } catch (err) {
            console.error('Error al notificar cierre de sesión al servidor:', err);
        }
    }

    // Limpieza local y redirección
    localStorage.removeItem('token');
    window.location.href = 'index.html';
}