const statusEl = document.getElementById('status');
const fileLabelEl = document.getElementById('fileLabel');
const textareaEl = document.getElementById('xmlContent');

function connect() {
    const ws = new WebSocket(`ws://${location.host}/`);

    ws.onopen = () => {
        statusEl.textContent = 'Connected';
        statusEl.className = 'status connected';
    };

    ws.onmessage = (event) => {
        const data = event.data;
        const newlineIndex = data.indexOf('\n');
        if (newlineIndex !== -1) {
            const fileName = data.substring(0, newlineIndex);
            const content = data.substring(newlineIndex + 1);
            fileLabelEl.textContent = `File changed: ${fileName}`;
            textareaEl.value = content;
        } else {
            textareaEl.value = data;
        }
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
