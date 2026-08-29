import { ElMessageBox } from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  confirmDrawerDiscard,
  hasDirtyDrawer,
  markDrawerDirty,
  resetDrawerDirty,
} from './drawerDirtyState'

describe('drawerDirtyState', () => {
  afterEach(() => {
    resetDrawerDirty()
    vi.restoreAllMocks()
  })

  it('allows a clean Drawer to close without confirmation', async () => {
    const confirm = vi.spyOn(ElMessageBox, 'confirm')

    await expect(confirmDrawerDiscard()).resolves.toBe(true)
    expect(confirm).not.toHaveBeenCalled()
  })

  it('keeps a dirty Drawer open when discard is cancelled', async () => {
    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('cancel')
    markDrawerDirty()

    await expect(confirmDrawerDiscard()).resolves.toBe(false)
    expect(hasDirtyDrawer.value).toBe(true)
  })

  it('allows a dirty Drawer to close only after explicit discard confirmation', async () => {
    vi.spyOn(ElMessageBox, 'confirm').mockResolvedValue('confirm' as never)
    markDrawerDirty()

    await expect(confirmDrawerDiscard()).resolves.toBe(true)
    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      '尚有未保存的修改，确认放弃？',
      '放弃编辑',
      expect.objectContaining({
        confirmButtonText: '放弃修改',
        cancelButtonText: '继续编辑',
      }),
    )
  })
})
