import { LitElement, html, css } from './lit-core.min.js';

class RegisterSubscription extends LitElement {
    static styles = css``;

    static properties = {
        status: { type: String },
    };

    constructor() {
        super();
        this.status = "Initializing...";
    }

    render() {
        return html`
            <p>${this.status}</p>
        `;
    }

    async firstUpdated() {
        this.init();
    }

    async init() {
        await Notification.requestPermission();

        let publicVapidKey;
        try {
            const response = await fetch('/api/vapid_config', { method: "GET" });
            const data = await response.json();
            publicVapidKey = data.publicKey;
            
        } catch (err) {
            this.status = `Network error while loading stream: ${err.message}`;
            return;
        }

        if (!publicVapidKey) {
            this.status = "No publicVapidKey found!";
            return;
        }

        let w = await fetch("service-worker.js", { redirect: "error" })
        console.log(w)
        console.log(w.headers)

        const registration = await navigator.serviceWorker.register('/service-worker.js', {
            
        });
        const subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: publicVapidKey
        });

        const response = (await fetch('api/register_subscription', {
            method: 'POST',
            credentials: "include",
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(subscription.toJSON())
        }));

        await navigator.serviceWorker.ready;
        navigator.serviceWorker.controller.postMessage(await response.json());

        const pushName = (await cookieStore.get('PushName'))?.value ?? "";

        if (response.status === 200) {
            this.status = `${pushName} subscription registered successfully!`;

            //window.location.href = 'index.html'
        } else {
            this.status = `${pushName} failed to register subscription. Server responded with status ${response.status}`;
        }
    }
}

customElements.define('register-subscription', RegisterSubscription);