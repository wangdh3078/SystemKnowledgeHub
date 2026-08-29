import { ElButton } from 'element-plus'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
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
