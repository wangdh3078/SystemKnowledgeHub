interface ScrollSnapshot {
  readonly element: HTMLElement
  readonly top: number
  readonly left: number
}

export function createOverlayScrollPreserver() {
  let snapshot: ScrollSnapshot | null = null

  function capture(): void {
    const element = document.querySelector<HTMLElement>('.app-content-area__main')
    snapshot = element === null
      ? null
      : { element, top: element.scrollTop, left: element.scrollLeft }
  }

  function restore(): void {
    if (!snapshot) return
    const element = snapshot.element.isConnected
      ? snapshot.element
      : document.querySelector<HTMLElement>('.app-content-area__main')
    if (!element) return
    if (element.scrollTop !== snapshot.top) element.scrollTop = snapshot.top
    if (element.scrollLeft !== snapshot.left) element.scrollLeft = snapshot.left
  }

  function restoreAfterFocus(): void {
    queueMicrotask(restore)
    requestAnimationFrame(() => requestAnimationFrame(restore))
  }

  function release(): void {
    restore()
    snapshot = null
  }

  return { capture, restore, restoreAfterFocus, release }
}

export const overlayScrollPreserver = createOverlayScrollPreserver()
