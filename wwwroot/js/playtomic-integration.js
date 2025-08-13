// Playtomic Integration JavaScript
// Functions for syncing users with Playtomic

// Playtomic sync preview and management
function previewSync() {
    const previewDiv = document.getElementById('syncPreview');
    const activeUsersSpan = document.getElementById('previewActiveUsers');
    const branchesSpan = document.getElementById('previewBranches');
    const startSyncBtn = document.getElementById('startSyncBtn');

    // Show preview section
    previewDiv.classList.remove('d-none');
    activeUsersSpan.textContent = 'Loading...';
    branchesSpan.textContent = 'Loading...';

    // Get preview data
    fetch('/Admin/GetSyncPreview', {
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        }
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                activeUsersSpan.textContent = data.activeUsers;
                branchesSpan.textContent = data.branches;

                if (data.activeUsers > 0 && data.branches > 0) {
                    startSyncBtn.classList.remove('d-none');
                } else {
                    if (data.activeUsers === 0) {
                        showAlert('No active users found to sync.', 'warning');
                    }
                    if (data.branches === 0) {
                        showAlert('No branches with Playtomic Tenant ID found.', 'warning');
                    }
                }
            } else {
                showAlert('Failed to load sync preview.', 'danger');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error loading sync preview.', 'danger');
        });
}

function displaySyncResults(data) {
    const resultsDiv = document.getElementById('syncResults');

    if (data.success) {
        let resultHtml = `
            <div class="alert alert-success mt-3">
                <i class="bi bi-check-circle"></i>
                <strong>Sync Completed!</strong> ${data.message}
            </div>
        `;

        if (data.result && data.result.branchResults && data.result.branchResults.length > 0) {
            resultHtml += `
                <div class="card">
                    <div class="card-header">
                        <h6 class="mb-0">Branch Results</h6>
                    </div>
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-sm">
                                <thead>
                                    <tr>
                                        <th>Branch</th>
                                        <th>Status</th>
                                        <th>Users</th>
                                        <th>Error</th>
                                    </tr>
                                </thead>
                                <tbody>
            `;

            data.result.branchResults.forEach(branch => {
                const statusBadge = branch.isSuccess
                    ? '<span class="badge bg-success">Success</span>'
                    : '<span class="badge bg-danger">Failed</span>';

                const userCount = branch.isSuccess ? branch.userCount : 0;
                const errorMessage = branch.errorMessage || '';

                resultHtml += `
                    <tr>
                        <td>${branch.branchName}</td>
                        <td>${statusBadge}</td>
                        <td>${userCount}</td>
                        <td><small class="text-danger">${errorMessage}</small></td>
                    </tr>
                `;
            });

            resultHtml += `
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            `;
        }

        resultsDiv.innerHTML = resultHtml;
    } else {
        resultsDiv.innerHTML = `
            <div class="alert alert-danger">
                <i class="bi bi-exclamation-triangle"></i>
                <strong>Sync Failed:</strong> ${data.message}
            </div>
        `;
    }
}

// Integration management functions
function saveIntegration() {
    const accessToken = document.getElementById('integrationAccessToken').value.trim();
    const refreshToken = document.getElementById('integrationRefreshToken').value.trim();
    const accessTokenExpiration = document.getElementById('integrationAccessTokenExpiration').value;
    const refreshTokenExpiration = document.getElementById('integrationRefreshTokenExpiration').value;

    // Validate tokens
    if (!accessToken || !refreshToken || !accessTokenExpiration || !refreshTokenExpiration) {
        showAlert('Please fill in all integration fields.', 'warning');
        return;
    }

    // Validate timestamp inputs
    const accessTimestamp = parseInt(accessTokenExpiration);
    const refreshTimestamp = parseInt(refreshTokenExpiration);

    if (isNaN(accessTimestamp) || isNaN(refreshTimestamp)) {
        showAlert('Please enter valid timestamps for expiration dates.', 'warning');
        return;
    }

    // Convert timestamps to UTC DateTime (milliseconds to DateTime)
    const accessExpiration = new Date(accessTimestamp).toISOString();
    const refreshExpiration = new Date(refreshTimestamp).toISOString();

    // Save integration settings
    fetch('/Admin/SavePlaytomicIntegration', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            accessToken: accessToken,
            refreshToken: refreshToken,
            accessTokenExpiration: accessExpiration,
            refreshTokenExpiration: refreshExpiration
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showAlert('Integration settings saved successfully! You can now sync users.', 'success');
                // Move to step 2
                showSyncStep(data.integration);
            } else {
                showAlert('Failed to save integration settings. ' + (data.message || ''), 'danger');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error saving integration settings.', 'danger');
        });
}

