import { describe, expect, it, vi } from 'vitest'
import { createPortalReadClient } from './portalReadApi'
import { decodePortalHome, decodePortalPage } from './portalReadContracts'

const homePayload = {
  portalName: '系统知识中心',
  categories: [{ nodeId: 1, title: 'MES', nodeKind: 'Folder', pageId: null }],
  recentPages: [
    {
      id: 9,
      title: 'Lot Track In',
      primaryTarget: { type: 'BusinessFunction', id: 2, title: 'Lot Track In' },
      breadcrumb: [{ nodeId: 1, title: 'MES' }],
      publishedAt: '2026-09-04T01:02:03Z',
    },
  ],
}

describe('Portal read API', () => {
  it('uses only anonymous GET with omitted credentials', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify(homePayload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    const client = createPortalReadClient('/api', fetchMock)

    await expect(client.getHome()).resolves.toEqual(homePayload)
    expect(fetchMock).toHaveBeenCalledWith('/api/portal/home', {
      method: 'GET',
      credentials: 'omit',
      headers: { Accept: 'application/json' },
      signal: undefined,
    })
    expect(Object.keys(client)).toEqual(['getHome', 'getTree', 'getPage', 'search'])
  })

  it('strictly validates home limits and top-level categories', () => {
    expect(() =>
      decodePortalHome({
        ...homePayload,
        categories: [{ ...homePayload.categories[0], nodeKind: 'Page' }],
      }),
    ).toThrow()
    expect(() =>
      decodePortalHome({
        ...homePayload,
        recentPages: Array.from({ length: 9 }, () => homePayload.recentPages[0]),
      }),
    ).toThrow()
  })

  it('decodes every B03 closed section discriminator and rejects unsupported content', () => {
    const base = {
      id: 9,
      title: 'Lot Track In',
      primaryTarget: { type: 'BusinessFunction', id: 2, title: 'Lot Track In' },
      breadcrumb: [],
    }
    const sections = [
      {
        id: 1,
        heading: '摘要',
        sourceKind: 'PrimaryTarget',
        projectionKind: 'Summary',
        content: {
          kind: 'Summary',
          targetType: 'System',
          targetId: 1,
          title: 'MES',
          summary: null,
        },
      },
      {
        id: 2,
        heading: '正文',
        sourceKind: 'ExplicitReference',
        projectionKind: 'KnowledgeDocumentBody',
        content: {
          kind: 'KnowledgeDocumentBody',
          documentId: 3,
          title: '说明',
          documentType: 'KnowledgeArticle',
          bodyMarkdown: '# 正文',
        },
      },
      {
        id: 3,
        heading: '系统',
        sourceKind: 'ExplicitReference',
        projectionKind: 'StructuredOverview',
        content: {
          kind: 'SystemOverview',
          systemId: 1,
          name: 'MES',
          displayName: 'MES',
          systemType: 'Application',
          lifecycle: 'Running',
          purpose: null,
        },
      },
      {
        id: 4,
        heading: '功能',
        sourceKind: 'PrimaryTarget',
        projectionKind: 'StructuredOverview',
        content: {
          kind: 'BusinessFunctionOverview',
          businessFunctionId: 2,
          name: 'TrackIn',
          displayName: 'Lot Track In',
          functionType: 'Workflow',
          systemName: 'MES',
          purpose: null,
          callerSummary: null,
          inputDescription: null,
          outputDescription: null,
        },
      },
      {
        id: 5,
        heading: '对象',
        sourceKind: 'ExplicitReference',
        projectionKind: 'StructuredOverview',
        content: {
          kind: 'DatabaseObjectOverview',
          databaseObjectId: 4,
          schemaName: 'MES',
          objectName: 'LOT',
          objectType: 'Table',
          businessDescription: null,
          databaseComment: '批次',
          estimatedRows: 48000,
          accessMode: 'Read',
          businessKeyColumns: ['LOT_ID'],
        },
      },
      {
        id: 6,
        heading: '集成',
        sourceKind: 'ExplicitReference',
        projectionKind: 'StructuredOverview',
        content: {
          kind: 'IntegrationOverview',
          integrationId: 5,
          name: 'MES → ERP',
          integrationType: 'HttpApi',
          sourcePartyName: 'MES',
          targetPartyName: 'ERP',
          flowDirection: 'OneWay',
          purpose: null,
        },
      },
      {
        id: 7,
        heading: '结构',
        sourceKind: 'ExplicitReference',
        projectionKind: 'DatabaseStructure',
        content: {
          kind: 'DatabaseStructure',
          databaseObjectId: 4,
          schemaName: 'MES',
          objectName: 'LOT',
          objectType: 'Table',
          businessDescription: null,
          databaseComment: '批次',
          estimatedRows: 48000,
          accessMode: 'Read',
          businessKeyColumns: ['LOT_ID'],
          columns: [
            {
              ordinal: 1,
              columnName: 'LOT_ID',
              nativeDataType: 'NUMBER',
              nullable: false,
              databaseComment: '主键',
            },
          ],
        },
      },
    ]

    expect(
      decodePortalPage({ ...base, sections }).sections.map((item) => item.content.kind),
    ).toEqual([
      'Summary',
      'KnowledgeDocumentBody',
      'SystemOverview',
      'BusinessFunctionOverview',
      'DatabaseObjectOverview',
      'IntegrationOverview',
      'DatabaseStructure',
    ])
    expect(() =>
      decodePortalPage({
        ...base,
        sections: [{ ...sections[0], content: { kind: 'Traceability' } }],
      }),
    ).toThrow()
  })
})
