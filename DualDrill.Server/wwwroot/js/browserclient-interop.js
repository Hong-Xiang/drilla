export async function getInteropClass() {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
    var exports = await getAssemblyExports("DualDrill.Client.dll");
    return exports.DualDrill.Client.BrowserJSInterop;
}

const PromiseCompletionSource = {};
const ClientIdHandle = new Promise((resolve, reject) => {
    PromiseCompletionSource.resolve = resolve;
    PromiseCompletionSource.reject = reject;
});

window.DualDrillSetClientId = (id) => {
    PromiseCompletionSource.resolve(id);
};

export function GetClientId() {
    return ClientIdHandle;
}

function normalizedPointerEvent(e) {
    const rect = e.target.getBoundingClientRect();
    const result = {
        detail: e.detail,
        screenX: e.screenX,
        screenY: e.screenY,
        clientX: e.clientX,
        clientY: e.clientY,
        offsetX: e.offsetX,
        offsetY: e.offsetY,
        pageX: e.pageX,
        pageY: e.pageY,
        movementX: e.movementX,
        movementY: e.movementY,
        button: e.button,
        buttons: e.buttons,
        ctrlKey: e.ctrlKey,
        shiftKey: e.shiftKey,
        altKey: e.altKey,
        metaKey: e.metaKey,
        type: e.type,
        pointerId: e.pointerId,
        width: e.width,
        height: e.height,
        pressure: e.pressure,
        tiltX: e.tiltX,
        tiltY: e.tiltY,
        pointerType: e.pointerType,
        isPrimary: e.isPrimary,
        boundingRect: rect
    };
    return result;
}
