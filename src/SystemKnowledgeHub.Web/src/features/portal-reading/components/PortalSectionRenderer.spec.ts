import { shallowMount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import PortalSectionRenderer from './PortalSectionRenderer.vue'
import type { PortalPageSection } from '../api/portalReadContracts'

function section(
  content: PortalPageSection['content'],
  projectionKind: PortalPageSection['projectionKind'],
): PortalPageSection {
  return { id: 1, heading: '章节', sourceKind: 'PrimaryTarget', projectionKind, content }
}

describe('PortalSectionRenderer', () => {
  it('renders Summary and KnowledgeDocumentBody through the existing Markdown component', () => {
    const summary = shallowMount(PortalSectionRenderer, {
      props: {
        section: section(
          {
            kind: 'Summary',
            targetType: 'BusinessFunction',
            targetId: 2,
            title: 'Lot Track In',
            summary: '批次进站',
          },
          'Summary',
        ),
      },
    })
    expect(summary.text()).toContain('业务功能')
    expect(summary.text()).toContain('批次进站')

    const body = shallowMount(PortalSectionRenderer, {
      props: {
        section: section(
          {
            kind: 'KnowledgeDocumentBody',
            documentId: 3,
            title: '业务说明',
            documentType: 'KnowledgeArticle',
            bodyMarkdown: '# 安全正文',
          },
          'KnowledgeDocumentBody',
        ),
      },
      global: {
        stubs: {
          KnowledgeDocumentMarkdown: {
            props: ['markdown'],
            template: '<div class="markdown-stub">{{ markdown }}</div>',
          },
        },
      },
    })
    expect(body.get('.markdown-stub').text()).toBe('# 安全正文')
  })

  it('renders every structured overview without raw identifiers or management actions', () => {
    const contents: PortalPageSection['content'][] = [
      {
        kind: 'SystemOverview',
        systemId: 1,
        name: 'MES',
        displayName: '制造执行系统',
        systemType: 'Application',
        lifecycle: 'Running',
        purpose: '生产执行',
      },
      {
        kind: 'BusinessFunctionOverview',
        businessFunctionId: 2,
        name: 'TrackIn',
        displayName: 'Lot Track In',
        functionType: 'Workflow',
        systemName: 'MES',
        purpose: '进站',
        callerSummary: null,
        inputDescription: 'Lot',
        outputDescription: 'Result',
      },
      {
        kind: 'DatabaseObjectOverview',
        databaseObjectId: 4,
        schemaName: 'MES',
        objectName: 'LOT',
        objectType: 'Table',
        businessDescription: '批次',
        databaseComment: '批次主表',
        estimatedRows: 48000,
        accessMode: 'Read',
        businessKeyColumns: ['LOT_ID'],
      },
      {
        kind: 'IntegrationOverview',
        integrationId: 5,
        name: 'MES → ERP',
        integrationType: 'HttpApi',
        sourcePartyName: 'MES',
        targetPartyName: 'ERP',
        flowDirection: 'OneWay',
        purpose: '同步批次',
      },
    ]
    const text = contents
      .map((content) =>
        shallowMount(PortalSectionRenderer, {
          props: { section: section(content, 'StructuredOverview') },
        }).text(),
      )
      .join(' ')
    expect(text).toContain('制造执行系统')
    expect(text).toContain('Lot Track In')
    expect(text).toContain('MES.LOT')
    expect(text).toContain('MES → ERP')
    expect(text).not.toMatch(/systemId|databaseObjectId|编辑|删除|发布/u)
  })

  it('renders DatabaseStructure in one locally scrollable semantic table', () => {
    const wrapper = shallowMount(PortalSectionRenderer, {
      props: {
        section: section(
          {
            kind: 'DatabaseStructure',
            databaseObjectId: 4,
            schemaName: 'MES',
            objectName: 'LOT',
            objectType: 'Table',
            businessDescription: '批次',
            databaseComment: '批次主表',
            estimatedRows: 48000,
            accessMode: 'Read',
            businessKeyColumns: ['LOT_ID'],
            columns: [
              {
                ordinal: 1,
                columnName: 'LOT_ID',
                nativeDataType: 'NUMBER(19)',
                nullable: false,
                databaseComment: '主键',
              },
            ],
          },
          'DatabaseStructure',
        ),
      },
    })
    expect(wrapper.get('.portal-table-wrap').attributes('tabindex')).toBe('0')
    expect(wrapper.get('table').text()).toContain('LOT_ID')
    expect(wrapper.text()).toContain('48,000')
    expect(wrapper.text()).toContain('批次主表')
  })

  it('fails closed for an unknown runtime discriminator and logs only safe diagnostics', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const invalid = section(
      { kind: 'Summary', targetType: 'System', targetId: 1, title: 'MES', summary: null },
      'Summary',
    ) as unknown as PortalPageSection
    ;(invalid as { content: { kind: string } }).content = { kind: 'Traceability' }
    const wrapper = shallowMount(PortalSectionRenderer, { props: { section: invalid } })
    expect(wrapper.text()).toContain('该内容暂不可显示')
    expect(warn).toHaveBeenCalledWith('Portal section discriminator is unsupported.', {
      sectionId: 1,
      projectionKind: 'Summary',
    })
    warn.mockRestore()
  })
})
