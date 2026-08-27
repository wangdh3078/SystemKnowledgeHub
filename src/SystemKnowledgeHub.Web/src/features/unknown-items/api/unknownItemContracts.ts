type ApiId = number

export type UnknownItemPriority = 'High' | 'Medium' | 'Low'
export type UnknownItemStatus = 'Open' | 'Investigating' | 'ConclusionConfirmed' | 'Closed'
export type UnknownTargetType = 'System' | 'DatabaseSource' | 'BusinessFunction' | 'DatabaseObject' | 'DatabaseColumn' | 'BusinessRule' | 'Integration'

export interface UnknownTarget { type: UnknownTargetType; id: ApiId }
export interface HistoricalTargetIdentity {
  id: ApiId; targetType: string; displayName: string; isDeleted: boolean; isNavigable: boolean
}
export interface PersonSnapshotInput {
  displayName: string
  roleOrIdentity: string
  occurredAt: string
  team: string | null
  externalUserKey: string | null
  source: string | null
  note: string | null
}
export type PersonSnapshot = PersonSnapshotInput
export interface UnknownTargetSummary { target: UnknownTarget; display: string; primary: boolean; identity?: HistoricalTargetIdentity }
export interface UnknownSystemSummary extends Partial<HistoricalTargetIdentity> { id: ApiId; name: string }
export interface UnknownItemListRow {
  id: ApiId; itemCode: string; question: string; system: UnknownSystemSummary
  primaryTarget: UnknownTarget & { display: string; identity?: HistoricalTargetIdentity }; priority: UnknownItemPriority; status: UnknownItemStatus
  findingCount: number; evidenceCount: number; updatedAt: string
}
export interface UnknownItemsListResponse { items: UnknownItemListRow[]; page: number; pageSize: number; total: number }
export interface Finding { id: ApiId; content: string; recordedBy: PersonSnapshot }
export interface InvestigationEvidence { id: ApiId; subject: UnknownTarget; evidenceType: string; sourceTitle: string }
export interface Resolution { id: ApiId; conclusion: string; confirmedBy: PersonSnapshot | null; confirmedAt: string | null }
export interface KnowledgeUpdate {
  id: ApiId; target: UnknownTarget; targetIdentity?: HistoricalTargetIdentity; subjectDetailKey: string | null; changeSummary: string
  before: unknown; after: unknown; status: 'Proposed' | 'Applied'
}
export type KnowledgeUpdateApplyAction = 'AddColumnKnownValue' | 'UpdateDatabaseColumnKnowledge' | 'UpdateBusinessFunction' | 'UpdateBusinessRule' | 'UpdateIntegration'
export interface KnowledgeUpdateDraft {
  id: ApiId | null; target: UnknownTarget; subjectDetailKey: string | null; applyAction: KnowledgeUpdateApplyAction
  changeSummary: string; before: unknown; after: unknown; knowledgeStatusBefore: null; knowledgeStatusAfter: null
}
export interface UnknownItemActivity {
  type: string; summary: string; occurredAt: string
}
export interface UnknownItemDetailResponse {
  id: ApiId; itemCode: string; system: UnknownSystemSummary; concurrencyToken: string
  question: { text: string; context: string | null; priority: UnknownItemPriority; status: UnknownItemStatus; createdAt: string; updatedAt: string }
  relatedObjects: UnknownTargetSummary[]; findings: Finding[]; evidence: InvestigationEvidence[]
  resolution: Resolution | null; knowledgeUpdates: KnowledgeUpdate[]; activity: UnknownItemActivity[]
  contextRail: { knowledgeImpact: string[]; evidenceCount: number; openGapCount: number }
  availableActions: string[]
}
export interface UnknownItemsListParams {
  keyword?: string; systemId?: ApiId; relatedObjectType?: UnknownTargetType; priority?: UnknownItemPriority
  status?: UnknownItemStatus; sort?: string; page?: number; pageSize?: number
}
export interface CreateUnknownItemInput {
  systemId: ApiId; question: string; context: string | null; priority: UnknownItemPriority
  primaryTarget: UnknownTarget; relatedTargets: UnknownTarget[]; creator: PersonSnapshotInput
}
export interface CreateUnknownItemResponse { id: ApiId; itemCode: string; status: UnknownItemStatus; concurrencyToken: string }
export interface WorkflowResponse { status: UnknownItemStatus; concurrencyToken: string }
export interface AddFindingResponse extends WorkflowResponse { finding: Finding }
export interface AddEvidenceResponse extends WorkflowResponse { evidence: InvestigationEvidence }
export interface SaveResolutionResponse extends WorkflowResponse { resolution: Resolution; knowledgeUpdates: KnowledgeUpdate[] }
export interface ApplyKnowledgeUpdateResponse extends WorkflowResponse {
  unknownItemId: ApiId; unknownItemStatus: UnknownItemStatus
  knowledgeUpdate: { id: ApiId; status: 'Applied'; appliedAt: string }
  target: UnknownTarget; targetKnowledgeStatus: string; targetConcurrencyToken: string; availableActions: string[]
}
export interface ReopenUnknownItemResponse extends WorkflowResponse { appliedKnowledgeUpdatesRetained: boolean }

export const priorityLabels: Record<UnknownItemPriority, string> = { High: '高', Medium: '中', Low: '低' }
export const unknownItemStatusLabels: Record<UnknownItemStatus, string> = {
  Open: '待处理', Investigating: '调查中', ConclusionConfirmed: '结论已确认', Closed: '已关闭',
}
export const targetTypeLabels: Record<UnknownTargetType, string> = {
  System: '系统', DatabaseSource: '数据库来源', BusinessFunction: '业务功能', DatabaseObject: '数据库对象',
  DatabaseColumn: '字段', BusinessRule: '业务规则', Integration: '集成关系',
}
