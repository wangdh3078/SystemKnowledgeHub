/// <reference types="node" />

import { ElButton } from 'element-plus'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import usersSource from '../features/users/pages/UsersManagementView.vue?raw'
import unknownItemsSource from '../features/unknown-items/pages/UnknownItemsListView.vue?raw'
import documentsSource from '../features/knowledge-documents/pages/KnowledgeDocumentsListView.vue?raw'
import systemsSource from '../features/systems/pages/SystemsListView.vue?raw'
import businessFunctionsSource from '../features/business-functions/pages/BusinessFunctionsListView.vue?raw'
import databaseObjectsSource from '../features/database-knowledge/pages/DatabaseObjectsListView.vue?raw'
import businessFunctionDetailSource from '../features/business-functions/pages/BusinessFunctionDetailView.vue?raw'
import databaseObjectDetailSource from '../features/database-knowledge/pages/DatabaseObjectDetailView.vue?raw'
import businessRuleDetailSource from '../features/business-rules/pages/BusinessRuleDetailView.vue?raw'
import integrationDetailSource from '../features/integrations/pages/IntegrationDetailView.vue?raw'
import knowledgeDocumentDetailSource from '../features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue?raw'
import statusProgressionSource from '../features/knowledge-status/components/KnowledgeStatusProgressionPanel.vue?raw'
import columnDetailSource from '../features/database-knowledge/components/ColumnDetailDrawer.vue?raw'
import userDrawerSource from '../features/users/components/UserManagementDrawer.vue?raw'
import loginIdentitySource from '../features/users/components/LoginIdentityManagementPanel.vue?raw'
import knowledgeRoleSource from '../features/users/components/KnowledgeRoleManagementDialog.vue?raw'
import attachmentAdministrationSource from '../features/attachment-administration/pages/AdministratorAttachmentsView.vue?raw'
import attachmentDetailSource from '../features/attachment-administration/components/AdministratorAttachmentDetailDrawer.vue?raw'
import unknownItemDetailSource from '../features/unknown-items/pages/UnknownItemDetailView.vue?raw'
import appSidebarSource from '../layouts/AppSidebar.vue?raw'

const uiFoundationSource = readFileSync(resolve(process.cwd(), 'src/styles/ui-foundation.css'), 'utf8')
const unknownItemsStylesSource = readFileSync(resolve(process.cwd(), 'src/features/unknown-items/unknown-items.css'), 'utf8')
const databaseKnowledgeStylesSource = readFileSync(resolve(process.cwd(), 'src/features/database-knowledge/database-knowledge.css'), 'utf8')

function readFeatureStyles(directory: string): Readonly<Record<string, string>> {
  const styles: Record<string, string> = {}
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) Object.assign(styles, readFeatureStyles(path))
    else if (entry.isFile() && entry.name.endsWith('.css')) styles[path] = readFileSync(path, 'utf8')
  }
  return styles
}

const featureStyles = readFeatureStyles(resolve(process.cwd(), 'src/features'))

function primaryActionOpening(source: string, label: string): string {
  const match = source.match(new RegExp(`<el-button[^>]*skh-page-primary-action[^>]*>${label}`))
    ?? source.match(new RegExp(`<el-button[^>]*skh-page-primary-action[^>]*>[\\s\\S]{0,80}${label}`))
  expect(match, `${label} should use the shared page action`).not.toBeNull()
  return match?.[0] ?? ''
}

