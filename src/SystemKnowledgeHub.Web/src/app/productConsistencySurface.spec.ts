import { describe, expect, it } from 'vitest'
import appTopBarSource from '../layouts/AppTopBar.vue?raw'
import businessFunctionsSource from '../features/business-functions/pages/BusinessFunctionsListView.vue?raw'
import databaseObjectsSource from '../features/database-knowledge/pages/DatabaseObjectsListView.vue?raw'
import knowledgeDocumentsSource from '../features/knowledge-documents/pages/KnowledgeDocumentsListView.vue?raw'
import systemDetailSource from '../features/systems/pages/SystemDetailView.vue?raw'
import systemsSource from '../features/systems/pages/SystemsListView.vue?raw'
import unknownItemsSource from '../features/unknown-items/pages/UnknownItemsListView.vue?raw'

describe('page-level create consistency', () => {
  it.each([
    ['系统', systemsSource, '新增系统'],
    ['业务功能', businessFunctionsSource, '新增业务功能'],
    ['数据库对象', databaseObjectsSource, '新增数据库对象'],
    ['知识内容', knowledgeDocumentsSource, '新增知识内容'],
    ['待确认事项', unknownItemsSource, '新增待确认事项'],
  ])('%s list keeps its named primary create action in the page header', (_label, view, action) => {
    expect(view).toContain('skh-page-header')
    expect(view).toContain(`type="primary"`)
    expect(view).toMatch(new RegExp(`>\\s*${action}\\s*</el-button`))
  })

  it('keeps the global cross-object create action unchanged', () => {
    expect(appTopBarSource).toContain("kind: 'create-knowledge-object'")
    expect(appTopBarSource).toMatch(/>\s*新增\s*<\/el-button>/)
  })

  it('uses the canonical feature flows rather than introducing page-specific create APIs', () => {
    expect(systemsSource).toContain("kind: 'create-system'")
    expect(businessFunctionsSource).toContain("kind: 'create-business-function'")
    expect(databaseObjectsSource).toContain("kind: 'create-database-knowledge'")
    expect(knowledgeDocumentsSource).toContain('<CreateKnowledgeDocumentDialog')
    expect(unknownItemsSource).toContain("kind: 'create-unknown-item'")
  })
})

describe('focused copy and validation layout contracts', () => {
  it('states the structured System overview boundary without changing document status presentation', () => {
    expect(systemDetailSource).toContain('结构化知识状态概况')
    expect(systemDetailSource).toContain('不包含关联知识文档或业务规则')
    expect(systemDetailSource).toContain('<SystemUnifiedKnowledgeView')
  })
})
