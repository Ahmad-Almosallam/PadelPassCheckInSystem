function showCheckInConfirmation(data) {
    // Set user info
    const userImageDiv = document.getElementById('confirmUserImage');
    if (data.userImage) {
        userImageDiv.innerHTML = `
            <img src="${data.userImage}" alt="${data.userName}" 
                 class="rounded-circle" style="width: 100px; height: 100px; object-fit: cover;">`;
    } else {
        userImageDiv.innerHTML = `
            <div class="bg-secondary rounded-circle d-flex align-items-center justify-content-center" 
                 style="width: 100px; height: 100px;">
                <i class="bi bi-person-fill text-white" style="font-size: 3rem;"></i>
            </div>`;
    }

    // Set user details
    document.getElementById('confirmUserName').textContent = data.userName;
    document.getElementById('confirmSubEndDate').textContent = data.subEndDate;
    document.getElementById('confirmIdentifier').value = data.identifier;

    // Set default values for court assignment
    if (data.defaultPlayStartTime) {
        document.getElementById('confirmPlayStartTime').value = data.defaultPlayStartTime;
    }
    if (data.defaultPlayDurationMinutes) {
        document.getElementById('confirmPlayDuration').value = data.defaultPlayDurationMinutes;
    }

    // Show the modal
    const modal = new bootstrap.Modal(document.getElementById('checkInConfirmModal'));
    modal.show();
}

function confirmCheckInWithCourt() {
    const form = document.getElementById('checkInConfirmForm');
    if (!form.checkValidity()) {
        form.reportValidity();
        return;
    }

    const identifier = document.getElementById('confirmIdentifier').value;
    const courtName = document.getElementById('confirmCourtName').value.trim();
    const playDuration = parseInt(document.getElementById('confirmPlayDuration').value);
    const playStartTime = document.getElementById('confirmPlayStartTime').value;
    const notes = document.getElementById('confirmNotes').value.trim();

    let playStartDateTime = null;
    if (playStartTime) {
        const today = new Date();
        const [hours, minutes] = playStartTime.split(':');
        playStartDateTime = new Date(today.getFullYear(), today.getMonth(), today.getDate(), parseInt(hours), parseInt(minutes));
    }

    fetch('/CheckIn/CheckInWithCourtAssignment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({
            identifier: identifier,
            courtName: courtName,
            playDurationMinutes: playDuration,
            playStartTime: playStartDateTime,
            notes: notes
        })
    })
    .then(response => response.json())
    .then(data => {
        const modal = bootstrap.Modal.getInstance(document.getElementById('checkInConfirmModal'));
        modal.hide();

        // Reset all form inputs
        document.getElementById('checkInConfirmForm').reset();
        document.getElementById('confirmIdentifier').value = '';
        document.getElementById('confirmUserName').textContent = '';
        document.getElementById('confirmSubEndDate').textContent = '';
        document.getElementById('confirmUserImage').innerHTML = '';
        document.getElementById('phoneNumberInput').value = ''; // Reset phone number input

        if (data.success) {
            if (data.courtAssignmentSuccess) {
                showSuccessAnimation({
                    userName: data.userName,
                    userImage: data.userImage,
                    subEndDate: data.subEndDate,
                    checkInTimeKSA: data.checkInTimeKSA,
                    courtName: data.courtName,
                    playDurationMinutes: data.playDurationMinutes,
                    playStartTime: data.playStartTime
                });
            } else {
                showMessage('Check-in successful but court assignment failed: ' + data.message, 'warning');
            }
            updateRecentCheckIns();
            updateTodayCount();
        } else {
            showMessage(data.message, 'danger');
            playErrorSound();
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showMessage('An error occurred while processing check-in and court assignment.', 'danger');
    });
}
