import { describe, expect, it } from 'vitest'
import { decodeSearchKnowledge } from './searchContracts'

describe('decodeSearchKnowledge', () => {
  it('maps a KnowledgeDocument result with safe navigation and document metadata', () => {
    const result = decodeSearchKnowledge({
      query: '监听',
      total: 1,
      groups: [{
        objectType: 'KnowledgeDocument',
        label: '知识内容',
        items: [{
          id: 42,
          systemContext: '知识内容',
          title: 'Oracle 数据库连接异常处理',
          shortDescription: '…检查 Oracle 数据库监听服务状态…',
          knowledgeStatus: 'Confirmed',
          unknownItemStatus: null,
          contentType: 'Sop',
          lifecycleStatus: 'Published',
          updatedAt: '2026-08-22T08:00:00Z',
          navigation: { routeObjectType: 'KnowledgeDocument', routeObjectId: 42, openDrawer: null, drawerObjectId: null },
        }],
      }],
    })

    const item = result.groups[0].items[0]
    expect(result.groups[0].objectType).toBe('KnowledgeDocument')
    expect(item.contentType).toBe('Sop')
    expect(item.lifecycleStatus).toBe('Published')
    expect(item.navigation).toEqual({ routeObjectType: 'KnowledgeDocument', routeObjectId: 42, openDrawer: null, drawerObjectId: null })
    expect(item.shortDescription).not.toContain('<')
  })
})
