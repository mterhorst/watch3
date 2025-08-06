self.addEventListener('push', async (event) => {
    const data = event.data.json();

    if (data.type === "startStream") {
        await fetch('/api/stream/start', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
        });
    }
});

self.addEventListener('message', event => {

});