using System.Buffers;
using System.Formats.Asn1;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Watch3.Models;

namespace Watch3.Http
{
    public sealed class VapidHttp
    {
        private const string ENCRYPTED_CONTENT_ENCODING = "aes128gcm";

        private readonly HttpClient _httpClient;
        private readonly PushServiceConfig _config;

        public VapidHttp(HttpClient httpClient, PushServiceConfig config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task RequestPushMessageDelivery(PushSubscription subscription, PushPayload payload, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            ArgumentNullException.ThrowIfNull(payload);

            await using var message = new MemoryStream();
            await JsonSerializer.SerializeAsync(message, payload, Json.JsonAppContext.PushPayload, token);
            message.Seek(0, SeekOrigin.Begin);

            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint);
            request.Headers.TryAddWithoutValidation("TTL", TimeSpan.FromMinutes(1).TotalSeconds.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("Urgency", "normal");

            var endpointUri = new Uri(subscription.Endpoint);
            var audience = endpointUri.Scheme + @"://" + endpointUri.Host;

            string vapidToken = GenerateVapidJwt(audience, DateTime.UtcNow.AddHours(12));
            request.Headers.Authorization = new AuthenticationHeaderValue("vapid", $"t={vapidToken}, k={_config.PublicKey}");

            var (agreementPublicKey, sharedSecretHmac) = ECDHAgreementCalculator.CalculateAgreement(
                FromUrlBase64String(subscription.Keys.P256dh),
                FromUrlBase64String(subscription.Keys.Auth));

            var keyingMaterial = GetKeyingMaterial(subscription, agreementPublicKey, sharedSecretHmac);

            // Encrypt the payload stream
            var newStream = new MemoryStream();
            await EncodeAsync(message,
                              newStream,
                              salt: null,
                              key: keyingMaterial,
                              keyId: agreementPublicKey,
                              token);

            newStream.Seek(0, SeekOrigin.Begin);

            request.Content = new StreamContent(newStream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
            request.Content.Headers.ContentEncoding.Add(ENCRYPTED_CONTENT_ENCODING);

            using var response = await _httpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
$"""
Received unexpected response
StatusCode: {response.StatusCode}
ReasonPhrase: {response.ReasonPhrase}
Content: {await response.Content.ReadAsStringAsync(token)}
""",
                    inner: null,
                    statusCode: response.StatusCode);
            }
        }

        // ----------------- VAPID JWT -----------------

        private string GenerateVapidJwt(string audience, DateTime absoluteExpiration)
        {
            using var jwtSigner = new ES256Signer(FromUrlBase64String(_config.PrivateKey));

            var headerSegment = GenerateJwtHeaderSegment();
            var bodySegment = GenerateJwtBodySegment(audience, absoluteExpiration);
            var jwtInput = headerSegment + "." + bodySegment;

            var signature = ToUrlBase64String(jwtSigner.GenerateSignature(jwtInput));
            return jwtInput + "." + signature;
        }

        private string GenerateJwtHeaderSegment()
        {
            return ToUrlBase64String(Encoding.UTF8.GetBytes(new JsonObject
            {
                new KeyValuePair<string, JsonNode?>("typ", "JWT"),
                new KeyValuePair<string, JsonNode?>("alg", "ES256")
            }.ToJsonString()));
        }

        private string GenerateJwtBodySegment(string audience, DateTime absoluteExpiration)
        {
            var body = new List<KeyValuePair<string, JsonNode?>>
            {
                new("aud", audience),
                new("exp", new DateTimeOffset(absoluteExpiration).ToUnixTimeSeconds())
            };

            if (!string.IsNullOrEmpty(_config.Subject))
            {
                body.Add(new("sub", _config.Subject));
            }

            return ToUrlBase64String(Encoding.UTF8.GetBytes(new JsonObject(body).ToJsonString()));
        }

        // ----------------- Encryption -----------------

        private static byte[] GetKeyingMaterial(PushSubscription subscription, byte[] agreementPublicKey, byte[] sharedSecretHmac)
        {
            var userAgentPublicKey = FromUrlBase64String(subscription.Keys.P256dh);
            var infoParameter = GetKeyingMaterialInfoParameter(userAgentPublicKey, agreementPublicKey);

            using var hasher = new HMACSHA256(sharedSecretHmac);
            return hasher.ComputeHash(infoParameter);
        }