function startPlaytomicSyncWithIntegration() {
    const syncBtn = document.getElementById('startSyncBtn');
    const originalText = syncBtn.innerHTML;
    
    // Show loading state
    syncBtn.disabled = true;
    syncBtn.innerHTML = '<i class="bi bi-spinner"></i> Syncing...';
    
    // Clear previous results
    const resultsDiv = document.getElementById('syncResults');
    if (resultsDiv) {
        resultsDiv.innerHTML = '';
        resultsDiv.classList.remove('d-none');
    }
    
    // Show sync progress
    document.getElementById('syncProgress').classList.remove('d-none');
    
    fetch('/Admin/SyncUsersWithIntegration', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    })
        .then(response => response.json())
        .then(data => {
            // Hide progress
            document.getElementById('syncProgress').classList.add('d-none');
            
            if (data.success) {
                showAlert(data.message, 'success');
                displaySyncResults(data);
            } else {
                if (data.requiresSetup) {
                    showAlert('Integration not configured. Please set up the integration first.', 'warning');
                    // Redirect back to setup
                    document.getElementById('syncButtons').classList.add('d-none');
                    document.getElementById('integrationForm').classList.remove('d-none');
                    document.getElementById('integrationButtons').classList.remove('d-none');
                    document.getElementById('existingIntegration').classList.add('d-none');
                } else {
                    showAlert('Sync failed: ' + (data.message || 'Unknown error'), 'danger');
                    if (data) {
                        displaySyncResults(data);
                    }
                }
            }
        })
        .catch(error => {
            console.error('Error during sync:', error);
            showAlert('An error occurred during sync. Please try again.', 'danger');
            document.getElementById('syncProgress').classList.add('d-none');
        })
        .finally(() => {
            // Reset button state
            syncBtn.disabled = false;
            syncBtn.innerHTML = originalText;
        });
}

function editIntegration() {
    // Get current integration data and populate form
    fetch('/Admin/GetPlaytomicIntegration')
        .then(response => response.json())
        .then(data => {
            if (data.success && data.integration) {
                // Populate form with current values
                document.getElementById('integrationAccessToken').value = data.integration.accessToken;
                document.getElementById('integrationRefreshToken').value = data.integration.refreshToken;
                
                // Convert DateTime to timestamps (milliseconds)
                const accessExpiration = new Date(data.integration.accessTokenExpiration);
                const refreshExpiration = new Date(data.integration.refreshTokenExpiration);
                
                document.getElementById('integrationAccessTokenExpiration').value = accessExpiration.getTime();
                document.getElementById('integrationRefreshTokenExpiration').value = refreshExpiration.getTime();
            }
            
            // Show form
            document.getElementById('integrationForm').classList.remove('d-none');
            document.getElementById('existingIntegration').classList.add('d-none');
            document.getElementById('integrationButtons').classList.remove('d-none');
            document.getElementById('syncButtons').classList.add('d-none');
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error loading integration data.', 'danger');
        });
}

