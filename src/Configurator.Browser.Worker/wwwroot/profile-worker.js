import { dotnet } from "./_framework/dotnet.js";
import * as cryptoBridge from "./vendor/profile-crypto.js";

let active = false;
let output = null;
let entry = null;
let channel = null;

function clear(data) {
    for (const name of ["payload", "credential", "definition", "answers"])
        if (data?.[name] instanceof Uint8Array) data[name].fill(0);
    if (Array.isArray(data?.recipients))
        data.recipients.forEach(bytes => { if (bytes instanceof Uint8Array) bytes.fill(0); });
}

function bounded(data) {
    return typeof data.metadata === "string" && data.metadata.length <= 262144 &&
        [["payload", 4194304], ["credential", 1024], ["definition", 2097152], ["answers", 2097152]]
            .every(([name, limit]) => data[name] instanceof Uint8Array && data[name].length <= limit) &&
        Array.isArray(data.recipients) && data.recipients.length <= 8 &&
        data.recipients.every(bytes => bytes instanceof Uint8Array && bytes.length > 0 && bytes.length <= 1024);
}

self.onmessage = async ({ data }) => {
    if (data?.kind === "boot" && channel === null && typeof data.channel === "string") {
        channel = data.channel;
        try {
            const runtime = await dotnet.withDiagnosticTracing(false)
                .withConfig({ print: () => {}, printErr: () => {} }).create();
            runtime.setModuleImports("profile-crypto", cryptoBridge);
            runtime.setModuleImports("profile-output", {
                set: bytes => {
                    if (!active || output !== null || bytes.length > 4194304)
                        throw new Error("profile-output-invalid");
                    output = bytes.slice();
                    bytes.fill(0);
                }
            });
            const exports = await runtime.getAssemblyExports("Configurator.Browser.Worker");
            entry = exports.Configurator.Browser.Worker.ProfileWorkerEntry;
            await runtime.runMain();
            self.postMessage({ kind: "ready", channel });
        } catch {
            self.postMessage({ kind: "failed", channel });
        }
        return;
    }
    if (data?.kind !== "execute" || data.channel !== channel || !entry || active || !bounded(data)) {
        clear(data);
        self.postMessage({ kind: "failed", channel });
        return;
    }
    active = true;
    try {
        for (const recipient of data.recipients) entry.AddRecipient(recipient);
        const replyText = await entry.Execute(data.metadata, data.payload, data.credential, data.definition, data.answers);
        const reply = JSON.parse(replyText);
        const request = JSON.parse(data.metadata);
        if (reply.requestId !== request.requestId || JSON.stringify(reply.authority) !== JSON.stringify(request.authority))
            throw new Error("profile-reply-invalid");
        const allowed = request.operation === "inspect" ? "inspect"
            : request.operation === "download" ? "artifact"
                : ["seal", "create"].includes(request.operation) ? "profile" : null;
        if (output !== null && (reply.status !== "ok" || reply.outputKind !== allowed || allowed === null))
            throw new Error("profile-output-invalid");
        const response = { kind: "reply", channel, reply: replyText, output };
        if (output !== null) self.postMessage(response, [output.buffer]);
        else self.postMessage(response);
        output = null;
    } catch {
        self.postMessage({ kind: "failed", channel });
    } finally {
        output?.fill(0);
        output = null;
        entry.ClearRecipients();
        clear(data);
        active = false;
    }
};
