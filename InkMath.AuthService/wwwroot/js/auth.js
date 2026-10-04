document.addEventListener('DOMContentLoaded', () => {
    const loginForm = document.getElementById('loginForm');

    // Si no está el formulario de login en esta página, salir silenciosamente
    if (!loginForm) return;

    const passwordInput = document.getElementById('passwordInput');
    const btnTogglePass = document.getElementById('btnTogglePass');
    const imgEyeIcon = document.getElementById('imgEyeIcon');

    const PATH_EYE = 'icons/bx-eye.svg';
    const PATH_EYE_SLASH = 'icons/bx-eye-slash.svg';

    if (btnTogglePass && passwordInput && imgEyeIcon) {
        btnTogglePass.addEventListener('click', () => {
            const isPassword = passwordInput.getAttribute('type') === 'password';
            passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
            imgEyeIcon.setAttribute('src', isPassword ? PATH_EYE : PATH_EYE_SLASH);
            imgEyeIcon.setAttribute('alt', isPassword ? 'Mostrar contraseña' : 'Ocultar contraseña');
        });
    }

    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const emailInput = document.getElementById('emailInput');
        const email = emailInput ? emailInput.value.trim() : '';
        const password = passwordInput ? passwordInput.value.trim() : '';
        const emailError = document.getElementById('emailError');
        const passwordError = document.getElementById('passwordError');

        let isValid = true;
        if (emailError) emailError.textContent = '';
        if (passwordError) passwordError.textContent = '';

        if (!email) {
            if (emailError) emailError.textContent = 'El correo es requerido.';
            isValid = false;
        }

        if (!password) {
            if (passwordError) passwordError.textContent = 'La contraseña es requerida.';
            isValid = false;
        }

        if (!isValid) return;

        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password })
            });

            if (response.ok) {
                const data = await response.json();
                const rolId = Number(data.rolId);

                if (rolId === 2) {
                    window.location.href = 'aulas.html';
                } else if (rolId === 3) {
                    window.location.href = 'juego.html';
                } else if (rolId === 1) {
                    window.location.href = 'admin.html';
                } else if (passwordError) {
                    passwordError.textContent = 'Rol de usuario no reconocido o sin asignación.';
                }
            } else {
                const errorData = await response.json().catch(() => null);
                const mensajeError = errorData?.mensaje || 'Credenciales incorrectas o usuario no encontrado.';
                if (passwordError) passwordError.textContent = mensajeError;
            }

        } catch (error) {
            console.error('Error en la conexión con el servidor:', error);
            if (passwordError) passwordError.textContent = 'Error de conexión con el servidor. Inténtalo más tarde.';
        }
    });
});