// Notification helper functions for DocN application

// Request notification permissions
window.requestNotificationPermission = async function() {
    if (!("Notification" in window)) {
        console.log("This browser does not support desktop notifications");
        return false;
    }

    if (Notification.permission === "granted") {
        return true;
    }

    if (Notification.permission !== "denied") {
        const permission = await Notification.requestPermission();
        return permission === "granted";
    }

    return false;
};

// Show browser notification
window.showNotification = async function(title, body, options = {}) {
    if (!("Notification" in window)) {
        console.log("This browser does not support desktop notifications");
        return;
    }

    if (Notification.permission === "granted") {
        const notification = new Notification(title, {
            body: body,
            icon: options.icon || '/icon-192.png',
            badge: options.badge || '/favicon.png',
            tag: options.tag || 'docn-notification',
            requireInteraction: options.requireInteraction || false,
            silent: options.silent || false
        });

        notification.onclick = function() {
            window.focus();
            notification.close();
            if (options.onClick) {
                options.onClick();
            }
        };
    } else if (Notification.permission !== "denied") {
        const permission = await Notification.requestPermission();
        if (permission === "granted") {
            window.showNotification(title, body, options);
        }
    }
};

// Play notification sound
window.playNotificationSound = function() {
    try {
        const audio = new Audio('/sounds/notification.mp3');
        audio.volume = 0.5;
        audio.play().catch(err => {
            console.log("Unable to play notification sound:", err);
        });
    } catch (err) {
        console.log("Error playing notification sound:", err);
    }
};

// Check if notifications are supported
window.areNotificationsSupported = function() {
    return "Notification" in window;
};

// Get current notification permission status
window.getNotificationPermission = function() {
    if ("Notification" in window) {
        return Notification.permission;
    }
    return "unsupported";
};
