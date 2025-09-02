// End Users Management JavaScript
// Core functionality for managing end users

const utilUrl = "https://cdn.jsdelivr.net/npm/intl-tel-input@25.8.3/build/js/utils.js";

const errorMap = {
    0: "Invalid number",
    1: "Invalid country code",
    2: "Too short",
    3: "Too long",
    4: "Invalid number"
};

function wirePhone(inputEl, errorEl, defaultFallback = "sa") {
    if (!inputEl) return null;

    const iti = window.intlTelInput(inputEl, {
        initialCountry: "auto",
        strictMode: true,
        hiddenInput: () => ({ phone: "PhoneNumber" }),
        geoIpLookup: cb => {
            fetch("https://ipapi.co/json")
                .then(r => r.json())
                .then(d => cb(d.country_code))
                .catch(() => cb(defaultFallback.toLowerCase()));
        },
        loadUtils: () => import("https://cdn.jsdelivr.net/npm/intl-tel-input@25.8.3/build/js/utils.js"),
    });

    const doValidate = () => {
        const v = inputEl.value.trim();
        if (!v) {
            inputEl.setCustomValidity("Required");
            if (errorEl) { errorEl.textContent = "Required"; errorEl.classList.remove("d-none"); }
            return false;
        }
        // avoid false negatives before utils is ready
        if (iti.promise && iti.promise.pending) return true;

        if (iti.isValidNumber()) {
            inputEl.setCustomValidity("");
            if (errorEl) errorEl.classList.add("d-none");
            document.getElementById('hiddenPhoneNumber').value = iti.getNumber();
            return true;
        } else {
            const err = iti.getValidationError();
            const msg = errorMap[err] || "Invalid phone";
            inputEl.setCustomValidity(msg);
            if (errorEl) { errorEl.textContent = msg; errorEl.classList.remove("d-none"); }
            return false;
        }
    };

    // mark pending until resolved
    if (iti.promise) iti.promise.pending = true;
    iti.promise?.then(() => { iti.promise.pending = false; });

    inputEl.addEventListener("keyup", doValidate);
    inputEl.addEventListener("input", () => {
        inputEl.setCustomValidity("");
        errorEl?.classList.add("d-none");
    });
    inputEl.addEventListener("countrychange", doValidate);

    return { iti, validate: doValidate };
}

// --- Create phone ---
const createPhoneElm = document.querySelector("#PhoneNumber");
const createPhoneError = document.querySelector("#createPhoneError");
const createPhone = wirePhone(createPhoneElm, createPhoneError, "sa");

// --- Edit phone ---
const editPhoneElm = document.getElementById("editPhone");
const editPhoneError = document.getElementById("editPhoneError"); // add this <small> in your edit modal
const editPhone = wirePhone(editPhoneElm, editPhoneError, "sa");


// User editing functionality
function editUser(id, name, phone, email, imageUrl, startDate, endDate) {
    document.getElementById('editId').value = id;
    document.getElementById('editName').value = name;
    editPhone.iti.setNumber(phone || '');
    document.getElementById('editEmail').value = email;
    document.getElementById('editImageUrl').value = imageUrl;
    // Dates are already converted to KSA format in the server
    document.getElementById('editStartDate').value = startDate;
    document.getElementById('editEndDate').value = endDate;

    // Clear duration field and calculation alert when editing existing user
    document.getElementById('editDurationDays').value = '';
    document.getElementById('editDateCalculationAlert').classList.add('d-none');

    new bootstrap.Modal(document.getElementById('editModal')).show();
}

// Date calculation functions
function calculateEndDate() {
    const startDateInput = document.getElementById('createStartDate');
    const durationInput = document.getElementById('createDurationDays');
    const endDateInput = document.getElementById('createEndDate');
    const alert = document.getElementById('dateCalculationAlert');
    const details = document.getElementById('calculationDetails');

    if (startDateInput.value && durationInput.value) {
        const startDate = new Date(startDateInput.value);
        const days = parseInt(durationInput.value);

        // Calculate end date by adding days
        const endDate = new Date(startDate);
        endDate.setDate(endDate.getDate() + (days - 1));

        // Format date for input (YYYY-MM-DD)
        const formattedEndDate = endDate.toISOString().split('T')[0];
        endDateInput.value = formattedEndDate;

        // Show calculation details
        details.textContent = `${days} day${days > 1 ? 's' : ''} from ${startDate.toLocaleDateString()} = ${endDate.toLocaleDateString()}`;
        alert.classList.remove('d-none');
    } else {
        alert.classList.add('d-none');
    }
}

function calculateEditEndDate() {
    const startDateInput = document.getElementById('editStartDate');
    const durationInput = document.getElementById('editDurationDays');
    const endDateInput = document.getElementById('editEndDate');
    const alert = document.getElementById('editDateCalculationAlert');
    const details = document.getElementById('editCalculationDetails');

    if (startDateInput.value && durationInput.value) {
        const startDate = new Date(startDateInput.value);
        const days = parseInt(durationInput.value);

        // Calculate end date by adding days
        const endDate = new Date(startDate);
        endDate.setDate(endDate.getDate() + (days - 1));

        // Format date for input (YYYY-MM-DD)
        const formattedEndDate = endDate.toISOString().split('T')[0];
        endDateInput.value = formattedEndDate;

        // Show calculation details
        details.textContent = `${days} day${days > 1 ? 's' : ''} from ${startDate.toLocaleDateString()} = ${endDate.toLocaleDateString()}`;
        alert.classList.remove('d-none');
    } else {
        alert.classList.add('d-none');
    }
}

