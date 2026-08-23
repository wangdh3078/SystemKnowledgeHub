import { describe, expect, it } from 'vitest'
import {
  hasActiveDirtyDocumentEdit,
  isDocumentEditDirty,
  setActiveDocumentEditDirty,
} from './documentEditState'

const initial = { title: '标题', summary: '摘要', bodyMarkdown: '正文' }

describe('isDocumentEditDirty', () => {
  it('uses values rather than editor change counts', () => {
    expect(isDocumentEditDirty(initial, initial)).toBe(false)
    expect(isDocumentEditDirty({ ...initial, title: '修改后' }, initial)).toBe(true)
    expect(isDocumentEditDirty({ ...initial, bodyMarkdown: '修改后正文' }, initial)).toBe(true)
    expect(isDocumentEditDirty({ ...initial, title: '标题' }, initial)).toBe(false)
  })

  it('exposes the active dirty edit state for session actions without duplicating document data', () => {
    setActiveDocumentEditDirty(false)
    expect(hasActiveDirtyDocumentEdit.value).toBe(false)
    setActiveDocumentEditDirty(true)
    expect(hasActiveDirtyDocumentEdit.value).toBe(true)
    setActiveDocumentEditDirty(false)
  })
})
