let subscriptionId;

self.addEventListener('push', async (event) => {
    const data = event.data.json();

    if (self.subscriptionId === data.subscription.info.id) {
        const allClients = await clients.matchAll({
            type: "window",
            includeUncontrolled: true
        });

        for (const client of allClients) {
            client.postMessage(data);
        }
    } else {
        await fetch('/api/accept_push', {
            method: 'POST',
            body: event.data.text(),
            headers: {
                'Content-Type': 'application/json'
            },
        });
    }
});

self.addEventListener("fetch", event => {
    event.waitUntil((async () => {
        self.subscriptionId = (await cookieStore.get("SubscriptionId"))?.value;
    })());
});