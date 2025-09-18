export default class Helpers {
    static async sendPushRequest(type, data) {
        const subscription = (await (await navigator.serviceWorker.getRegistration()).pushManager.getSubscription()).toJSON();

        var id = this.createGuid();

        await fetch('/api/push', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                id,
                subscription,
                type,
                data
            })
        });

        return await this.waitForServiceWorkerData(id);
    }

    static waitForServiceWorkerData(id) {
        return new Promise((resolve) => {
            navigator.serviceWorker.addEventListener("message", (event) => {
                if (event.data?.id === id) {
                    resolve(event.data.data);
                }
            });
        });
    }

    static createGuid() {
        const buf = new Uint8Array(16);
        crypto.getRandomValues(buf);
        buf[6] = (buf[6] & 0x0f) | 0x40;
        buf[8] = (buf[8] & 0x3f) | 0x80;
        const hex = [...buf].map(b => b.toString(16).padStart(2, "0"));
        return (
            hex.slice(0, 4).join("") + "-" +
            hex.slice(4, 6).join("") + "-" +
            hex.slice(6, 8).join("") + "-" +
            hex.slice(8, 10).join("") + "-" +
            hex.slice(10, 16).join("")
        );
    }
}
