// User Modals and QR Code JavaScript
// Modal functionality and QR code generation

// QR Code generation functions
function generateQRCode(userId) {
    fetch(`/Admin/GenerateQRCode?endUserId=${userId}`)
        .then(response => response.json())
        .then(async data => {
            if (data.success) {
                // Show the QR link modal with multiple options
                showQRLinkModal(data.downloadUrl);
            } else if (data.message === "QR code has already been downloaded.") {
                if (confirm("This QR code has already been downloaded. Would you like to generate a new one?")) {
                    regenerateQRCode(userId);
                }
            } else {
                // Show error message
                showErrorAlert(data.message || 'Failed to generate QR code');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showErrorAlert('Error generating QR code');
        });
}

function regenerateQRCode(userId) {
    fetch(`/Admin/GenerateQRCode?endUserId=${userId}&forceRegenerate=true`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showQRLinkModal(data.downloadUrl);
            } else {
                showErrorAlert(data.message || 'Failed to regenerate QR code');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showErrorAlert('Error regenerating QR code');
        });
}

function showQRLinkModal(downloadUrl) {
    document.getElementById('qrDownloadLink').value = downloadUrl;

    // Show share button if Web Share API is supported
    if (navigator.share) {
        document.getElementById('shareButton').style.display = 'block';
    }

    new bootstrap.Modal(document.getElementById('qrLinkModal')).show();
    showSuccessAlert('QR code download link generated successfully!');
}

// QR Code link handling functions
function copyLinkFallback() {
    const linkInput = document.getElementById('qrDownloadLink');
    const copyIcon = document.getElementById('copyIcon');

    // Try multiple methods for copying
    let copySuccess = false;

    // Method 1: Modern clipboard API (works on HTTPS)
    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(linkInput.value).then(() => {
            copySuccess = true;
            updateCopyButton(copyIcon, true);
        }).catch(() => {
            // Fallback to method 2
            copySuccess = fallbackCopyMethod(linkInput);
            updateCopyButton(copyIcon, copySuccess);
        });
    } else {
        // Method 2: Fallback for older browsers and non-HTTPS
        copySuccess = fallbackCopyMethod(linkInput);
        updateCopyButton(copyIcon, copySuccess);
    }
}

function fallbackCopyMethod(input) {
    try {
        // Select the text
        input.select();
        input.setSelectionRange(0, 99999); // For mobile devices

        // Copy the text
        const successful = document.execCommand('copy');

        // Deselect
        if (window.getSelection) {
            window.getSelection().removeAllRanges();
        }

        return successful;
    } catch (err) {
        console.error('Fallback copy failed:', err);
        return false;
    }
}

function updateCopyButton(icon, success) {
    if (success) {
        icon.className = 'bi bi-check-circle text-success';
        setTimeout(() => {
            icon.className = 'bi bi-clipboard';
        }, 2000);
        showSuccessAlert('Link copied to clipboard!');
    } else {
        icon.className = 'bi bi-x-circle text-danger';
        setTimeout(() => {
            icon.className = 'bi bi-clipboard';
        }, 2000);
        showErrorAlert('Failed to copy link. Please manually select and copy the text.');

        // Automatically select the text for manual copying
        const linkInput = document.getElementById('qrDownloadLink');
        linkInput.focus();
        linkInput.select();
    }
}

function openQRLink() {
    const link = document.getElementById('qrDownloadLink').value;
    window.open(link, '_blank');
}

function shareQRLink() {
    const link = document.getElementById('qrDownloadLink').value;

    if (navigator.share) {
        navigator.share({
            title: 'QR Code Download',
            text: 'Download your QR code for check-in',
            url: link
        }).then(() => {
            console.log('Successfully shared');
        }).catch((error) => {
            console.log('Error sharing:', error);
            // Fallback to copy
            copyLinkFallback();
        });
    } else {
        // Fallback to copy
        copyLinkFallback();
    }
}

function downloadQR() {
    const link = document.createElement('a');
    const qrImage = document.getElementById('qrCodeImage').src;
    const memberId = document.getElementById('uniqueId').textContent;

    link.href = qrImage;
    link.download = `QRCode_${memberId}.png`;
    link.click();
}
