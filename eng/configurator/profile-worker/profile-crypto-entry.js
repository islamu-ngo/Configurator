import { GeneralEncrypt, generalDecrypt, flattenedDecrypt } from "jose";

const policy = Object.freeze({
    keyManagementAlgorithms: ["PBES2-HS512+A256KW"],
    contentEncryptionAlgorithms: ["A256GCM"],
    maxPBES2Count: 600000,
    maxDecompressedLength: 0
});
let recipients = [];
let output = null;
let busy = false;

// The Core codec admits shape, costs and UTF-8 before entering these private imports.
// This module is registered only inside the owned dedicated SDK worker.
export function addRecipient(bytes) {
    if (busy || recipients.length >= 8 || !(bytes instanceof Uint8Array) ||
        bytes.length === 0 || bytes.length > 1024) throw new Error("profile-crypto-input");
    recipients.push(bytes.slice());
    bytes.fill(0);
}

export async function encrypt(payload) {
    if (busy || recipients.length === 0) throw new Error("profile-crypto-busy");
    busy = true;
    try {
        const encryption = new GeneralEncrypt(payload)
            .setProtectedHeader({ enc: "A256GCM", typ: "cfgvault" });
        for (const recipient of recipients)
            encryption.addRecipient(recipient)
                .setUnprotectedHeader({ alg: "PBES2-HS512+A256KW" })
                .setKeyManagementParameters({ p2c: 600000 });
        output = new TextEncoder().encode(JSON.stringify(await encryption.encrypt()));
    } finally {
        payload.fill(0);
        recipients.forEach(bytes => bytes.fill(0));
        recipients = [];
        busy = false;
    }
}

export async function decrypt(container, index, credential) {
    if (busy) throw new Error("profile-crypto-busy");
    busy = true;
    try {
        const envelope = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(container));
        // Core has admitted the exact general or flattened shape, including the index.
        // Keep the authenticated protected string verbatim; never retry another form.
        const result = Object.hasOwn(envelope, "recipients")
            ? await generalDecrypt({
                protected: envelope.protected, iv: envelope.iv, ciphertext: envelope.ciphertext,
                tag: envelope.tag, recipients: [envelope.recipients[index]]
            }, credential, policy)
            : await flattenedDecrypt(envelope, credential, policy);
        output = result.plaintext;
    } finally {
        container.fill(0);
        credential.fill(0);
        busy = false;
    }
}

export function copyOutput() {
    if (busy || output === null) throw new Error("profile-crypto-output");
    return output;
}

export function clear() {
    if (busy) throw new Error("profile-crypto-busy");
    recipients.forEach(bytes => bytes.fill(0));
    recipients = [];
    output?.fill(0);
    output = null;
}
