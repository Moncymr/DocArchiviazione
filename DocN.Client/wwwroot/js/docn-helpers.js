// DocN - JavaScript Helper Functions

/**
 * Download file from base64 data
 * @param {string} filename - Name of the file to download
 * @param {string} base64Data - Base64 encoded file data
 */
window.downloadFile = (filename, base64Data) => {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray]);
        
        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        // Trigger download
        document.body.appendChild(link);
        link.click();
        
        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        
        console.log(`✅ File "${filename}" downloaded successfully`);
    } catch (error) {
        console.error('❌ Error downloading file:', error);
        alert('Error downloading file. Please try again.');
    }
};

/**
 * Copy text to clipboard
 * @param {string} text - Text to copy
 */
window.copyToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        console.log('✅ Text copied to clipboard');
        return true;
    } catch (error) {
        console.error('❌ Error copying to clipboard:', error);
        return false;
    }
};

/**
 * Show toast notification
 * @param {string} message - Message to display
 * @param {string} type - Type of notification (success, error, info, warning)
 * @param {number} duration - Duration in milliseconds (default: 3000)
 */
window.showToast = (message, type = 'info', duration = 3000) => {
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    
    // Style
    Object.assign(toast.style, {
        position: 'fixed',
        top: '20px',
        right: '20px',
        padding: '1rem 1.5rem',
        borderRadius: '8px',
        color: 'white',
        fontWeight: '600',
        zIndex: '10000',
        animation: 'slideInRight 0.3s ease',
        boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
    });
    
    // Colors based on type
    const colors = {
        success: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
        error: '#dc3545',
        info: '#17a2b8',
        warning: '#ffc107'
    };
    toast.style.background = colors[type] || colors.info;
    
    // Add to document
    document.body.appendChild(toast);
    
    // Remove after duration
    setTimeout(() => {
        toast.style.animation = 'slideOutRight 0.3s ease';
        setTimeout(() => {
            document.body.removeChild(toast);
        }, 300);
    }, duration);
};

// Add CSS for animations
const style = document.createElement('style');
style.textContent = `
    @keyframes slideInRight {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    
    @keyframes slideOutRight {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(100%);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);

/**
 * Initialize drag and drop for dashboard widgets
 */
window.initDragAndDrop = () => {
    const widgetsGrid = document.querySelector('.widgets-grid');
    if (!widgetsGrid) return;

    let draggedElement = null;

    const widgets = widgetsGrid.querySelectorAll('.widget-container');
    
    widgets.forEach(widget => {
        widget.setAttribute('draggable', 'true');
        
        widget.addEventListener('dragstart', (e) => {
            draggedElement = widget;
            widget.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
        });
        
        widget.addEventListener('dragend', () => {
            widget.classList.remove('dragging');
        });
        
        widget.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            
            const afterElement = getDragAfterElement(widgetsGrid, e.clientY, e.clientX);
            if (afterElement == null) {
                widgetsGrid.appendChild(draggedElement);
            } else {
                widgetsGrid.insertBefore(draggedElement, afterElement);
            }
        });
    });
};

function getDragAfterElement(container, y, x) {
    const draggableElements = [...container.querySelectorAll('.widget-container:not(.dragging)')];
    
    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offsetY = y - box.top - box.height / 2;
        const offsetX = x - box.left - box.width / 2;
        const offset = Math.sqrt(offsetX * offsetX + offsetY * offsetY);
        
        if (offset < 0 && offset > closest.offset) {
            return { offset: offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}

/**
 * Get widget positions after drag and drop
 */
window.getWidgetPositions = () => {
    const widgetsGrid = document.querySelector('.widgets-grid');
    if (!widgetsGrid) return [];
    
    const widgets = widgetsGrid.querySelectorAll('.widget-container');
    const positions = [];
    
    widgets.forEach((widget, index) => {
        const widgetId = widget.getAttribute('data-widget-id');
        if (widgetId) {
            positions.push({
                widgetId: parseInt(widgetId),
                position: index
            });
        }
    });
    
    return positions;
};

console.log('✅ DocN JavaScript helpers loaded');
