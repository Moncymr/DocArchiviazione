// PDF Preview Helper
// Uses PDF.js to render PDF documents in the browser

window.pdfPreview = {
    pdfjsLib: null,
    currentDoc: null,
    currentScale: 1.5,

    // Initialize PDF.js library
    async init() {
        if (!this.pdfjsLib && typeof pdfjsLib !== 'undefined') {
            this.pdfjsLib = pdfjsLib;
            // Set worker path
            this.pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
            console.log('PDF.js initialized');
        }
    },

    // Load and render PDF document
    async loadPdf(containerId, documentId, maxPages = 3) {
        try {
            await this.init();
            
            if (!this.pdfjsLib) {
                console.error('PDF.js library not loaded');
                return { success: false, error: 'PDF.js library not available' };
            }

            const container = document.getElementById(containerId);
            if (!container) {
                console.error(`Container ${containerId} not found`);
                return { success: false, error: 'Container not found' };
            }

            // Clear previous content
            container.innerHTML = '<div class="pdf-loading">Loading PDF...</div>';

            // Fetch the PDF document
            const url = `/api/documents/${documentId}/download`;
            
            // Load the PDF document
            const loadingTask = this.pdfjsLib.getDocument(url);
            const pdf = await loadingTask.promise;
            this.currentDoc = pdf;

            console.log(`PDF loaded: ${pdf.numPages} pages`);

            // Clear loading message
            container.innerHTML = '';

            // Render only first few pages
            const pagesToRender = Math.min(pdf.numPages, maxPages);
            
            for (let pageNum = 1; pageNum <= pagesToRender; pageNum++) {
                await this.renderPage(pdf, pageNum, container);
            }

            // Show message if there are more pages
            if (pdf.numPages > maxPages) {
                const morePages = document.createElement('div');
                morePages.className = 'pdf-more-pages';
                morePages.innerHTML = `
                    <p>
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                            <path d="M6 10.5a.5.5 0 0 1 .5-.5h3a.5.5 0 0 1 0 1h-3a.5.5 0 0 1-.5-.5zm-2-3a.5.5 0 0 1 .5-.5h7a.5.5 0 0 1 0 1h-7a.5.5 0 0 1-.5-.5zm-2-3a.5.5 0 0 1 .5-.5h11a.5.5 0 0 1 0 1h-11a.5.5 0 0 1-.5-.5z"/>
                        </svg>
                        Showing ${pagesToRender} of ${pdf.numPages} pages. Open full document to view all pages.
                    </p>
                `;
                container.appendChild(morePages);
            }

            return { success: true, pages: pdf.numPages };

        } catch (error) {
            console.error('Error loading PDF:', error);
            const container = document.getElementById(containerId);
            if (container) {
                container.innerHTML = `
                    <div class="pdf-error">
                        <p>Error loading PDF: ${error.message}</p>
                        <p class="hint">Try opening the full document instead.</p>
                    </div>
                `;
            }
            return { success: false, error: error.message };
        }
    },

    // Render a single page
    async renderPage(pdf, pageNum, container) {
        try {
            const page = await pdf.getPage(pageNum);
            
            // Create canvas for this page
            const canvas = document.createElement('canvas');
            const context = canvas.getContext('2d');
            
            // Calculate viewport
            const viewport = page.getViewport({ scale: this.currentScale });
            canvas.height = viewport.height;
            canvas.width = viewport.width;
            
            // Add page number label
            const pageContainer = document.createElement('div');
            pageContainer.className = 'pdf-page-container';
            
            const pageLabel = document.createElement('div');
            pageLabel.className = 'pdf-page-label';
            pageLabel.textContent = `Page ${pageNum}`;
            
            pageContainer.appendChild(pageLabel);
            pageContainer.appendChild(canvas);
            container.appendChild(pageContainer);

            // Render the page
            const renderContext = {
                canvasContext: context,
                viewport: viewport
            };
            
            await page.render(renderContext).promise;
            
            console.log(`Page ${pageNum} rendered`);

        } catch (error) {
            console.error(`Error rendering page ${pageNum}:`, error);
            throw error;
        }
    },

    // Cleanup
    cleanup() {
        if (this.currentDoc) {
            this.currentDoc.destroy();
            this.currentDoc = null;
        }
    }
};
