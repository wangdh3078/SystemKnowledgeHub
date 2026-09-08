import { getKnowledgeDocument } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import { getPortalTargets } from './portalManagementApi'
import type { PortalTargetSummary } from './portalManagementContracts'

// Reuse current-document authority and the Administrator target inventory. Title is
// only a server search hint; identity and lifecycle must match a returned Portal target.
export async function resolveKnowledgeDocumentHandoff(
  id: number,
  signal: AbortSignal,
): Promise<PortalTargetSummary | null> {
  const document = await getKnowledgeDocument(id, signal)
  if (document.lifecycleStatus !== 'Published') return null
  let page = 1
  while (!signal.aborted) {
    const result = await getPortalTargets(
      { type: 'KnowledgeDocument', search: document.title.slice(0, 200), page, pageSize: 100 },
      signal,
    )
    const target = result.items.find((item) => item.type === 'KnowledgeDocument' && item.id === id)
    if (target) return target.lifecycle === 'Published' ? target : null
    if (result.items.length === 0 || page * result.pageSize >= result.total) return null
    page++
  }
  return null
}
