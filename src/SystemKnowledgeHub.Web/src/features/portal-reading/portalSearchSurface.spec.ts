import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { routes } from '../../app/router/routes'

const layout = readFileSync('src/layouts/PortalLayout.vue', 'utf8')
const search = readFileSync('src/features/portal-reading/pages/PortalSearchView.vue', 'utf8')

describe('PORTAL-B04 search surface', () => {
  it('keeps query, page and page size in the Portal URL', () => {
    expect(routes.find((route) => route.name === 'portal-search')?.path).toBe('/portal/search')
    expect(layout).toContain('搜索知识...')
    expect(layout).toContain("name: 'portal-search'")
    expect(search).toContain('q: query.value, page: value, pageSize: pageSize.value')
    expect(search).toContain('q: query.value, page: 1, pageSize: value')
    expect(search).toContain('未找到匹配的已发布知识。')
    expect(search).toContain('SkhPagination')
  })
})
