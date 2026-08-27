import { describe, expect, it } from 'vitest'
import dashboardSource from '../features/dashboard/pages/DashboardView.vue?raw'
import systemsSource from '../features/systems/pages/SystemsListView.vue?raw'
import businessFunctionsSource from '../features/business-functions/pages/BusinessFunctionsListView.vue?raw'
import databaseObjectsSource from '../features/database-knowledge/pages/DatabaseObjectsListView.vue?raw'
import knowledgeDocumentsSource from '../features/knowledge-documents/pages/KnowledgeDocumentsListView.vue?raw'
import knowledgeDocumentDetailSource from '../features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue?raw'
import revisionHistorySource from '../features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.vue?raw'
import revisionCompareSource from '../features/knowledge-documents/components/RevisionCompareView.vue?raw'
import revisionRestoreSource from '../features/knowledge-documents/components/KnowledgeDocumentRestoreDialogContent.vue?raw'
import unknownItemsSource from '../features/unknown-items/pages/UnknownItemsListView.vue?raw'
import unknownItemDetailSource from '../features/unknown-items/pages/UnknownItemDetailView.vue?raw'
import usersSource from '../features/users/pages/UsersManagementView.vue?raw'
import evidenceSource from '../features/evidence/components/EvidenceDetailDrawer.vue?raw'
import relationshipSource from '../features/relationships/components/RelationshipDetailDrawer.vue?raw'
import systemKnowledgeSource from '../features/systems/components/SystemUnifiedKnowledgeView.vue?raw'

const datetimeSurfaces = [
  dashboardSource,
  systemsSource,
  businessFunctionsSource,
  knowledgeDocumentsSource,
  knowledgeDocumentDetailSource,
  revisionHistorySource,
  revisionCompareSource,
  revisionRestoreSource,
  unknownItemsSource,
  unknownItemDetailSource,
  usersSource,
  evidenceSource,
  relationshipSource,
  systemKnowledgeSource,
]

describe('global date-time presentation consistency', () => {
  it.each(datetimeSurfaces)(
    'uses the authoritative shared formatter on each DateTime surface',
    (source) => {
      expect(source).toContain('app/formatters/dateTime')
      expect(source).toContain('formatDateTime(')
    },
  )

  it('does not invent a DateTime column on Database Objects, which has no visible timestamp', () => {
    expect(databaseObjectsSource).not.toContain('label="更新于"')
    expect(databaseObjectsSource).not.toContain('formatDateTime(')
  })

  it.each([
    systemsSource,
    businessFunctionsSource,
    knowledgeDocumentsSource,
    unknownItemsSource,
    usersSource,
  ])('reserves a complete single-line timestamp column', (source) => {
    expect(source).toMatch(/label="更新于"[\s\S]{0,80}width="156"/)
  })

  it('removes page-local locale and substring date-time formatting from audited surfaces', () => {
    const combined = datetimeSurfaces.join('\n')
    expect(combined).not.toContain('new Date(')
    expect(combined).not.toContain('Intl.DateTimeFormat')
    expect(combined).not.toContain("replace('T', ' ').slice")
    expect(combined).not.toContain('slice(0, 10)')
  })
})
