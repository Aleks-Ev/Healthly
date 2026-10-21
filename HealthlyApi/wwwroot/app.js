// ==========================================================================
// HEALTHLY FRONTEND APP - API INTEGRATION & THEMING LOGIC
// ==========================================================================

document.addEventListener('DOMContentLoaded', () => {
    
    // --- DOM Elements ---
    const apiUrlInput = document.getElementById('apiUrlInput');
    const btnCheckStatus = document.getElementById('btnCheckStatus');
    const apiStatusBadge = document.getElementById('apiStatusBadge');
    const btnFetchUsers = document.getElementById('btnFetchUsers');
    const usersTableBody = document.getElementById('usersTableBody');
    const addUserForm = document.getElementById('addUserForm');
    const formAlert = document.getElementById('formAlert');

    // Theme Picker DOM
    const themeBtn = document.getElementById('themeBtn');
    const themeDropdown = document.getElementById('themeDropdown');
    const colorDots = document.querySelectorAll('.color-dot');
    const customColorInput = document.getElementById('customColor');

    // Predefined Color Schemes
    const themes = {
        mint: { primary: '#32c596', hover: '#29b085', light: '#eafaf4' },
        blue: { primary: '#2563eb', hover: '#1d4ed8', light: '#eff6ff' },
        emerald: { primary: '#059669', hover: '#047857', light: '#ecfdf5' },
        purple: { primary: '#7c3aed', hover: '#6d28d9', light: '#f5f3ff' },
        amber: { primary: '#d97706', hover: '#b45309', light: '#fffbeb' }
    };

    // ==========================================================================
    // 1. THEME & COLOR CUSTOMIZATION LOGIC
    // ==========================================================================

    // Toggle Dropdown
    themeBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        themeDropdown.classList.toggle('show');
    });

    document.addEventListener('click', () => {
        themeDropdown.classList.remove('show');
    });

    themeDropdown.addEventListener('click', (e) => {
        e.stopPropagation();
    });

    // Apply Theme Function
    function applyPrimaryColor(primaryHex, hoverHex = null, lightHex = null) {
        document.documentElement.style.setProperty('--primary-color', primaryHex);
        
        if (hoverHex) {
            document.documentElement.style.setProperty('--primary-hover', hoverHex);
        } else {
            // Generate basic hover effect
            document.documentElement.style.setProperty('--primary-hover', primaryHex);
        }
        
        if (lightHex) {
            document.documentElement.style.setProperty('--primary-light', lightHex);
        }
    }

    // Preset dots click
    colorDots.forEach(dot => {
        dot.addEventListener('click', () => {
            const themeKey = dot.getAttribute('data-theme');
            if (themes[themeKey]) {
                const t = themes[themeKey];
                applyPrimaryColor(t.primary, t.hover, t.light);
                customColorInput.value = t.primary;
            }
        });
    });

    // Custom Color Input Change
    customColorInput.addEventListener('input', (e) => {
        applyPrimaryColor(e.target.value);
    });


    // ==========================================================================
    // 2. BACKEND API INTEGRATION LOGIC
    // ==========================================================================

    function getBaseUrl() {
        return apiUrlInput.value.replace(/\/$/, ''); // Remove trailing slash
    }

    // --- Check API Connection ---
    async function checkApiStatus() {
        apiStatusBadge.className = 'status-badge badge-connecting';
        apiStatusBadge.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Подключение...';

        try {
            const response = await fetch(`${getBaseUrl()}/api/users`, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (response.ok) {
                apiStatusBadge.className = 'status-badge badge-online';
                apiStatusBadge.innerHTML = '<i class="fa-solid fa-circle-check"></i> API Доступен (200 OK)';
            } else {
                throw new Error(`Ошибка сервера: ${response.status}`);
            }
        } catch (error) {
            apiStatusBadge.className = 'status-badge badge-offline';
            apiStatusBadge.innerHTML = '<i class="fa-solid fa-circle-xmark"></i> Нет связи с API';
            console.error('API Error:', error);
        }
    }

    // --- Fetch & Render Users List (GET /api/users) ---
    async function fetchUsers() {
        usersTableBody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center text-muted">
                    <i class="fa-solid fa-spinner fa-spin"></i> Загрузка пользователей...
                </td>
            </tr>
        `;

        try {
            const response = await fetch(`${getBaseUrl()}/api/users`);
            if (!response.ok) throw new Error('Не удалось загрузить данных');

            const users = await response.json();

            if (users.length === 0) {
                usersTableBody.innerHTML = `
                    <tr>
                        <td colspan="6" class="text-center text-muted">
                            База данных пуста. Добавьте первого пользователя через форму слева.
                        </td>
                    </tr>
                `;
                return;
            }

            // Render Table Rows
            usersTableBody.innerHTML = users.map(u => `
                <tr>
                    <td><strong>#${u.id}</strong></td>
                    <td>
                        <div><strong>${escapeHtml(u.fullName)}</strong></div>
                        <div class="text-muted" style="font-size:11px;">${escapeHtml(u.email)}</div>
                    </td>
                    <td><span class="status-badge" style="background:#e2e8f0; font-size:11px;">${escapeHtml(u.role)}</span></td>
                    <td>${u.gender ? escapeHtml(u.gender) : '—'}</td>
                    <td>
                        ${u.height ? u.height + ' см' : '—'} / ${u.weight ? u.weight + ' кг' : '—'}
                    </td>
                    <td class="text-muted" style="font-size:11px;">
                        ${new Date(u.createdAt).toLocaleDateString('ru-RU')}
                    </td>
                </tr>
            `).join('');

        } catch (error) {
            usersTableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center" style="color:var(--error-color);">
                        <i class="fa-solid fa-triangle-exclamation"></i> Ошибка при получении данных
                    </td>
                </tr>
            `;
            console.error('Fetch Users Error:', error);
        }
    }

    // --- Create User (POST /api/users) ---
    addUserForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        showAlert('', 'hidden');

        // Build Payload matching C# User model
        const payload = {
            email: document.getElementById('userEmail').value,
            fullName: document.getElementById('userName').value,
            role: document.getElementById('userRole').value,
            gender: document.getElementById('userGender').value,
            passwordHash: "default_temporary_hash", // Фейковый хэш для тестирования
            height: parseFloat(document.getElementById('userHeight').value) || null,
            weight: parseFloat(document.getElementById('userWeight').value) || null
        };

        try {
            const response = await fetch(`${getBaseUrl()}/api/users`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (response.ok || response.status === 201) {
                showAlert('Пользователь успешно сохранен в PostgreSQL!', 'alert-success');
                addUserForm.reset();
                fetchUsers(); // Refresh Table
            } else {
                const errData = await response.json().catch(() => null);
                showAlert(`Ошибка сохранения (${response.status})`, 'alert-danger');
            }
        } catch (error) {
            showAlert('Ошибка подключения при отправке данных', 'alert-danger');
            console.error('Post User Error:', error);
        }
    });

    // Helper: Show Alert
    function showAlert(msg, className) {
        formAlert.className = `alert-box ${className}`;
        formAlert.textContent = msg;
    }

    // Helper: Escape HTML
    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    // --- Event Listeners ---
    btnCheckStatus.addEventListener('click', checkApiStatus);
    btnFetchUsers.addEventListener('click', fetchUsers);

    // Initial Auto-Check
    checkApiStatus();
});