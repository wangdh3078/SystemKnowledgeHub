import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { routes } from '../../app/router/routes'
import bootstrapSource from '../../app/bootstrap/bootstrapApp.ts?raw'
import navigationSource from '../../app/router/navigation.ts?raw'
import managementSource from './pages/PortalManagementView.vue?raw'
import previewSource from './components/PortalPreviewDialog.vue?raw'

const stylesSource = readFileSync('src/features/portal-management/portal-management.css', 'utf8')

describe('PORTAL-B02 management surface', () => {
  it('registers an Administrator-only route and navigation entry', () => {
    const route = routes.find((item) => item.name === 'portal-management')
    expect(route?.path).toBe('/portal-management')
    expect(route?.meta?.minimumAccessLevel).toBe('Administrator')
    expect(navigationSource).toContain("label: '知识门户管理'")
    expect(navigationSource).toContain("minimumAccessLevel: 'Administrator'")
  })

  it('is a two-pane composition workbench rather than a CRUD table', () => {
    expect(managementSource).toContain('Portal 页面树')
    expect(managementSource).toContain('页面编排')
    expect(managementSource).toContain('添加章节')
    expect(managementSource).toContain('保存编排')
    expect(managementSource).toContain('Portal 位置')
    expect(managementSource).toContain('尚有未保存的页面编排修改')
    expect(managementSource).toContain("window.addEventListener('beforeunload', beforeUnload)")
    expect(managementSource).toContain('页面已被其他操作修改，请重新加载后再继续。')
    expect(managementSource).toContain('reorderPortalNodes(node.parentNodeId, siblings)')
    expect(managementSource).toContain('需要处理：{{ section.healthMessage }}')
  })

  it('registers the tree and loading integrations used by the real workbench', () => {
    expect(bootstrapSource).toContain('app.use(ElTree)')
    expect(bootstrapSource).toContain('app.use(ElLoading)')
    expect(bootstrapSource).toContain('element-plus/es/components/tree/style/css')
    expect(bootstrapSource).toContain('element-plus/es/components/loading/style/css')
  })

  it('exposes only the four B01-supported projections and no Derived authoring', () => {
    for (const projection of [
      'Summary',
      'KnowledgeDocumentBody',
      'StructuredOverview',
      'DatabaseStructure',
    ]) {
      expect(managementSource).toContain(projection)
    }
    for (const deferred of ['AttachmentList', 'TrustSummary', 'RelatedKnowledge', 'Traceability']) {
      expect(managementSource).not.toContain(deferred)
    }
    expect(managementSource).not.toContain('value="Derived"')
  })

  it('renders preview through the existing safe Markdown component and local table overflow', () => {
    expect(previewSource).toContain('KnowledgeDocumentMarkdown')
    expect(previewSource).not.toContain('v-html')
    expect(previewSource).toContain('portal-preview-table-wrap')
    expect(previewSource).toContain('预览')
    expect(stylesSource).toContain('margin-top: 5vh')
    expect(stylesSource).toContain('max-height: 90vh')
    expect(stylesSource).toContain('overflow-x: auto')
    expect(stylesSource).toContain('@media (max-width: 1180px)')
  })
})
