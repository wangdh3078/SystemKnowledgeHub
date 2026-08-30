import { describe, expect, it } from 'vitest'
import source from './UsersManagementView.vue?raw'

describe('UsersManagementView status copy', () => {
  it('labels canonical User state without implying login-method state', () => {
    expect(source).toContain('label="用户状态"')
    expect(source).toContain("scope.row.isActive ? '用户启用' : '用户停用'")
    expect(source).toContain('placeholder="用户状态：全部"')
    expect(source).not.toContain('prop="isActive" label="状态"')
  })
})
