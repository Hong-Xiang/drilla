function normalizedPointerEvent(e) {
    console.log(e);
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
    console.log(result);
    return result;
}
export function afterWebStarted(blazor) {
    console.log(blazor);
    blazor.registerCustomEventType('normalizedpointerdown', {
        browserEventName: 'pointerdown',
        createEventArgs: normalizedPointerEvent
    });
    blazor.registerCustomEventType('normalizedpointerup', {
        browserEventName: 'pointerup',
        createEventArgs: normalizedPointerEvent
    });
    blazor.registerCustomEventType('normalizedpointermove', {
        browserEventName: 'pointermove',
        createEventArgs: normalizedPointerEvent
    });

}