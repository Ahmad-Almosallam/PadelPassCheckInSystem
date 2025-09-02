// --- Phone check-in wiring ---
const phoneInputEl = document.getElementById('phoneNumberInput');
const phoneErrorEl = document.getElementById('phoneInputError');

const phoneIti = window.intlTelInput(phoneInputEl, {
    initialCountry: "sa",
    nationalMode: false,                  // submit +E.164
    autoPlaceholder: "aggressive",
    geoIpLookup: cb => {
        fetch("https://ipapi.co/json")
            .then(r => r.json())
            .then(d => cb(d.country_code))
            .catch(() => cb(defaultFallback.toLowerCase()));
    },
    loadUtils: () => import("https://cdn.jsdelivr.net/npm/intl-tel-input@25.8.3/build/js/utils.js"),
});

const phoneErrorMap = {
    0: "Invalid number",
    1: "Invalid country code",
    2: "Too short",
    3: "Too long",
    4: "Invalid number"
};

function validatePhone() {
    const v = phoneInputEl.value.trim();
    if (!v) {
        phoneInputEl.setCustomValidity("Required");
        phoneErrorEl.textContent = "Required";
        phoneErrorEl.classList.remove("d-none");
        return false;
    }
    // wait for utils to be ready on first load
    if (phoneIti.promise && phoneIti.promise.pending) return true;

    if (phoneIti.isValidNumber()) {
        phoneInputEl.setCustomValidity("");
        phoneErrorEl.classList.add("d-none");
        return true;
    } else {
        const err = phoneIti.getValidationError();
        const msg = phoneErrorMap[err] || "Invalid phone";
        phoneInputEl.setCustomValidity(msg);
        phoneErrorEl.textContent = msg;
        phoneErrorEl.classList.remove("d-none");
        return false;
    }
}

// mark utils pending state
if (phoneIti.promise) { phoneIti.promise.pending = true; phoneIti.promise.then(() => phoneIti.promise.pending = false); }

phoneInputEl.addEventListener("keyup", validatePhone);
phoneInputEl.addEventListener("input", () => { phoneInputEl.setCustomValidity(""); phoneErrorEl.classList.add("d-none"); });
phoneInputEl.addEventListener("countrychange", validatePhone);


function showCheckInConfirmation(data) {
    // Set user info
    const userImageDiv = document.getElementById('confirm-UserImage');
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
    document.getElementById('confirm-UserName').textContent = data.userName;
    document.getElementById('confirm-SubEndDate').textContent = data.subEndDate;
    document.getElementById('confirm-Identifier').value = data.identifier;

    // Set default values for court assignment
    if (data.defaultPlayStartTime) {
        // document.getElementById('confirm-PlayStartTime').value = data.defaultPlayStartTime;
        // Set today's date but specific time
        const [hours, minutes] = data.defaultPlayStartTime.split(':');
        const playStartTimePicker = flatpickr("#confirm-PlayStartTime", {
            enableTime: true,
            noCalendar: true,
            dateFormat: "h:i K",
            allowInput: true,
            minuteIncrement: 30
        });
        playStartTimePicker.setDate(new Date().setHours(hours, minutes));

    }
    if (data.defaultPlayDurationMinutes) {
        document.getElementById('confirm-PlayDuration').value = data.defaultPlayDurationMinutes;
    }


    // write a fetch request to GetCourtsByBranch
    fetch(`Admin/GetCourtsByBranch`)
        .then(response => response.json())
        .then(data => {
            if (!data.success) {
                alert(data.message);
                return;
            }
            const courts = data.courts;
            const courtSelect = document.getElementById('confirm-CourtName');
            // remove childs from courtSelect
            courtSelect.innerHTML = '';
            courts.forEach(court => {
                const option = document.createElement('option');
                option.value = court.value;
                option.textContent = court.text;
                courtSelect.appendChild(option);
            });
        })
        .catch(error => {
            console.error('Error fetching courts:', error);
            alert('Failed to load courts. Please try again later.');
        });


    // Show the modal
    const modal = new bootstrap.Modal(document.getElementById('confirm-CheckInModal'));
    modal.show();
}

function confirmCheckInWithCourt() {
    const form = document.getElementById('confirm-CheckInForm');
    if (!form.checkValidity()) {
        form.reportValidity();
        return;
    }

    const identifier = document.getElementById('confirm-Identifier').value;
    const courtName = document.getElementById('confirm-CourtName').value.trim();
    const playDuration = parseInt(document.getElementById('confirm-PlayDuration').value);
    const playStartTime = document.getElementById('confirm-PlayStartTime').value;
    const notes = document.getElementById('confirm-Notes').value.trim();
    const playerAttended = document.getElementById('confirm-PlayerAttended').checked;
    const checkInDate = document.getElementById('confirm-CheckInDate').value.trim();

    var playStartTime24 = convertTo24Hour(playStartTime);
    
    let checkInDateTime = new Date(`${checkInDate}T${playStartTime24}:00`);


    let playStartDateTime = null;
    if (playStartTime24) {
        const today = new Date();
        const [hours, minutes] = playStartTime24.split(':');
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
            branchCourtId: courtName,
            playDurationMinutes: playDuration,
            playStartTime: playStartDateTime,
            notes: notes,
            playerAttended: playerAttended,
            checkInDate: checkInDateTime
        })
    })
        .then(response => response.json())
        .then(data => {
            const modal = bootstrap.Modal.getInstance(document.getElementById('confirm-CheckInModal'));
            modal.hide();

            // Reset all form inputs
            document.getElementById('confirm-CheckInForm').reset();
            document.getElementById('confirm-Identifier').value = '';
            document.getElementById('confirm-UserName').textContent = '';
            document.getElementById('confirm-SubEndDate').textContent = '';
            document.getElementById('confirm-UserImage').innerHTML = '';
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
