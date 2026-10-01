document.addEventListener('DOMContentLoaded', () => {
    const ROL_DOCENTE = 2;
    const ROL_ESTUDIANTE = 3;
    const DEFAULT_CLASS = '';

    // Compatibilidad: ?variante=express (o directa) abre directamente el modo exprés
    const urlParams = new URLSearchParams(window.location.search);
    const startsExpress = ['directa', 'express'].includes(urlParams.get('variante'));

    // Estado
    let currentStep = 1;
    let selectedRolId = null;
    let isExpress = false;

    // Elementos DOM
    const groupCodigoClase = document.getElementById('groupCodigoClase');
    const progressBarContainer = document.getElementById('progressBarContainer');
    const stepSubtitle = document.getElementById('stepSubtitle');
    const registerForm = document.getElementById('registerForm');

    const btnBack = document.getElementById('btnBack');
    const btnNext = document.getElementById('btnNext');
    const btnSubmit = document.getElementById('btnSubmit');

    const expressOffer = document.getElementById('expressOffer');
    const btnExpress = document.getElementById('btnExpress');

    const passwordInput = document.getElementById('passwordInput');
    const btnTogglePass = document.getElementById('btnTogglePass');
    const imgEyeIcon = document.getElementById('imgEyeIcon');
    const codigoClaseInput = document.getElementById('codigoClaseInput');
    const lblCodigoClase = document.getElementById('lblCodigoClase');
    const codigoClaseError = document.getElementById('codigoClaseError');

    const PATH_EYE = 'icons/bx-eye.svg';
    const PATH_EYE_SLASH = 'icons/bx-eye-slash.svg';

    // ---------- Toggle contraseña ----------
    btnTogglePass.addEventListener('click', () => {
        const isPassword = passwordInput.getAttribute('type') === 'password';
        passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
        imgEyeIcon.setAttribute('src', isPassword ? PATH_EYE : PATH_EYE_SLASH);
    });

    // ---------- Validación visual de requisitos de contraseña ----------
    passwordInput.addEventListener('input', () => {
        const val = passwordInput.value;
        toggleRule('ruleLength', val.length >= 8);
        toggleRule('ruleUpper', /[A-Z]/.test(val));
        toggleRule('ruleNumber', /[0-9]/.test(val));
        toggleRule('ruleSpecial', /[+*@#$!%&?_]/.test(val));
    });

    function toggleRule(elementId, isValid) {
        document.getElementById(elementId).classList.toggle('valid', isValid);
    }

    // ---------- Selección de rol ----------
    window.selectRole = function (rolId, element) {
        selectedRolId = rolId;
        document.querySelectorAll('.role-card').forEach(card => card.classList.remove('selected'));
        element.classList.add('selected');
        document.getElementById('roleError').textContent = '';

        // La opción exprés solo existe para estudiantes
        expressOffer.hidden = rolId !== ROL_ESTUDIANTE;
    };

    // ---------- Modo exprés ----------
    function enterExpress() {
        isExpress = true;
        selectedRolId = ROL_ESTUDIANTE;
        currentStep = 3; // Nombre, Apellido y Código de clase
        codigoClaseInput.value = codigoClaseInput.value.trim() || DEFAULT_CLASS;
        lblCodigoClase.textContent = 'Código de Clase';
        codigoClaseError.textContent = '';
        updateStepUI();
    }

    function exitExpress() {
        isExpress = false;
        currentStep = 1; // vuelve a la selección de rol; el rol Estudiante sigue marcado
        if (codigoClaseInput.value.trim() === DEFAULT_CLASS) {
            codigoClaseInput.value = '';
        }
        lblCodigoClase.textContent = 'Código de Clase (Opcional)';
        codigoClaseError.textContent = '';
        updateStepUI();
    }

    btnExpress.addEventListener('click', enterExpress);

    // ---------- Navegación ----------
    btnNext.addEventListener('click', () => {
        if (validateCurrentStep()) {
            currentStep++;
            updateStepUI();
        }
    });

    btnBack.addEventListener('click', () => {
        if (isExpress) {
            exitExpress();
        } else {
            currentStep--;
            updateStepUI();
        }
    });

    // ---------- Validaciones por paso ----------
    function validateCurrentStep() {
        let isValid = true;

        if (currentStep === 1) {
            if (!selectedRolId) {
                document.getElementById('roleError').textContent = 'Por favor selecciona un rol para continuar.';
                isValid = false;
            }
        } else if (currentStep === 2) {
            const email = document.getElementById('emailInput').value.trim();
            const password = passwordInput.value.trim();
            const termsCheck = document.getElementById('termsCheck');

            document.getElementById('emailError').textContent = '';
            document.getElementById('passwordError').textContent = '';
            document.getElementById('termsError').textContent = '';

            if (!email) {
                document.getElementById('emailError').textContent = 'El correo es obligatorio.';
                isValid = false;
            }

            const isPassValid = password.length >= 8 && /[A-Z]/.test(password) && /[0-9]/.test(password) && /[+*@#$!%&?_]/.test(password);
            if (!isPassValid) {
                document.getElementById('passwordError').textContent = 'La contraseña no cumple con los requisitos.';
                isValid = false;
            }

            if (!termsCheck.checked) {
                document.getElementById('termsError').textContent = 'Debes aceptar los términos y condiciones.';
                isValid = false;
            }
        }

        return isValid;
    }

    // ---------- Actualización de la vista ----------
    function updateStepUI() {
        document.querySelectorAll('.step-content').forEach(el => el.classList.remove('active'));
        document.getElementById(`step${currentStep}`).classList.add('active');

        // El código de clase se muestra en exprés y, en el flujo normal, solo para estudiantes
        groupCodigoClase.style.display = (isExpress || selectedRolId === ROL_ESTUDIANTE) ? 'flex' : 'none';

        // La barra de progreso no aplica en exprés
        progressBarContainer.hidden = isExpress;

        if (isExpress) {
            stepSubtitle.textContent = 'Registro exprés de estudiante';
        } else {
            const subtitles = {
                1: 'Paso 1: Selecciona tu tipo de cuenta',
                2: 'Paso 2: Ingresa tu correo y contraseña',
                3: 'Paso 3: Completa tus datos personales'
            };
            stepSubtitle.textContent = subtitles[currentStep];

            document.getElementById('pStep1').classList.toggle('active', currentStep >= 1);
            document.getElementById('pLine1').classList.toggle('active', currentStep >= 2);
            document.getElementById('pStep2').classList.toggle('active', currentStep >= 2);
            document.getElementById('pLine2').classList.toggle('active', currentStep >= 3);
            document.getElementById('pStep3').classList.toggle('active', currentStep >= 3);
        }

        // En exprés, "Atrás" sirve para volver al registro normal
        btnBack.textContent = isExpress ? 'Volver al registro normal' : 'Atrás';
        btnBack.style.display = currentStep === 1 ? 'none' : 'block';
        btnNext.style.display = currentStep === 3 ? 'none' : 'block';
        btnSubmit.style.display = currentStep === 3 ? 'block' : 'none';
    }

    // ---------- Envío del formulario ----------
    registerForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const nombre = document.getElementById('nombreInput').value.trim();
        const apellido = document.getElementById('apellidoInput').value.trim();
        const nombreError = document.getElementById('nombreError');
        const apellidoError = document.getElementById('apellidoError');

        nombreError.textContent = '';
        apellidoError.textContent = '';
        codigoClaseError.textContent = '';

        let isValid = true;
        if (!nombre) {
            nombreError.textContent = 'El nombre es obligatorio.';
            isValid = false;
        }
        if (!apellido) {
            apellidoError.textContent = 'El apellido es obligatorio.';
            isValid = false;
        }

        if (!isValid) return;

        const endpoint = isExpress ? '/api/Auth/registro-express' : '/api/Auth/registro';

        // Petición multipart/form-data requerida por la API
        const formData = new FormData();
        formData.append('Nombre', nombre);
        formData.append('Apellido', apellido);

        if (isExpress) {
            formData.append('CodigoClase', codigoClaseInput.value.trim() || DEFAULT_CLASS);
        } else {
            formData.append('Email', document.getElementById('emailInput').value.trim());
            formData.append('Password', passwordInput.value.trim());
            formData.append('RoleId', selectedRolId);
            formData.append('AceptoTerminos', document.getElementById('termsCheck').checked);

            if (selectedRolId === ROL_ESTUDIANTE) {
                const codigo = codigoClaseInput.value.trim();
                if (codigo) {
                    formData.append('CodigoClase', codigo);
                }
            }
        }

        try {
            const response = await fetch(endpoint, {
                method: 'POST',
                body: formData
            });

            if (response.ok) {
                const data = await response.json().catch(() => null);

                if (isExpress) {
                    if (data && data.token) {
                        localStorage.setItem('token', data.token);
                    }
                    window.location.href = 'juego.html';
                } else {
                    window.location.href = 'index.html';
                }
            } else {
                const errorData = await response.json().catch(() => null);
                codigoClaseError.textContent = errorData?.mensaje || 'Error al procesar el registro.';
            }
        } catch (error) {
            console.error('Error de red:', error);
            codigoClaseError.textContent = 'Error de conexión con el servidor.';
        }
    });

    // ---------- Estado inicial ----------
    if (startsExpress) {
        const studentCard = document.querySelector(`.role-card[data-rol="${ROL_ESTUDIANTE}"]`);
        if (studentCard) window.selectRole(ROL_ESTUDIANTE, studentCard);
        enterExpress();
    } else {
        updateStepUI();
    }
});