function showSyncStep(integration) {
    // Hide the integration form and show sync step
    document.getElementById('integrationForm').classList.add('d-none');
    document.getElementById('integrationButtons').classList.add('d-none');
    document.getElementById('existingIntegration').classList.remove('d-none');
    document.getElementById('syncButtons').classList.remove('d-none');
    document.getElementById('syncStep').classList.remove('d-none');
    
    // Update existing integration display with new data
    if (integration) {
        const accessExpiration = new Date(integration.accessTokenExpiration);
        const refreshExpiration = new Date(integration.refreshTokenExpiration);
        const now = new Date();
        
        // Update display elements if they exist
        const displayAccessExpiration = document.getElementById('displayAccessTokenExpiration');
        const displayRefreshExpiration = document.getElementById('displayRefreshTokenExpiration');
        const accessTokenStatus = document.getElementById('accessTokenStatus');
        const refreshTokenStatus = document.getElementById('refreshTokenStatus');
        
        if (displayAccessExpiration) {
            displayAccessExpiration.textContent = accessExpiration.toLocaleString();
        }
        if (displayRefreshExpiration) {
            displayRefreshExpiration.textContent = refreshExpiration.toLocaleString();
        }
        
        // Update token status badges
        if (accessTokenStatus) {
            if (accessExpiration > now) {
                accessTokenStatus.className = 'badge bg-success';
                accessTokenStatus.textContent = 'Valid';
            } else {
                accessTokenStatus.className = 'badge bg-warning';
                accessTokenStatus.textContent = 'Expired';
            }
        }
        
        if (refreshTokenStatus) {
            if (refreshExpiration > now) {
                refreshTokenStatus.className = 'badge bg-success';
                refreshTokenStatus.textContent = 'Valid';
            } else {
                refreshTokenStatus.className = 'badge bg-danger';
                refreshTokenStatus.textContent = 'Expired';
            }
        }
    }
    
    // Automatically load sync preview data
    // getSyncPreview();
}

function getSyncPreview() {
    const syncPreviewDiv = document.getElementById('syncPreview');
    const startSyncBtn = document.getElementById('startSyncBtn');
    
    if (syncPreviewDiv) {
        syncPreviewDiv.classList.remove('d-none');
        syncPreviewDiv.innerHTML = '<div class="text-center"><i class="bi bi-spinner spin"></i> Loading preview...</div>';
    }
    
    fetch('/Admin/GetSyncPreview')
        .then(response => response.json())
        .then(data => {
            if (data.success && syncPreviewDiv) {
                syncPreviewDiv.innerHTML = `
                    <div class="card-body">
                        <h6><i class="bi bi-info-circle"></i> Sync Preview</h6>
                        <div class="row">
                            <div class="col-md-6">
                                <p class="mb-1"><strong>Active Users:</strong> <span id="previewActiveUsers" class="badge bg-primary">${data.activeUsers}</span></p>
                            </div>
                            <div class="col-md-6">
                                <p class="mb-0"><strong>Branches with Tenant ID:</strong> <span id="previewBranches" class="badge bg-info">${data.branches}</span></p>
                            </div>
                        </div>
                        ${data.activeUsers > 0 && data.branches > 0 ? 
                            '<div class="alert alert-success mt-2 mb-0"><i class="bi bi-check-circle"></i> Ready to sync!</div>' : 
                            '<div class="alert alert-warning mt-2 mb-0"><i class="bi bi-exclamation-triangle"></i> No data available for sync.</div>'
                        }
                    </div>
                `;
                
                if(data.activeUsers > 0 && data.branches > 0) {
                    startSyncBtn.classList.remove('d-none');
                }
                
                // Update preview spans if they exist (for compatibility)
                const activeUsersSpan = document.getElementById('previewActiveUsers');
                const branchesSpan = document.getElementById('previewBranches');
                if (activeUsersSpan) activeUsersSpan.textContent = data.activeUsers;
                if (branchesSpan) branchesSpan.textContent = data.branches;
                
                
            } else if (syncPreviewDiv) {
                syncPreviewDiv.innerHTML = `
                    <div class="card-body">
                        <div class="alert alert-warning">
                            <i class="bi bi-exclamation-triangle"></i> Unable to load sync preview.
                            ${data.message ? '<br><small>' + data.message + '</small>' : ''}
                        </div>
                    </div>
                `;
            }
        })
        .catch(error => {
            console.error('Error getting sync preview:', error);
            if (syncPreviewDiv) {
                syncPreviewDiv.innerHTML = `
                    <div class="card-body">
                        <div class="alert alert-danger">
                            <i class="bi bi-exclamation-triangle"></i> Error loading sync preview.
                            <br><small>Please check your network connection and try again.</small>
                        </div>
                    </div>
                `;
            }
        });
}