function clearDuration() {
    const durationInput = document.getElementById('createDurationDays');
    const alert = document.getElementById('dateCalculationAlert');

    // Clear the duration field to indicate manual override
    durationInput.value = '';
    alert.classList.add('d-none');
}

function clearEditDuration() {
    const durationInput = document.getElementById('editDurationDays');
    const alert = document.getElementById('editDateCalculationAlert');

    // Clear the duration field to indicate manual override
    durationInput.value = '';
    alert.classList.add('d-none');
}

// Pagination and search functions
function changePageSize(pageSize) {
    const url = new URL(window.location);
    url.searchParams.set('pageSize', pageSize);
    url.searchParams.set('page', '1'); // Reset to first page when changing page size
    window.location.href = url.toString();
}

function resetSearch() {
    // This will be replaced with the actual URL in the view
    window.location.href = window.endUsersBaseUrl || '/Admin/EndUsers';
}

// Stop subscription functionality
function showStopSubscriptionModal(userId, userName, userPhone) {
    document.getElementById('stopEndUserId').value = userId;
    document.getElementById('stopUserName').textContent = userName;
    document.getElementById('stopUserPhone').textContent = userPhone;

    new bootstrap.Modal(document.getElementById('stopSubscriptionModal')).show();
}

function submitStopSubscription() {
    const form = document.getElementById('stopSubscriptionForm');
    const endUserId = document.getElementById('stopEndUserId').value;
    const stopReason = document.getElementById('stopReason').value.trim();

    if (!stopReason) {
        showErrorAlert('Please provide a reason for stopping the subscription.');
        return;
    }

    if (stopReason.length > 500) {
        showErrorAlert('Reason cannot exceed 500 characters.');
        return;
    }

    // Create form data for submission
    const formData = new FormData();
    formData.append('EndUserId', endUserId);
    formData.append('StopReason', stopReason);

    // Submit via fetch to handle the response
    fetch('/Admin/StopSubscription', {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        }
    })
        .then(response => {
            if (response.ok) {
                // Success - reload the page to show updated data
                window.location.reload();
            } else {
                // Handle error response
                response.text().then(text => {
                    showErrorAlert('Failed to stop subscription. Please try again.');
                });
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showErrorAlert('An error occurred while stopping the subscription.');
        });

    // Close the modal
    bootstrap.Modal.getInstance(document.getElementById('stopSubscriptionModal')).hide();
}

function showStopDetails(stoppedDate, reason) {
    document.getElementById('stopDetailsDate').textContent = stoppedDate;
    document.getElementById('stopDetailsReason').textContent = reason;

    new bootstrap.Modal(document.getElementById('stopDetailsModal')).show();
}

// Alert functions
function showSuccessAlert(message) {
    showAlert(message, 'success');
}

function showErrorAlert(message) {
    showAlert(message, 'danger');
}

function showAlert(message, type) {
    // Remove existing alerts
    const existingAlerts = document.querySelectorAll('.dynamic-alert');
    existingAlerts.forEach(alert => alert.remove());

    // Create new alert
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show position-fixed dynamic-alert`;
    alertDiv.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    alertDiv.innerHTML = `
        <i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'}"></i>
        <strong>${type === 'success' ? 'Success!' : 'Error!'}</strong> ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    document.body.appendChild(alertDiv);

    // Remove alert after 5 seconds
    setTimeout(() => {
        if (alertDiv && alertDiv.parentNode) {
            alertDiv.remove();
        }
    }, 5000);
}

// Initialize page when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    // Set default start date to today when create modal opens
    const createModal = document.getElementById('createModal');
    if (createModal) {
        createModal.addEventListener('show.bs.modal', function () {
            const today = new Date().toISOString().split('T')[0];
            document.getElementById('createStartDate').value = today;

            // Set default duration to empty
            document.getElementById('createDurationDays').value = '';

            // Calculate initial end date
            calculateEndDate();
        });

        // Reset form when modal closes
        createModal.addEventListener('hidden.bs.modal', function () {
            document.querySelector('#createModal form').reset();
            document.getElementById('dateCalculationAlert').classList.add('d-none');
        });
    }

    // Reset edit form when modal closes
    const editModal = document.getElementById('editModal');
    if (editModal) {
        editModal.addEventListener('hidden.bs.modal', function () {
            document.getElementById('editDateCalculationAlert').classList.add('d-none');
        });
    }

    // Reset stop subscription form when modal closes
    const stopModal = document.getElementById('stopSubscriptionModal');
    if (stopModal) {
        stopModal.addEventListener('hidden.bs.modal', function () {
            document.getElementById('stopSubscriptionForm').reset();
        });
    }


    document.getElementById("page-select").value = pageSize;
});
