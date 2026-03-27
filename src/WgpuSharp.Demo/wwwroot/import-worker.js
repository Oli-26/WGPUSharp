// Web Worker for reading mesh files off the main thread.
// Receives a File, reads it to ArrayBuffer, converts to base64 efficiently.
self.onmessage = async (e) => {
    const file = e.data;
    self.postMessage({ type: 'progress', phase: 'Reading file...', pct: 10 });

    const buf = await file.arrayBuffer();
    self.postMessage({ type: 'progress', phase: 'Encoding...', pct: 50 });

    // Efficient base64 encoding using chunks (avoids O(n²) string concat)
    const bytes = new Uint8Array(buf);
    const chunkSize = 32768;
    const chunks = [];
    for (let i = 0; i < bytes.length; i += chunkSize) {
        const slice = bytes.subarray(i, Math.min(i + chunkSize, bytes.length));
        chunks.push(String.fromCharCode.apply(null, slice));
    }
    const base64 = btoa(chunks.join(''));

    self.postMessage({ type: 'progress', phase: 'Transferring...', pct: 80 });
    self.postMessage({ type: 'done', fileName: file.name, base64, size: bytes.length });
};
