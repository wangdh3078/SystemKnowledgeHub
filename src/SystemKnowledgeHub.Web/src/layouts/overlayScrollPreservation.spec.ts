import { afterEach, describe, expect, it, vi } from 'vitest'
import { createOverlayScrollPreserver } from './overlayScrollPreservation'

describe('createOverlayScrollPreserver', () => {
  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
  })

  it('restores the app reading container without using global window scrolling', () => {
    document.body.innerHTML = '<main class="app-content-area__main"></main>'
    const main = document.querySelector<HTMLElement>('.app-content-area__main')!
    main.scrollTop = 480
    main.scrollLeft = 12
    const preserver = createOverlayScrollPreserver()

    preserver.capture()
    main.scrollTop = 0
    main.scrollLeft = 0
    preserver.restore()

    expect(main.scrollTop).toBe(480)
    expect(main.scrollLeft).toBe(12)
  })

  it('restores again after the overlay focus lifecycle has completed', async () => {
    document.body.innerHTML = '<main class="app-content-area__main"></main>'
    const main = document.querySelector<HTMLElement>('.app-content-area__main')!
    main.scrollTop = 320
    const preserver = createOverlayScrollPreserver()
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      callback(0)
      return 1
    })

    preserver.capture()
    main.scrollTop = 0
    preserver.restoreAfterFocus()
    await Promise.resolve()

    expect(main.scrollTop).toBe(320)
  })

  it('restores a replaced reading container after an overlay render', () => {
    document.body.innerHTML = '<main class="app-content-area__main"></main>'
    const main = document.querySelector<HTMLElement>('.app-content-area__main')!
    main.scrollTop = 240
    const preserver = createOverlayScrollPreserver()

    preserver.capture()
    main.replaceWith(main.cloneNode())
    const replacement = document.querySelector<HTMLElement>('.app-content-area__main')!
    preserver.restore()

    expect(replacement.scrollTop).toBe(240)
  })
})
