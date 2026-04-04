const statusEl = document.getElementById('status');
const fileLabelEl = document.getElementById('fileLabel');
const pdfViewer = document.getElementById('pdfViewer');
let currentBlobUrl = null;
function connect() {
    const ws = new WebSocket(`ws://${location.host}/`);
    ws.binaryType = 'arraybuffer';
    ws.onopen = () => {
        statusEl.textContent = 'Connected';
        statusEl.className = 'status connected';
    };
    ws.onmessage = (event) => {
        if (currentBlobUrl) {
            URL.revokeObjectURL(currentBlobUrl);
        }
        const blob = new Blob([event.data], { type: 'application/pdf' });
        currentBlobUrl = URL.createObjectURL(blob);
        pdfViewer.src = currentBlobUrl;
        fileLabelEl.textContent = 'Template updated';
    };
    ws.onclose = () => {
        statusEl.textContent = 'Disconnected';
        statusEl.className = 'status disconnected';
        setTimeout(connect, 2000);
    };
    ws.onerror = () => {
        ws.close();
    };
}
connect();