        private static byte[] GetKeyingMaterialInfoParameter(byte[] userAgentPublicKey, byte[] applicationServerPublicKey)
        {
            const byte KEYING_MATERIAL_INFO_PARAMETER_DELIMITER = 1;

            var _keyingMaterialInfoParameterPrefix = Encoding.ASCII.GetBytes("WebPush: info");

            var infoParameter = new byte[_keyingMaterialInfoParameterPrefix.Length + userAgentPublicKey.Length + applicationServerPublicKey.Length + 2];

            Buffer.BlockCopy(_keyingMaterialInfoParameterPrefix, 0, infoParameter, 0, _keyingMaterialInfoParameterPrefix.Length);
            int infoParameterIndex = _keyingMaterialInfoParameterPrefix.Length + 1;

            Buffer.BlockCopy(userAgentPublicKey, 0, infoParameter, infoParameterIndex, userAgentPublicKey.Length);
            infoParameterIndex += userAgentPublicKey.Length;

            Buffer.BlockCopy(applicationServerPublicKey, 0, infoParameter, infoParameterIndex, applicationServerPublicKey.Length);
            infoParameter[^1] = KEYING_MATERIAL_INFO_PARAMETER_DELIMITER;

            return infoParameter;
        }

        private static byte[] FromUrlBase64String(string input)
        {
            input = input.Replace('-', '+').Replace('_', '/');
            while (input.Length % 4 != 0)
            {
                input += "=";
            }
            return Convert.FromBase64String(input);
        }

