document.addEventListener('DOMContentLoaded', () => {
    const urlParams = new URLSearchParams(window.location.search);
    const isExpressVariant = urlParams.get('variante') === 'directa' || urlParams.get('variante') === 'express';

    let currentStep = 1;
    let selectedRolId = null;

    // Elementos DOM
    const groupCodigoClase = document.getElementById('groupCodigoClase');
    const progressBarContainer = document.getElementById('progressBarContainer');
    const stepSubtitle = document.getElementById('stepSubtitle');
    const registerForm = document.getElementById('registerForm');

    const btnBack = document.getElementById('btnBack');
    const btnNext = document.getElementById('btnNext');
    const btnSubmit = document.getElementById('btnSubmit');

    const passwordInput = document.getElementById('passwordInput');
    const btnTogglePass = document.getElementById('btnTogglePass');
    const imgEyeIcon = document.getElementById('imgEyeIcon');
    const codigoClaseInput = document.getElementById('codigoClaseInput');
    const lblCodigoClase = document.getElementById('lblCodigoClase');

    const PATH_EYE = 'icons/bx-eye.svg';
    const PATH_EYE_SLASH = 'icons/bx-eye-slash.svg';

    // Ocultar campo de clase por defecto en flujo normal
    if (!isExpressVariant && groupCodigoClase) {
        groupCodigoClase.style.display = 'none';
    }

    // Ajustes para la variante Exprés
    if (isExpressVariant) {
        progressBarContainer.style.display = 'none';
        selectedRolId = 3; // Estudiante
        currentStep = 3; // Muestra directamente Nombre, Apellido y Código
        codigoClaseInput.value = 'DEFAULT-CLASS'; // Código por defecto para exprés
        lblCodigoClase.textContent = 'Código de Clase';
        if (groupCodigoClase) groupCodigoClase.style.display = 'flex';
        updateStepUI();
        stepSubtitle.textContent = 'Registro exprés de estudiante';
    }

    // Toggle Contraseña
    btnTogglePass.addEventListener('click', () => {
        const isPassword = passwordInput.getAttribute('type') === 'password';
        passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
        imgEyeIcon.setAttribute('src', isPassword ? PATH_EYE : PATH_EYE_SLASH);
    });

    // Validación visual de requisitos de Contraseña
    passwordInput.addEventListener('input', () => {
        const val = passwordInput.value;
        toggleRule('ruleLength', val.length >= 8);
        toggleRule('ruleUpper', /[A-Z]/.test(val));
        toggleRule('ruleNumber', /[0-9]/.test(val));
        toggleRule('ruleSpecial', /[+*@#$!%&?_]/.test(val));
    });

    function toggleRule(elementId, isValid) {
        const el = document.getElementById(elementId);
        if (isValid) {
            el.classList.add('valid');
        } else {
            el.classList.remove('valid');
        }
    }

    // Selección de Rol
    window.selectRole = function (rolId, element) {
        selectedRolId = rolId;
        document.querySelectorAll('.role-card').forEach(card => card.classList.remove('selected'));
        element.classList.add('selected');
        document.getElementById('roleError').textContent = '';

        // Ocultar o mostrar el campo según el rol seleccionado (3 = Estudiante)
        if (groupCodigoClase) {
            groupCodigoClase.style.display = (rolId === 3) ? 'flex' : 'none';
        }
    };

    // Navegación
    btnNext.addEventListener('click', () => {
        if (validateCurrentStep()) {
            currentStep++;
            updateStepUI();
        }
    });

    btnBack.addEventListener('click', () => {
        currentStep--;
        updateStepUI();
    });

    // Validaciones por Paso
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

    // Actualización de Vista por Paso
    function updateStepUI() {
        document.querySelectorAll('.step-content').forEach(el => el.classList.remove('active'));
        document.getElementById(`step${currentStep}`).classList.add('active');

        if (currentStep === 3 && !isExpressVariant && groupCodigoClase) {
            groupCodigoClase.style.display = (selectedRolId === 3) ? 'flex' : 'none';
        }

        if (!isExpressVariant) {
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

        btnBack.style.display = (currentStep === 1 || (isExpressVariant && currentStep === 3)) ? 'none' : 'block';
        btnNext.style.display = currentStep === 3 ? 'none' : 'block';
        btnSubmit.style.display = currentStep === 3 ? 'block' : 'none';
    }

    // Envío del Formulario
    registerForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const nombre = document.getElementById('nombreInput').value.trim();
        const apellido = document.getElementById('apellidoInput').value.trim();
        const nombreError = document.getElementById('nombreError');
        const apellidoError = document.getElementById('apellidoError');

        nombreError.textContent = '';
        apellidoError.textContent = '';

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

        const endpoint = isExpressVariant ? '/api/Auth/registro-express' : '/api/Auth/registro';

        // Petición multipart/form-data requerida por la API
        const formData = new FormData();
        formData.append('Nombre', nombre);
        formData.append('Apellido', apellido);

        if (isExpressVariant) {
            formData.append('CodigoClase', codigoClaseInput.value.trim() || 'DEFAULT-CLASS');
        } else {
            formData.append('Email', document.getElementById('emailInput').value.trim());
            formData.append('Password', passwordInput.value.trim());
            formData.append('RoleId', selectedRolId);
            formData.append('AceptoTerminos', document.getElementById('termsCheck').checked);

            if (selectedRolId === 3) {
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

                if (isExpressVariant) {
                    if (data && data.token) {
                        localStorage.setItem('token', data.token);
                    }
                    window.location.href = 'estudiante.html';
                } else {
                    window.location.href = 'index.html';
                }
            } else {
                const errorData = await response.json().catch(() => null);
                document.getElementById('codigoClaseError').textContent = errorData?.mensaje || 'Error al procesar el registro.';
            }
        } catch (error) {
            console.error('Error de red:', error);
            document.getElementById('codigoClaseError').textContent = 'Error de conexión con el servidor.';
        }
    });
});