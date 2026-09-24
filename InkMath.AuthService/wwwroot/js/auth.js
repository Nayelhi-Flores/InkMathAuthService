document.addEventListener('DOMContentLoaded', () => {
    const passwordInput = document.getElementById('passwordInput');
    const btnTogglePass = document.getElementById('btnTogglePass');
    const imgEyeIcon = document.getElementById('imgEyeIcon');
    const loginForm = document.getElementById('loginForm');

    const PATH_EYE = 'icons/bx-eye.svg';
    const PATH_EYE_SLASH = 'icons/bx-eye-slash.svg';

    // Alternar visibilidad de la contraseña
    btnTogglePass.addEventListener('click', () => {
        const isPassword = passwordInput.getAttribute('type') === 'password';

        passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
        imgEyeIcon.setAttribute('src', isPassword ? PATH_EYE : PATH_EYE_SLASH);
        imgEyeIcon.setAttribute('alt', isPassword ? 'Mostrar contraseña' : 'Ocultar contraseña');
    });

    // Envío del formulario y petición al backend
    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const email = document.getElementById('emailInput').value.trim();
        const password = passwordInput.value.trim();
        const emailError = document.getElementById('emailError');
        const passwordError = document.getElementById('passwordError');

        let isValid = true;
        emailError.textContent = '';
        passwordError.textContent = '';

        if (!email) {
            emailError.textContent = 'El correo es requerido.';
            isValid = false;
        }

        if (!password) {
            passwordError.textContent = 'La contraseña es requerida.';
            isValid = false;
        }

        if (!isValid) return;

        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    email: email,
                    password: password
                })
            });

            if (response.ok) {
                const data = await response.json();

                // Guardar token/sesión si el backend retorna JWT o datos de usuario
                if (data.token) {
                    localStorage.setItem('token', data.token);
                }

                let rolId = data.rolId || data.idRol;

                // Si la respuesta JSON no trae la propiedad de forma explícita, la extraemos del JWT
                if (!rolId && data.token) {
                    try {
                        const payloadBase64 = data.token.split('.')[1];
                        const decodedPayload = JSON.parse(atob(payloadBase64));

                        // Busca la claim de rol por ID numérico o claim de tipo role
                        rolId = parseInt(decodedPayload.rolId || decodedPayload.idRol || decodedPayload.role || decodedPayload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]);
                    } catch (e) {
                        console.error('Error al decodificar el token:', e);
                    }
                }

                // Validación por ID numérico (1: Admin, 2: Maestro, 3: Estudiante)
                if (rolId === 2) {
                    window.location.href = 'aulas.html';
                } else if (rolId === 3) {
                    window.location.href = 'estudiante.html';
                } else if (rolId === 1) {
                    window.location.href = 'admin.html';
                } else {
                    passwordError.textContent = 'Rol de usuario no reconocido o sin asignación.';
                }
            } else {
                const errorData = await response.json().catch(() => null);
                const mensajeError = errorData?.mensaje || 'Credenciales incorrectas o usuario no encontrado.';
                passwordError.textContent = mensajeError;
            }

        } catch (error) {
            console.error('Error en la conexión con el servidor:', error);
            passwordError.textContent = 'Error de conexión con el servidor. Inténtalo más tarde.';
        }
    });
});