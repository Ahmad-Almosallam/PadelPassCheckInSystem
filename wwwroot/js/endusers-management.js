// End Users Management JavaScript
// Core functionality for managing end users

// User editing functionality
function editUser(id, name, phone, email, imageUrl, startDate, endDate) {
    document.getElementById('editId').value = id;
    document.getElementById('editName').value = name;
    document.getElementById('editPhone').value = phone;
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

// Sync Rekaz functionality
function startSyncRekaz() {
    // Show progress indicators
    const progressDiv = document.getElementById('syncRekazProgress');
    const resultDiv = document.getElementById('syncRekazResult');
    const startButton = document.getElementById('startSyncRekazBtn');
    
    // Reset UI state
    progressDiv.classList.remove('d-none');
    resultDiv.innerHTML = '';
    startButton.disabled = true;
    startButton.innerHTML = '<i class="bi bi-hourglass-split"></i> Syncing...';
    
    // Call the sync API
    fetch('/Admin/SyncRekaz', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        }
    })
    .then(async response => {
        const data = await response.json();
        
        // Hide progress indicator
        progressDiv.classList.add('d-none');
        
        if (response.ok && data.success) {
            // Show success result
            resultDiv.innerHTML = `
                <div class="alert alert-success">
                    <h6><i class="bi bi-check-circle"></i> Sync Completed Successfully!</h6>
                    <p class="mb-2">${data.message}</p>
                    <hr>
                    <small class="text-muted">
                        <i class="bi bi-info-circle"></i> 
                        ${data.syncedCount} customer${data.syncedCount !== 1 ? 's' : ''} synced from Rekaz.
                    </small>
                </div>
            `;
            
            // Update button to allow closing
            startButton.innerHTML = '<i class="bi bi-check-circle"></i> Sync Complete';
            startButton.disabled = false;
            startButton.setAttribute('data-bs-dismiss', 'modal');
            
            // Show success alert
            showSuccessAlert(`Successfully synced ${data.syncedCount} Rekaz customers!`);
            
            // Auto-close modal after 3 seconds and reload page to show updated data
            setTimeout(() => {
                bootstrap.Modal.getInstance(document.getElementById('syncRekazModal')).hide();
                window.location.reload();
            }, 3000);
        } else {
            // Show error result
            const errorMessage = data.message || 'An error occurred during sync.';
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <h6><i class="bi bi-exclamation-triangle"></i> Sync Failed</h6>
                    <p class="mb-0">${errorMessage}</p>
                </div>
            `;
            
            // Reset button
            startButton.innerHTML = '<i class="bi bi-arrow-clockwise"></i> Retry Sync';
            startButton.disabled = false;
            startButton.removeAttribute('data-bs-dismiss');
            
            // Show error alert
            showErrorAlert('Failed to sync Rekaz customers. Please try again.');
        }
    })
    .catch(error => {
        console.error('Sync Rekaz Error:', error);
        
        // Hide progress indicator
        progressDiv.classList.add('d-none');
        
        // Show error result
        resultDiv.innerHTML = `
            <div class="alert alert-danger">
                <h6><i class="bi bi-exclamation-triangle"></i> Connection Error</h6>
                <p class="mb-0">Unable to connect to the server. Please check your connection and try again.</p>
            </div>
        `;
        
        // Reset button
        startButton.innerHTML = '<i class="bi bi-arrow-clockwise"></i> Retry Sync';
        startButton.disabled = false;
        startButton.removeAttribute('data-bs-dismiss');
        
        // Show error alert
        showErrorAlert('Connection error occurred during sync. Please try again.');
    });
}

// Reset Sync Rekaz modal when it's opened
document.addEventListener('DOMContentLoaded', function() {
    const syncRekazModal = document.getElementById('syncRekazModal');
    if (syncRekazModal) {
        syncRekazModal.addEventListener('show.bs.modal', function() {
            // Reset modal state when opened
            const progressDiv = document.getElementById('syncRekazProgress');
            const resultDiv = document.getElementById('syncRekazResult');
            const startButton = document.getElementById('startSyncRekazBtn');
            
            progressDiv.classList.add('d-none');
            resultDiv.innerHTML = '';
            startButton.disabled = false;
            startButton.innerHTML = '<i class="bi bi-cloud-arrow-down"></i> Start Sync';
            startButton.removeAttribute('data-bs-dismiss');
        });
    }
});

// Initialize page when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
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
