document.addEventListener('DOMContentLoaded', () => {
    initLayout();
});

const SIDEBAR_STORAGE_KEY = 'sidebarColapsado';

// fg = color del ícono/texto activo, bg = fondo de la "pill" activa (bottom nav móvil)
const NAV_ITEMS = [
    { href: 'aulas', icon: 'icons/bx-school.svg', label: 'Aulas', fg: '#076394', bg: '#DCEFFA' },
    { href: 'evaluaciones', icon: 'icons/bx-rocket.svg', label: 'Evaluaciones', fg: '#8F3E15', bg: '#FDE8DC' },
    { href: 'recursos', icon: 'icons/bx-folder.svg', label: 'Recursos', fg: '#2A7A47', bg: '#DDF3E5' },
    { href: 'configuracion', icon: 'icons/bx-cog.svg', label: 'Configuración', fg: '#6B4700', bg: '#FFF3D6' },
];

// Navegación del panel de Administrador (body[data-nav="admin"]): el rol 1 no usa las pantallas de maestro.
const ADMIN_NAV_ITEMS = [
    { href: 'dashboard', icon: 'icons/bx-grid-alt.svg', label: 'Dashboard', fg: '#076394', bg: '#DCEFFA' },
    { href: 'admin', icon: 'icons/bx-cog.svg', label: 'Panel Admin', fg: '#8F3E15', bg: '#FDE8DC' },
    { href: 'auditoria', icon: 'icons/bx-shield-quarter.svg', label: 'Auditoría', fg: '#2A7A47', bg: '#DDF3E5' },
];

function getNavItems() {
    return document.body.dataset.nav === 'admin' ? ADMIN_NAV_ITEMS : NAV_ITEMS;
}

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
            ${getNavItems().map(i => `
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

    document.getElementById('btnLogout')?.addEventListener('click', confirmarCierreSesion);

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
    const itemsHtml = getNavItems().map(i => {
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

    // Cerrar sesión: en escritorio vive en el sidebar, que se oculta en vista reducida
    const logoutHtml = `
            <button type="button" id="btnLogoutMobile" class="bottom-nav-item bottom-nav-logout"
                    style="--fg:#B42318; --bg:#FDE2E0;" aria-label="Cerrar sesión" title="Cerrar sesión">
                <span class="bottom-nav-icon"
                      style="-webkit-mask-image:url('icons/bx-power.svg'); mask-image:url('icons/bx-power.svg');"></span>
            </button>`;

    bottomNav.innerHTML = itemsHtml + logoutHtml;
    document.getElementById('btnLogoutMobile')?.addEventListener('click', confirmarCierreSesion);
}

async function cargarPerfilTopbar() {
    try {
        const response = await fetch('/api/Usuarios/me');

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

// ---------- Confirmación previa (usa UI.confirm si ui-modal.js está cargado) ----------
async function confirmarCierreSesion() {
    if (window.UI && typeof UI.confirm === 'function') {
        const ok = await UI.confirm('¿Quieres cerrar tu sesión?', {
            title: 'Cerrar sesión',
            type: 'question',
            confirmText: 'Cerrar sesión',
            cancelText: 'Cancelar'
        });
        if (!ok) return;
    }
    await cerrarSesion();
}

// ---------- Cerrar sesión llamando al endpoint ----------
async function cerrarSesion() {
    try {
        await fetch('/api/Auth/logout', { method: 'POST' });
    } catch (err) {
        console.error('Error al cerrar sesión:', err);
    }
    window.location.href = '/';
}