        private static string ToUrlBase64String(byte[] input) =>
            Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static async Task EncodeAsync(Stream source,
                                             Stream destination,
                                             byte[]? salt,
                                             byte[] key,
                                             byte[]? keyId,
                                             CancellationToken token)
        {
            const int saltLength = 16;
            const int tagLength = 16;
            const int delimiterSize = 1;
            const int minRecordSize = tagLength + delimiterSize + 1;
            const byte recordDelimiter = 1;
            const byte lastRecordDelimiter = 2;
            const int recordSize = 4096;

            if (recordSize < minRecordSize)
                throw new ArgumentException($"recordSize must be ≥ {minRecordSize}", nameof(recordSize));
            if (keyId is not null && keyId.Length > byte.MaxValue)
                throw new ArgumentException("keyId too long (max 255 bytes).", nameof(keyId));

            salt ??= RandomNumberGenerator.GetBytes(saltLength);
            if (salt.Length != saltLength)
                throw new ArgumentException($"Salt must be {saltLength} bytes.", nameof(salt));

            // PRK = HMAC(salt, key)
            byte[] prk;
            using (var hmac = new HMACSHA256(salt))
            {
                prk = hmac.ComputeHash(key);
            }

            // derive CEK and NONCE
            static byte[] Derive(byte[] prk, string label)
            {
                var info = Encoding.ASCII.GetBytes($"Content-Encoding: {label}\0\u0001");
                using var h = new HMACSHA256(prk);
                return h.ComputeHash(info);
            }
            var cek = Derive(prk, ENCRYPTED_CONTENT_ENCODING);
            var nonceSeed = Derive(prk, "nonce")[..12];

            // write header
            await destination.WriteAsync(salt, token);
            await destination.WriteAsync(BitConverter.GetBytes(recordSize).Reverse().ToArray(), token);
            destination.WriteByte((byte)(keyId?.Length ?? 0));
            if (keyId is { Length: > 0 })
                await destination.WriteAsync(keyId, token);

            // buffers
            var maxPlain = recordSize - tagLength - delimiterSize;
            var plainBuf = ArrayPool<byte>.Shared.Rent(maxPlain + delimiterSize);
            var cipherBuf = ArrayPool<byte>.Shared.Rent(recordSize);

            try
            {
                using var aes = new AesGcm(cek[..16], 16);
                ulong seq = 0;
                int? peeked = null;

                var nonce = new byte[12];

                while (true)
                {
                    int read;
                    if (peeked.HasValue)
                    {
                        plainBuf[0] = (byte)peeked.Value;
                        read = await source.ReadAsync(plainBuf.AsMemory(1, maxPlain - 1), token) + 1;
                        peeked = null;
                    }
                    else
                    {
                        read = await source.ReadAsync(plainBuf.AsMemory(0, maxPlain), token);
                    }

                    byte delimiter = read == maxPlain ? recordDelimiter : lastRecordDelimiter;
                    if (delimiter != lastRecordDelimiter)
                    {
                        int b = source.ReadByte();
                        if (b == -1) delimiter = lastRecordDelimiter;
                        else peeked = b;
                    }
                    plainBuf[read] = delimiter;

                    for (int i = 0; i < nonce.Length; i++)
                    {
                        byte seqByte = i < 4 ? (byte)0 : (byte)(seq >> 8 * (11 - i));
                        nonce[i] = (byte)(nonceSeed[i] ^ seqByte);
                    }

                    var ct = cipherBuf.AsSpan(0, read + 1);
                    var tag = cipherBuf.AsSpan(read + 1, tagLength);
                    aes.Encrypt(nonce, plainBuf.AsSpan(0, read + 1), ct, tag);

                    await destination.WriteAsync(cipherBuf.AsMemory(0, ct.Length + tagLength), token);

                    if (delimiter == lastRecordDelimiter) break;
                    seq++;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(plainBuf, clearArray: true);
                ArrayPool<byte>.Shared.Return(cipherBuf, clearArray: true);
            }
        }

        // ----------------- ECDH Agreement Calculator -----------------

        private static class ECDHAgreementCalculator
        {
            private const string PRIVATE_DER_IDENTIFIER = "1.2.840.10045.3.1.7";
            private const string PUBLIC_DER_IDENTIFIER = "1.2.840.10045.2.1";
            private const string PUBLIC_PEM_KEY_PREFIX = "-----BEGIN PUBLIC KEY-----";
            private const string PUBLIC_PEM_KEY_SUFFIX = "-----END PUBLIC KEY-----";

            public static (byte[] PublicKey, byte[] SharedSecretHmac) CalculateAgreement(byte[] otherPartyPublicKey, byte[] hmacKey)
            {
                using var agreement = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

                var agreementPublicKey = GetAgreementPublicKey(agreement);
                using var otherKey = GetECDiffieHellmanPublicKey(otherPartyPublicKey);

                var sharedSecretHmac = agreement.DeriveKeyFromHmac(otherKey, HashAlgorithmName.SHA256, hmacKey);

                return (agreementPublicKey, sharedSecretHmac);
            }

            private static byte[] GetAgreementPublicKey(ECDiffieHellman agreement)
            {
                var p = agreement.ExportParameters(false);
                var pk = new byte[p.Q.X!.Length + p.Q.Y!.Length + 1];
                pk[0] = 0x04;
                Array.Copy(p.Q.X, 0, pk, 1, p.Q.X.Length);
                Array.Copy(p.Q.Y, 0, pk, p.Q.X.Length + 1, p.Q.Y.Length);
                return pk;
            }

            private static ECDiffieHellmanPublicKey GetECDiffieHellmanPublicKey(byte[] uncompressedPublicKey)
            {
                using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
                ecdh.ImportFromPem(GetPublicKeyPem(uncompressedPublicKey));
                return ecdh.PublicKey;
            }

            private static ReadOnlySpan<char> GetPublicKeyPem(byte[] uncompressedPublicKey)
            {
                var asn = new AsnWriter(AsnEncodingRules.DER);
                asn.PushSequence();
                asn.PushSequence();
                asn.WriteObjectIdentifier(PUBLIC_DER_IDENTIFIER);
                asn.WriteObjectIdentifier(PRIVATE_DER_IDENTIFIER);
                asn.PopSequence();
                asn.WriteBitString(uncompressedPublicKey);
                asn.PopSequence();

                return PUBLIC_PEM_KEY_PREFIX + Environment.NewLine
                     + Convert.ToBase64String(asn.Encode()) + Environment.NewLine
                     + PUBLIC_PEM_KEY_SUFFIX;
            }
        }

        // ----------------- ES256 Signer -----------------

        private sealed class ES256Signer : IDisposable
        {
            private const string PRIVATE_DER_IDENTIFIER = "1.2.840.10045.3.1.7";
            private const string PRIVATE_PEM_KEY_PREFIX = "-----BEGIN EC PRIVATE KEY-----";
            private const string PRIVATE_PEM_KEY_SUFFIX = "-----END EC PRIVATE KEY-----";

            private readonly ECDsa _signer;

            public ES256Signer(byte[] rawPrivateKey)
            {
                _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                _signer.ImportFromPem(GetPrivateKeyPem(rawPrivateKey));
            }

            public byte[] GenerateSignature(string input) =>
                _signer.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256);

            public void Dispose() => _signer?.Dispose();

            private static ReadOnlySpan<char> GetPrivateKeyPem(byte[] privateKey)
            {
                var asn = new AsnWriter(AsnEncodingRules.DER);
                asn.PushSequence();
                asn.WriteInteger(1);
                asn.WriteOctetString(privateKey);
                asn.PushSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                asn.WriteObjectIdentifier(PRIVATE_DER_IDENTIFIER);
                asn.PopSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                asn.PopSequence();

                return PRIVATE_PEM_KEY_PREFIX + Environment.NewLine
                     + Convert.ToBase64String(asn.Encode()) + Environment.NewLine
                     + PRIVATE_PEM_KEY_SUFFIX;
            }
        }
    }
}
