export const knowledgeStatuses = ['Unknown', 'Inferred', 'Confirmed'] as const
export type KnowledgeStatus = (typeof knowledgeStatuses)[number]

export const knowledgeStatusLabels: Readonly<Record<KnowledgeStatus, string>> = {
  Unknown: '未知',
  Inferred: '推断',
  Confirmed: '已确认',
}

export function isKnowledgeStatus(value: unknown): value is KnowledgeStatus {
  return value === 'Unknown' || value === 'Inferred' || value === 'Confirmed'
}