// Initialize Playtomic modal when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    const syncModal = document.getElementById('syncPlaytomicModal');
    if (syncModal) {
        syncModal.addEventListener('show.bs.modal', function () {
            // Reset modal state
            document.getElementById('syncResults').classList.add('d-none');
            document.getElementById('syncProgress').classList.add('d-none');
            document.getElementById('syncPreview').classList.add('d-none');
            document.getElementById('startSyncBtn').classList.add('d-none');
            
            
            // Check if integration exists
            fetch('/Admin/GetPlaytomicIntegration')
                .then(response => response.json())
                .then(data => {
                    if (data.success && data.integration) {
                        // Integration exists, show sync step
                        document.getElementById('integrationForm').classList.add('d-none');
                        document.getElementById('integrationButtons').classList.add('d-none');
                        showSyncStep(data.integration);
                    } else {
                        // No integration, show setup form
                        document.getElementById('integrationForm').classList.remove('d-none');
                        document.getElementById('integrationButtons').classList.remove('d-none');
                        document.getElementById('existingIntegration').classList.add('d-none');
                        document.getElementById('syncButtons').classList.add('d-none');
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    // Default to showing setup form
                    document.getElementById('integrationForm').classList.remove('d-none');
                    document.getElementById('integrationButtons').classList.remove('d-none');
                    document.getElementById('existingIntegration').classList.add('d-none');
                    document.getElementById('syncButtons').classList.add('d-none');
                });
        });
    }

    // New: Auto call SyncPlaytomicUserIds when the modal is opened
    const syncIdsModal = document.getElementById('syncPlaytomicUserIdsModal');
    if (syncIdsModal) {
        syncIdsModal.addEventListener('show.bs.modal', function () {
            const progress = document.getElementById('syncPlaytomicUserIdsProgress');
            const result = document.getElementById('syncPlaytomicUserIdsResult');
            if (result) {
                result.innerHTML = '';
            }
            if (progress) {
                progress.classList.remove('d-none');
            }

            fetch('/Admin/SyncPlaytomicUserIds', { method: 'GET' })
                .then(resp => resp.json())
                .then(data => {
                    if (progress) progress.classList.add('d-none');

                    if (data && data.success) {
                        const count = data.updatedCount || 0;
                        if (result) {
                            result.innerHTML = `
                                <div class="alert alert-success">
                                    <i class="bi bi-check-circle"></i>
                                    Updated PlaytomicUserId for <strong>${count}</strong> user(s).
                                </div>
                            `;
                        }
                    } else {
                        const message = (data && data.message) ? data.message : 'Failed to sync Playtomic User IDs.';
                        if (result) {
                            result.innerHTML = `
                                <div class="alert alert-danger">
                                    <i class="bi bi-exclamation-triangle"></i>
                                    ${message}
                                </div>
                            `;
                        }
                    }
                })
                .catch(err => {
                    console.error('Error syncing Playtomic User IDs:', err);
                    if (progress) progress.classList.add('d-none');
                    if (result) {
                        result.innerHTML = `
                            <div class="alert alert-danger">
                                <i class="bi bi-exclamation-triangle"></i>
                                An error occurred while syncing Playtomic User IDs. Please try again.
                            </div>
                        `;
                    }
                });
        });

        // Optional: reset modal when hidden
        syncIdsModal.addEventListener('hidden.bs.modal', function () {
            const progress = document.getElementById('syncPlaytomicUserIdsProgress');
            const result = document.getElementById('syncPlaytomicUserIdsResult');
            if (progress) progress.classList.add('d-none');
            if (result) result.innerHTML = '';
        });
    }
});
