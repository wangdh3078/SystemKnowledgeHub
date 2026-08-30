import { describe, expect, it } from 'vitest'
import source from './LoginIdentityManagementPanel.vue?raw'

describe('LoginIdentityManagementPanel copy', () => {
  it('explains the identity-provider mapping without conflating roles', () => {
    expect(source).toContain('<h4>企业统一登录（OIDC / SSO）</h4>')
    expect(source).toContain('不根据姓名、邮箱、工号或用户名自动绑定')
    expect(source).not.toContain('技术对象名：LoginIdentity')
    expect(source).toContain(':model-value="approvedProvider" readonly')
    expect(source).not.toContain('v-model="form.provider"')
    expect(source).toContain('由身份提供方提供的稳定标识')
    expect(source).toContain('<dt>用户状态</dt>')
    expect(source).toContain('<dt>企业统一登录状态</dt>')
    expect(source).toContain('<dt>最终登录状态</dt>')
  })
})
