/**
 * ========================================
 * ABC RETAIL - MAIN APPLICATION JAVASCRIPT
 * ========================================
 */

(function () {
    'use strict';

    // ========================================
    // TOAST NOTIFICATION SYSTEM
    // ========================================
    class ToastManager {
        constructor() {
            this.container = this.createContainer();
            this.defaultDuration = 5000;
        }

        createContainer() {
            let container = document.querySelector('.toast-container');
            if (!container) {
                container = document.createElement('div');
                container.className = 'toast-container';
                document.body.appendChild(container);
            }
            return container;
        }

        show(title, message, type = 'info', duration = this.defaultDuration) {
            const toast = document.createElement('div');
            const icons = {
                success: '✅',
                error: '❌',
                warning: '⚠️',
                info: 'ℹ️'
            };

            toast.className = `toast toast-${type}`;
            toast.innerHTML = `
                <span class="toast-icon">${icons[type] || icons.info}</span>
                <div class="toast-content">
                    <div class="toast-title">${title}</div>
                    <div class="toast-message">${message}</div>
                </div>
                <button class="toast-close" aria-label="Close">&times;</button>
            `;

            this.container.appendChild(toast);

            // Auto-remove after duration
            const timeoutId = setTimeout(() => {
                this.remove(toast);
            }, duration);

            // Close button handler
            toast.querySelector('.toast-close').addEventListener('click', () => {
                clearTimeout(timeoutId);
                this.remove(toast);
            });

            // Click outside to close
            toast.addEventListener('click', (e) => {
                if (e.target === toast) {
                    clearTimeout(timeoutId);
                    this.remove(toast);
                }
            });

            return toast;
        }

        remove(toast) {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(100%)';
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.remove();
                }
            }, 300);
        }

        success(title, message, duration) {
            return this.show(title, message, 'success', duration);
        }

        error(title, message, duration) {
            return this.show(title, message, 'error', duration);
        }

        warning(title, message, duration) {
            return this.show(title, message, 'warning', duration);
        }

        info(title, message, duration) {
            return this.show(title, message, 'info', duration);
        }
    }

    // ========================================
    // CONFIRM DIALOG
    // ========================================
    function showConfirmDialog(message, title = 'Confirm Action', confirmText = 'Confirm', cancelText = 'Cancel') {
        return new Promise((resolve) => {
            const overlay = document.createElement('div');
            overlay.style.cssText = `
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0, 0, 0, 0.5);
                backdrop-filter: blur(4px);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 10000;
                animation: fadeIn 0.3s ease;
            `;

            const dialog = document.createElement('div');
            dialog.style.cssText = `
                background: #fff;
                border-radius: 12px;
                padding: 2rem;
                max-width: 400px;
                width: 90%;
                box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
                animation: slideDown 0.3s ease;
            `;

            dialog.innerHTML = `
                <h3 style="margin-top: 0; color: #0f172a;">${title}</h3>
                <p style="color: #475569; margin: 1rem 0;">${message}</p>
                <div style="display: flex; gap: 0.75rem; justify-content: flex-end; margin-top: 1.5rem;">
                    <button class="btn btn-secondary" id="cancelBtn">${cancelText}</button>
                    <button class="btn btn-danger" id="confirmBtn">${confirmText}</button>
                </div>
            `;

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            const close = (result) => {
                overlay.remove();
                resolve(result);
            };

            dialog.querySelector('#confirmBtn').addEventListener('click', () => close(true));
            dialog.querySelector('#cancelBtn').addEventListener('click', () => close(false));
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) close(false);
            });

            // Keyboard support
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') close(false);
                if (e.key === 'Enter') close(true);
            }, { once: true });
        });
    }

    // ========================================
    // TABLE SEARCH / FILTER
    // ========================================
    class TableFilter {
        constructor(tableId, searchInputId) {
            this.table = document.getElementById(tableId);
            this.searchInput = document.getElementById(searchInputId);
            if (this.table && this.searchInput) {
                this.init();
            }
        }

        init() {
            this.searchInput.addEventListener('input', (e) => {
                const searchTerm = e.target.value.toLowerCase();
                const rows = this.table.querySelectorAll('tbody tr');

                rows.forEach(row => {
                    const text = row.textContent.toLowerCase();
                    row.style.display = text.includes(searchTerm) ? '' : 'none';
                });

                // Show/hide "no results" message
                const visibleRows = this.table.querySelectorAll('tbody tr[style*="display: none"]');
                let noResults = this.table.querySelector('.no-results');
                if (visibleRows.length === rows.length) {
                    if (!noResults) {
                        noResults = document.createElement('tr');
                        noResults.className = 'no-results';
                        noResults.innerHTML = `<td colspan="99" class="text-center text-muted py-4">No results found</td>`;
                        this.table.querySelector('tbody').appendChild(noResults);
                    }
                    noResults.style.display = '';
                } else if (noResults) {
                    noResults.style.display = 'none';
                }
            });
        }
    }

    // ========================================
    // FORM VALIDATION HELPER
    // ========================================
    class FormValidator {
        constructor(formId) {
            this.form = document.getElementById(formId);
            if (this.form) {
                this.init();
            }
        }

        init() {
            this.form.addEventListener('submit', (e) => {
                if (!this.validate()) {
                    e.preventDefault();
                }
            });

            // Real-time validation on blur
            this.form.querySelectorAll('input, select, textarea').forEach(field => {
                field.addEventListener('blur', () => {
                    this.validateField(field);
                });
                field.addEventListener('input', () => {
                    if (field.classList.contains('is-invalid') || field.classList.contains('is-valid')) {
                        this.validateField(field);
                    }
                });
            });
        }

        validate() {
            let isValid = true;
            this.form.querySelectorAll('input[required], select[required], textarea[required]').forEach(field => {
                if (!this.validateField(field)) {
                    isValid = false;
                }
            });
            return isValid;
        }

        validateField(field) {
            const isValid = field.checkValidity();
            field.classList.remove('is-valid', 'is-invalid');
            field.classList.add(isValid ? 'is-valid' : 'is-invalid');

            // Show/hide error message
            let error = field.parentElement.querySelector('.invalid-feedback');
            if (!isValid) {
                if (!error) {
                    error = document.createElement('div');
                    error.className = 'invalid-feedback';
                    field.parentElement.appendChild(error);
                }
                error.textContent = field.validationMessage || 'This field is required';
            } else if (error) {
                error.remove();
            }

            return isValid;
        }
    }

    // ========================================
    // IMAGE PREVIEW
    // ========================================
    function setupImagePreview(fileInputId, previewId) {
        const input = document.getElementById(fileInputId);
        const preview = document.getElementById(previewId);

        if (input && preview) {
            input.addEventListener('change', (e) => {
                const file = e.target.files[0];
                if (file) {
                    const reader = new FileReader();
                    reader.onload = (e) => {
                        preview.src = e.target.result;
                        preview.style.display = 'block';
                        if (preview.parentElement.querySelector('.no-preview')) {
                            preview.parentElement.querySelector('.no-preview').style.display = 'none';
                        }
                    };
                    reader.readAsDataURL(file);
                } else {
                    preview.style.display = 'none';
                    if (preview.parentElement.querySelector('.no-preview')) {
                        preview.parentElement.querySelector('.no-preview').style.display = 'block';
                    }
                }
            });
        }
    }

    // ========================================
    // DYNAMIC FORM ROWS (for order items)
    // ========================================
    class DynamicFormRows {
        constructor(containerId, addButtonId, template, maxRows = 20) {
            this.container = document.getElementById(containerId);
            this.addButton = document.getElementById(addButtonId);
            this.template = template;
            this.maxRows = maxRows;
            this.rowCount = 0;

            if (this.container && this.addButton) {
                this.init();
            }
        }

        init() {
            this.rowCount = this.container.querySelectorAll('.item-row').length;

            this.addButton.addEventListener('click', () => {
                this.addRow();
            });

            this.container.addEventListener('click', (e) => {
                if (e.target.classList.contains('remove-item')) {
                    this.removeRow(e.target);
                }
            });

            // Delegate input events for calculations
            this.container.addEventListener('input', (e) => {
                if (e.target.classList.contains('quantity') || e.target.classList.contains('unit-price')) {
                    this.calculateTotal();
                }
            });

            this.container.addEventListener('change', (e) => {
                if (e.target.classList.contains('product-select')) {
                    this.populateProductDetails(e.target);
                }
            });
        }

        addRow() {
            if (this.rowCount >= this.maxRows) {
                const toast = new ToastManager();
                toast.warning('Limit Reached', `Maximum ${this.maxRows} items allowed`);
                return;
            }

            const newRow = document.createElement('div');
            newRow.className = 'row mb-2 item-row animate-fade-in';
            newRow.innerHTML = this.template(this.rowCount);
            this.container.appendChild(newRow);
            this.rowCount++;
            this.calculateTotal();

            // Focus the first input in the new row
            newRow.querySelector('input')?.focus();
        }

        removeRow(button) {
            const row = button.closest('.item-row');
            if (this.container.querySelectorAll('.item-row').length > 1) {
                row.style.opacity = '0';
                row.style.transform = 'translateX(-20px)';
                setTimeout(() => {
                    row.remove();
                    this.rowCount--;
                    this.calculateTotal();
                }, 300);
            } else {
                const toast = new ToastManager();
                toast.warning('Cannot Remove', 'You must have at least one item');
            }
        }

        populateProductDetails(select) {
            const row = select.closest('.item-row');
            const selectedOption = select.options[select.selectedIndex];
            const productName = selectedOption.text;
            const price = selectedOption.dataset.price;

            const nameInput = row.querySelector('.product-name');
            const priceInput = row.querySelector('.unit-price');

            if (nameInput) nameInput.value = productName;
            if (priceInput) priceInput.value = price || 0;

            this.calculateTotal();
        }

        calculateTotal() {
            let total = 0;
            this.container.querySelectorAll('.item-row').forEach(row => {
                const quantity = parseFloat(row.querySelector('.quantity')?.value) || 0;
                const price = parseFloat(row.querySelector('.unit-price')?.value) || 0;
                total += quantity * price;
            });

            const totalInput = document.getElementById('TotalAmount');
            if (totalInput) {
                totalInput.value = total.toFixed(2);
                // Trigger change event for any listeners
                totalInput.dispatchEvent(new Event('change'));
            }

            return total;
        }
    }

    // ========================================
    // AUTO-DISMISS ALERTS
    // ========================================
    function setupAutoDismissAlerts(selector = '.alert', duration = 5000) {
        document.querySelectorAll(selector).forEach(alert => {
            const closeBtn = alert.querySelector('.btn-close');
            if (closeBtn) {
                closeBtn.addEventListener('click', () => {
                    alert.style.opacity = '0';
                    alert.style.transform = 'translateY(-20px)';
                    setTimeout(() => alert.remove(), 300);
                });
            }

            // Auto-dismiss after duration
            setTimeout(() => {
                if (alert.parentNode) {
                    alert.style.opacity = '0';
                    alert.style.transform = 'translateY(-20px)';
                    setTimeout(() => {
                        if (alert.parentNode) alert.remove();
                    }, 300);
                }
            }, duration);
        });
    }

    // ========================================
    // COPY TO CLIPBOARD
    // ========================================
    function copyToClipboard(text, successMessage = 'Copied to clipboard!') {
        if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(() => {
                const toast = new ToastManager();
                toast.success('Copied!', successMessage);
            }).catch(() => {
                fallbackCopy(text);
            });
        } else {
            fallbackCopy(text);
        }
    }

    function fallbackCopy(text) {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();
        try {
            document.execCommand('copy');
            const toast = new ToastManager();
            toast.success('Copied!', 'Copied to clipboard!');
        } catch (err) {
            const toast = new ToastManager();
            toast.error('Error', 'Failed to copy to clipboard');
        }
        document.body.removeChild(textarea);
    }

    // ========================================
    // ANIMATE ON SCROLL
    // ========================================
    function setupScrollAnimations(selector = '.animate-on-scroll') {
        const elements = document.querySelectorAll(selector);

        if (elements.length === 0) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animate-fade-in');
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        });

        elements.forEach(el => observer.observe(el));
    }

    // ========================================
    // LOADING OVERLAY
    // ========================================
    class LoadingOverlay {
        constructor() {
            this.overlay = null;
        }

        show(message = 'Loading...') {
            if (this.overlay) return;

            this.overlay = document.createElement('div');
            this.overlay.style.cssText = `
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0, 0, 0, 0.6);
                backdrop-filter: blur(4px);
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                z-index: 99999;
                animation: fadeIn 0.3s ease;
            `;

            this.overlay.innerHTML = `
                <div class="spinner" style="width: 50px; height: 50px;"></div>
                <p style="color: #fff; margin-top: 1rem; font-size: 1.1rem;">${message}</p>
            `;

            document.body.appendChild(this.overlay);
        }

        hide() {
            if (this.overlay) {
                this.overlay.style.opacity = '0';
                setTimeout(() => {
                    if (this.overlay?.parentNode) {
                        this.overlay.remove();
                        this.overlay = null;
                    }
                }, 300);
            }
        }

        updateMessage(message) {
            if (this.overlay) {
                const p = this.overlay.querySelector('p');
                if (p) p.textContent = message;
            }
        }
    }

    // ========================================
    // COUNT ANIMATION (for stats)
    // ========================================
    function animateCount(element, target, duration = 2000) {
        const start = 0;
        const startTime = performance.now();

        function update(currentTime) {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            const current = Math.round(start + (target - start) * eased);

            element.textContent = current.toLocaleString();

            if (progress < 1) {
                requestAnimationFrame(update);
            } else {
                element.textContent = target.toLocaleString();
            }
        }

        requestAnimationFrame(update);
    }

    // ========================================
    // DARK MODE TOGGLE (optional)
    // ========================================
    class DarkModeToggle {
        constructor() {
            this.toggle = document.getElementById('darkModeToggle');
            this.isDark = localStorage.getItem('darkMode') === 'true';

            if (this.toggle) {
                this.init();
            }
        }

        init() {
            if (this.isDark) {
                document.documentElement.setAttribute('data-theme', 'dark');
                this.toggle.checked = true;
            }

            this.toggle.addEventListener('change', () => {
                this.isDark = this.toggle.checked;
                document.documentElement.setAttribute('data-theme', this.isDark ? 'dark' : 'light');
                localStorage.setItem('darkMode', this.isDark);
            });
        }
    }

    // ========================================
    // EXPOSE GLOBALLY
    // ========================================
    window.ABC = {
        Toast: ToastManager,
        confirm: showConfirmDialog,
        TableFilter: TableFilter,
        FormValidator: FormValidator,
        setupImagePreview: setupImagePreview,
        DynamicFormRows: DynamicFormRows,
        copyToClipboard: copyToClipboard,
        setupAutoDismissAlerts: setupAutoDismissAlerts,
        setupScrollAnimations: setupScrollAnimations,
        LoadingOverlay: LoadingOverlay,
        animateCount: animateCount,
        DarkModeToggle: DarkModeToggle
    };

    // ========================================
    // AUTO-INITIALIZE ON DOCUMENT READY
    // ========================================
    document.addEventListener('DOMContentLoaded', function () {
        // Auto-dismiss alerts
        setupAutoDismissAlerts('.alert:not(.alert-permanent)', 5000);

        // Setup image previews
        setupImagePreview('imageFile', 'imagePreview');

        // Setup scroll animations
        setupScrollAnimations('.animate-on-scroll');

        // Initialize any table filters
        document.querySelectorAll('[data-table-filter]').forEach(el => {
            const tableId = el.dataset.tableFilter;
            const searchInput = document.getElementById(el.dataset.searchInput || 'searchInput');
            if (tableId) {
                new TableFilter(tableId, searchInput?.id || 'searchInput');
            }
        });

        console.log('🚀 ABC Retail Application initialized successfully!');
    });

})();