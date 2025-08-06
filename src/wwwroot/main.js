// main.js for TreeRTC home page

document.addEventListener('DOMContentLoaded', function () {
    const btn = document.getElementById('helloBtn');
    const output = document.getElementById('output');
    if (btn && output) {
        btn.addEventListener('click', async function () {
            output.textContent = 'Hello from TreeRTC!';


            const localVideo = document.getElementById('localVideo');   // Only needed on sender
            const remoteVideo = document.getElementById('remoteVideo');

            const peerConnection = new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            });

            const socket = new WebSocket('ws://your-signaling-server');

            socket.onmessage = async ({ data }) => {
                const message = JSON.parse(data);

                if (message.offer && !isInitiator) {
                    await peerConnection.setRemoteDescription(new RTCSessionDescription(message.offer));

                    // Only Home2 captures video and adds it
                    try {
                        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                        localVideo.srcObject = stream;
                        stream.getTracks().forEach(track => peerConnection.addTrack(track, stream));
                    } catch (err) {
                        console.error('Media error:', err);
                        return;
                    }

                    const answer = await peerConnection.createAnswer();
                    await peerConnection.setLocalDescription(answer);
                    socket.send(JSON.stringify({ answer }));
                }

                if (message.answer && isInitiator) {
                    await peerConnection.setRemoteDescription(new RTCSessionDescription(message.answer));
                }

                if (message.ice) {
                    try {
                        await peerConnection.addIceCandidate(message.ice);
                    } catch (e) {
                        console.error('ICE error:', e);
                    }
                }
            };

            peerConnection.onicecandidate = ({ candidate }) => {
                if (candidate) {
                    socket.send(JSON.stringify({ ice: candidate }));
                }
            };

            peerConnection.ontrack = (event) => {
                remoteVideo.srcObject = event.streams[0];
            };

            if (isInitiator) {
                // Initiator does not send media
                const offer = await peerConnection.createOffer();
                await peerConnection.setLocalDescription(offer);
                socket.addEventListener('open', () => {
                    socket.send(JSON.stringify({ offer }));
                });
            }

        });
    }
});

function initReceiver(remoteVideoElementId, signalingCallback) {
    const config = {
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    };

    const pc = new RTCPeerConnection(config);
    const remoteVideo = document.getElementById(remoteVideoElementId);

    // Handle incoming media stream
    pc.ontrack = (event) => {
        if (remoteVideo.srcObject !== event.streams[0]) {
            remoteVideo.srcObject = event.streams[0];
            console.log("Received remote stream");
        }
    };

    // Handle ICE candidates and send to remote peer
    pc.onicecandidate = (event) => {
        if (event.candidate) {
            signalingCallback({ type: 'candidate', candidate: event.candidate });
        }
    };

    // Handle incoming offer and generate answer
    async function handleSignalingMessage(message) {
        if (message.type === 'offer') {
            await pc.setRemoteDescription(new RTCSessionDescription(message.sdp));
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            signalingCallback({ type: 'answer', sdp: pc.localDescription });
        } else if (message.type === 'candidate') {
            await pc.addIceCandidate(new RTCIceCandidate(message.candidate));
        }
    }

    return { handleSignalingMessage, peerConnection: pc };
}
