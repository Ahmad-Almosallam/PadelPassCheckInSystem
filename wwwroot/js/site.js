// Write your JavaScript code here

function convertTo24Hour(time12h) {
    // Example input: "02:30 PM"
    const [time, modifier] = time12h.trim().split(' '); // ["02:30", "PM"]
    let [hours, minutes] = time.split(':').map(Number);

    // Convert hours based on AM/PM
    if (modifier.toLowerCase() === 'pm' && hours < 12) {
        hours += 12;
    }
    if (modifier.toLowerCase() === 'am' && hours === 12) {
        hours = 0;
    }

    // Ensure two digits for hours
    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
}