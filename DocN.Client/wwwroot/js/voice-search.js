// Voice recognition for search functionality

// Check if speech recognition is supported
window.isVoiceRecognitionSupported = function() {
    return 'webkitSpeechRecognition' in window || 'SpeechRecognition' in window;
};

// Start voice recognition and return the transcript
window.startVoiceRecognition = function() {
    return new Promise((resolve, reject) => {
        if (!window.isVoiceRecognitionSupported()) {
            reject(new Error('Speech recognition is not supported in this browser'));
            return;
        }

        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        const recognition = new SpeechRecognition();
        
        recognition.lang = 'it-IT'; // Italian language
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;
        recognition.continuous = false;

        recognition.onresult = function(event) {
            const transcript = event.results[0][0].transcript;
            resolve(transcript);
        };

        recognition.onerror = function(event) {
            console.error('Speech recognition error:', event.error);
            reject(new Error(event.error));
        };

        recognition.onend = function() {
            // Recognition session ended
        };

        try {
            recognition.start();
        } catch (error) {
            reject(error);
        }
    });
};

// Stop ongoing voice recognition
window.stopVoiceRecognition = function() {
    // Placeholder for stopping recognition if needed
    // Most browsers auto-stop after getting a result
};
