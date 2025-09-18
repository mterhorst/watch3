import { LitElement, html, css } from './lit-core.min.js';
import Helpers from "./helpers.js";
class VideoViewer extends LitElement {
    static styles = css`
                    :host {
                      display: block;
                      text-align: center;
                    }
                    video {
                      width: 640px;
                      height: auto;
                      background: black;
                    }
                    button {
                      margin-top: 8px;
                    }
                  `;

    static properties = {
        status: { type: String },
        showRestart: { type: Boolean },
        showStop: { type: Boolean }
    };

    constructor() {
        super();
        this.status = "Initializing...";
        this.showRestart = false;
        this.showStop = false;
        this.pc = null;
    }

    render() {
        return html`
            <video id="video" autoplay playsinline></video>
            <br />
            ${this.showRestart
                ? html`<button @click=${this.restartStream}>Restart Stream</button>`
                : null}
            ${this.showStop
                ? html`<button @click=${this.stopStream}>Stop Stream</button>`
                : null}
            <p>${this.status}</p>
        `;
    }

    firstUpdated() {
        this.init();
    }

    async restartStream() {
        this.showRestart = false;
        this.status = "Restarting stream...";
        await this.stopStream();
        await this.init();
    }

    async stopStream() {
        this.showStop = false;
        this.status = "";
        await Helpers.sendPushRequest("StopStream", {});
        const video = this.shadowRoot.getElementById('video');
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(track => track.stop());
            video.srcObject = null;
        }
        this.showRestart = true;
    }



    async init() {
        if (this.pc) {
            try {
                this.pc.close();
            } catch (err) {
                console.warn("Error closing old PeerConnection:", err);
            }
            this.pc = null;
        }

        let response = await Helpers.sendPushRequest("GetOffer", {});
        if (response.code) {
            this.status = response.error;
            if (response.code === 1)
                this.showRestart = true;
            return;
        }

        const configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
            ],
            bundlePolicy: 'balanced'
        };

        this.pc = new RTCPeerConnection(configuration);
        this.pc.addTransceiver("video", { direction: "recvonly" });
        this.pc.addTransceiver("audio", { direction: "recvonly" });

        const video = this.shadowRoot.getElementById('video');
        this.pc.ontrack = e => {
            if (e.track.kind === "video") {
                video.srcObject = e.streams[0];
            }
        };

        await this.pc.setRemoteDescription({ type: "offer", sdp: response.sdp });
        const answer = await this.pc.createAnswer();
        await this.pc.setLocalDescription(answer);
        await this.waitForIceGatheringComplete(this.pc);

        response = await Helpers.sendPushRequest("GetAnswer", {
            sdp: this.pc.localDescription.sdp,
            type: "answer"
        });
        if (response.code) {
            this.status = response.error;
            return;
        }

        this.showRestart = false;
        this.showStop = true;
        this.status = "Connected ✔️";
    }

    waitForIceGatheringComplete(pc) {
        return new Promise(resolve => {
            if (pc.iceGatheringState === 'complete') {
                resolve();
            } else {
                const checkState = () => {
                    if (pc.iceGatheringState === 'complete') {
                        pc.removeEventListener('icegatheringstatechange', checkState);
                        resolve();
                    }
                };
                pc.addEventListener('icegatheringstatechange', checkState);
            }
        });
    }
}

customElements.define('video-viewer', VideoViewer);