describe('shared UI consistency contracts', () => {
  it('uses the same page primary action contract on all primary list surfaces', () => {
    const pages = [
      usersSource,
      unknownItemsSource,
      documentsSource,
      systemsSource,
      businessFunctionsSource,
      databaseObjectsSource,
    ]

    for (const page of pages) expect(page).toContain('skh-page-primary-action')

    for (const [source, label] of [
      [usersSource, '新增用户'],
      [unknownItemsSource, '新增待确认事项'],
      [documentsSource, '新增知识内容'],
    ] as const) {
      const opening = primaryActionOpening(source, label)
      expect(opening).toContain('type="primary"')
      expect(opening).toContain(':icon="Plus"')
    }
  })

  it('defines an exact shared computed-style contract without feature overrides', () => {
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-page-primary-action\s*\{[^}]*height:\s*36px;[^}]*padding:\s*0 16px;[^}]*font-size:\s*13px;[^}]*font-weight:\s*650;/su)
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-section-action\s*\{[^}]*height:\s*32px;[^}]*padding:\s*0 12px;[^}]*font-size:\s*12px;[^}]*font-weight:\s*620;/su)
    expect(uiFoundationSource).toContain("[class*='el-icon'] + span")
    expect(uiFoundationSource).toContain('margin-left: 6px;')
    expect(uiFoundationSource).toContain('--el-button-disabled-bg-color: #a9a6ed;')

    for (const [path, source] of Object.entries(featureStyles)) {
      expect(source, `${path} must not override the shared action contracts`).not.toMatch(
        /\.skh-(?:page-primary|section|evidence|human-confirmation)-action/u,
      )
    }
  })

  it('keeps descriptive text selectors from recoloring action labels and icons', () => {
    expect(unknownItemsStylesSource).toContain('.unknown-list-header>div>span')
    expect(unknownItemsStylesSource).not.toContain('.unknown-list-header span')
    expect(databaseKnowledgeStylesSource).toContain('.database-object-evidence-section__heading > div:first-child > span')
    expect(databaseKnowledgeStylesSource).not.toMatch(/\.database-object-evidence-section__heading\s+span/u)

    expect(uiFoundationSource).toContain('.el-button.skh-page-primary-action > span')
    expect(uiFoundationSource).toContain('.el-button.skh-evidence-action > span')
    expect(uiFoundationSource).toContain('.el-button.skh-human-confirmation-action > span')
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-page-primary-action\s*\{[^}]*background-color:\s*var\(--color-primary\);[^}]*color:\s*#ffffff;/su)
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-evidence-action\s*\{[^}]*background-color:\s*var\(--color-primary\);[^}]*color:\s*#ffffff;/su)
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-human-confirmation-action\s*\{[^}]*background-color:\s*var\(--color-surface\);[^}]*color:\s*var\(--color-primary\);/su)
  })

  it('keeps Evidence primary and HumanConfirmation outline contracts consistent', () => {
    const evidenceSurfaces = [
      businessFunctionDetailSource,
      databaseObjectDetailSource,
      businessRuleDetailSource,
      integrationDetailSource,
      knowledgeDocumentDetailSource,
    ]
    for (const page of evidenceSurfaces) expect(page).toContain('skh-evidence-action')

    expect(databaseObjectDetailSource).toContain('skh-human-confirmation-action')
    expect(knowledgeDocumentDetailSource).toContain('skh-human-confirmation-action')
    expect(statusProgressionSource).toContain('skh-human-confirmation-action')
    expect(columnDetailSource).toContain('skh-section-action skh-evidence-action')
    expect(columnDetailSource).toContain('>添加证据</el-button>')

    for (const source of [databaseObjectDetailSource, knowledgeDocumentDetailSource]) {
      expect(source.indexOf('skh-evidence-action')).toBeLessThan(
        source.indexOf('skh-human-confirmation-action'),
      )
    }

    expect(uiFoundationSource).toMatch(/\.el-button\.skh-page-primary-action:focus-visible,[^{]*\.el-button\.skh-evidence-action:focus-visible\s*\{[^}]*background:\s*var\(--color-primary-hover\);[^}]*color:\s*#ffffff;/su)
    expect(uiFoundationSource).toMatch(/\.el-button\.skh-human-confirmation-action:focus-visible\s*\{[^}]*background:\s*var\(--color-primary-soft\);[^}]*color:\s*var\(--color-primary-hover\);/su)
  })

  it('keeps primary user-facing labels in Simplified Chinese while preserving technical keywords', () => {
    const auditedSources = [
      userDrawerSource,
      loginIdentitySource,
      knowledgeRoleSource,
      attachmentAdministrationSource,
      attachmentDetailSource,
      unknownItemDetailSource,
      appSidebarSource,
    ]
    const forbidden = [
      'ADMIN · USER PROFILE',
      'ADMIN · ATTACHMENT GOVERNANCE',
      'Knowledge Roles',
      'Kind / 类型',
      'Image / File',
      '附件 metadata',
      'Attachment metadata',
      '所属 KnowledgeDocument',
    ]

    for (const source of auditedSources) {
      for (const phrase of forbidden) expect(source).not.toContain(phrase)
    }

    expect(loginIdentitySource).toContain('OIDC / SSO')
    expect(loginIdentitySource).toContain('Subject / sub')
    expect(attachmentDetailSource).toContain('SHA-256')
    expect(attachmentDetailSource).toContain('附件元数据')
    expect(attachmentAdministrationSource).toContain('图片 / 文件')
  })

  it('preserves primary disabled, outline and danger button variants', () => {
    const primaryDisabled = mount(ElButton, {
      props: { type: 'primary', disabled: true },
      slots: { default: '新增' },
    })
    const secondary = mount(ElButton, {
      props: { plain: true },
      slots: { default: '添加人工确认' },
    })
    const danger = mount(ElButton, {
      props: { type: 'danger', plain: true },
      slots: { default: '删除' },
    })

    expect(primaryDisabled.classes()).toEqual(expect.arrayContaining(['el-button--primary', 'is-disabled']))
    expect(secondary.classes()).toContain('is-plain')
    expect(danger.classes()).toEqual(expect.arrayContaining(['el-button--danger', 'is-plain']))
  })
})
