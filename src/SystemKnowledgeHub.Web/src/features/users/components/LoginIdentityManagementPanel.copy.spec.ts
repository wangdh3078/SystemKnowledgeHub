import { describe, expect, it } from 'vitest'
import source from './LoginIdentityManagementPanel.vue?raw'

describe('LoginIdentityManagementPanel copy', () => {
  it('explains the identity-provider mapping without conflating roles', () => {
    expect(source).toContain('<h3>登录身份映射（OIDC / SSO）</h3>')
    expect(source).toContain('不是知识身份或权限角色')
    expect(source).not.toContain('技术对象名：LoginIdentity')
    expect(source).toContain('稳定 Subject / sub（由身份提供方提供）')
    expect(source).toContain('不能用姓名或邮箱代替')
  })
})
