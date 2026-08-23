import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'

export const relationTypes = ['Calls','Reads','Writes','UsesField','AppliesRule','PublishesVia','ConsumesVia','UsesIntegration','DependsOn','Documents','References','AppliesTo','SpecifiedBy','VerifiedBy','Supersedes'] as const
export type RelationType = (typeof relationTypes)[number]
export type KnowledgeTargetType = 'System' | 'DatabaseSource' | 'BusinessFunction' | 'DatabaseObject' | 'DatabaseColumn' | 'BusinessRule' | 'Integration' | 'KnowledgeDocument'
export interface KnowledgeTargetRef { readonly type: KnowledgeTargetType; readonly id: number }
export interface RelationshipSourcePayload { readonly source: KnowledgeTargetRef; readonly title: string; readonly documentType?: string; readonly systemId?: number; readonly systemName?: string }
export interface TargetPreview {
  readonly target: KnowledgeTargetRef
  readonly systemContext: readonly { readonly id: number; readonly name: string }[]
  readonly title: string
  readonly objectTypeLabel: string
  readonly shortDescription: string | null
  readonly knowledgeStatus: KnowledgeStatus
}
export interface KnowledgeTargetsResponse { readonly items: readonly TargetPreview[]; readonly page: number; readonly pageSize: number; readonly total: number }
export interface RelationshipDetailResponse {
  readonly id: number
  readonly concurrencyToken: string
  readonly source: { readonly target: KnowledgeTargetRef; readonly title: string; readonly systemContext: string }
  readonly target: { readonly target: KnowledgeTargetRef; readonly title: string; readonly systemContext: string }
  readonly relationType: RelationType
  readonly description: string | null
  readonly knowledgeStatus: KnowledgeStatus
  readonly evidence: readonly { readonly id: number; readonly evidenceType: string; readonly sourceTitle: string }[]
  readonly unknownItems: readonly unknown[]
  readonly created: { readonly displayName: string; readonly roleOrIdentity: string | null; readonly occurredAt: string }
  readonly statusChanged: { readonly displayName: string; readonly roleOrIdentity: string | null; readonly occurredAt: string }
  readonly availableActions: readonly string[]
}
export interface AddRelationshipRequest { readonly source: KnowledgeTargetRef; readonly relationType: RelationType; readonly target: KnowledgeTargetRef; readonly description: string | null }
export interface AddRelationshipResponse { readonly id: number; readonly source: KnowledgeTargetRef; readonly relationType: RelationType; readonly target: KnowledgeTargetRef; readonly knowledgeStatus: 'Unknown'; readonly concurrencyToken: string }
export interface RelatedKnowledge { readonly id:number; readonly direction:'Incoming'|'Outgoing'; readonly relationType:RelationType; readonly related:KnowledgeTargetRef; readonly title:string; readonly objectTypeLabel:string }
export interface UpdateRelationshipDescriptionRequest { readonly description: string | null; readonly concurrencyToken: string }
export interface ChangeRelationshipStatusRequest { readonly targetStatus: KnowledgeStatus; readonly reason: string | null; readonly concurrencyToken: string }

export const relationTypeLabels: Readonly<Record<RelationType, string>> = {
  Calls:'调用', Reads:'读取', Writes:'写入', UsesField:'使用字段', AppliesRule:'应用规则',
  PublishesVia:'通过集成发布', ConsumesVia:'通过集成消费', UsesIntegration:'使用集成', DependsOn:'依赖',
  Documents:'说明', References:'引用', AppliesTo:'适用于', SpecifiedBy:'由规格说明定义',
  VerifiedBy:'由测试用例验证', Supersedes:'替代旧文档',
}

const incomingRelationTypeLabels: Readonly<Partial<Record<RelationType, string>>> = {
  Documents: '由文档说明',
  References: '被引用',
  AppliesTo: '适用文档',
  SpecifiedBy: '定义需求',
  VerifiedBy: '验证需求/规格',
  Supersedes: '被新文档替代',
}

export function contextualRelationTypeLabel(relationType: RelationType, direction: 'Incoming' | 'Outgoing' = 'Outgoing'): string {
  return direction === 'Incoming' ? incomingRelationTypeLabels[relationType] ?? relationTypeLabels[relationType] : relationTypeLabels[relationType]
}

type Obj = Readonly<Record<string, unknown>>
function obj(v: unknown, f: string): Obj { if (typeof v !== 'object' || v === null || Array.isArray(v)) throw new Error(`${f} invalid`); return v as Obj }
function str(v: unknown, f: string): string { if (typeof v !== 'string') throw new Error(`${f} invalid`); return v }
function nullableStr(v: unknown, f: string): string | null { return v === null ? null : str(v, f) }
function id(v: unknown, f: string): number { if (typeof v !== 'number' || !Number.isSafeInteger(v) || v < 1) throw new Error(`${f} invalid`); return v }
function status(v: unknown): KnowledgeStatus { if (!isKnowledgeStatus(v)) throw new Error('knowledgeStatus invalid'); return v }
function relation(v: unknown): RelationType { const x = str(v,'relationType'); if (!relationTypes.some(t => t === x)) throw new Error('relationType invalid'); return x as RelationType }
function target(v: unknown): KnowledgeTargetRef { const x=obj(v,'target'); return {type:str(x.type,'target.type') as KnowledgeTargetType,id:id(x.id,'target.id')} }
function strings(v: unknown): readonly string[] { if(!Array.isArray(v)) throw new Error('array invalid'); return v.map((x,i)=>str(x,`[${i}]`)) }

export function isRelationshipSourcePayload(v: unknown): v is RelationshipSourcePayload {
  if (typeof v !== 'object' || v === null || Array.isArray(v)) return false
  const x=v as Obj
  return typeof x.title==='string' && typeof x.source==='object' && x.source!==null
}
export function decodeTargets(v: unknown): KnowledgeTargetsResponse {
  const x=obj(v,'targets'); if(!Array.isArray(x.items)) throw new Error('items invalid')
  return {items:x.items.map((raw,i)=>{const item=obj(raw,`items[${i}]`); if(!Array.isArray(item.systemContext)) throw new Error('systemContext invalid'); return {target:target(item.target),systemContext:item.systemContext.map(c=>{const s=obj(c,'system');return{id:id(s.id,'system.id'),name:str(s.name,'system.name')}}),title:str(item.title,'title'),objectTypeLabel:str(item.objectTypeLabel,'objectTypeLabel'),shortDescription:nullableStr(item.shortDescription,'shortDescription'),knowledgeStatus:status(item.knowledgeStatus)}}),page:id(x.page,'page'),pageSize:id(x.pageSize,'pageSize'),total:typeof x.total==='number'?x.total:0}
}
export function decodeRelationshipDetail(v: unknown): RelationshipDetailResponse {
  const x=obj(v,'relationship'); const endpoint=(raw:unknown)=>{const e=obj(raw,'endpoint');return{target:target(e.target),title:str(e.title,'title'),systemContext:str(e.systemContext,'systemContext')}}; const person=(raw:unknown)=>{const p=obj(raw,'person');return{displayName:str(p.displayName,'displayName'),roleOrIdentity:nullableStr(p.roleOrIdentity,'role'),occurredAt:str(p.occurredAt,'occurredAt')}}
  if(!Array.isArray(x.evidence)||!Array.isArray(x.unknownItems)) throw new Error('collections invalid')
  return{id:id(x.id,'id'),concurrencyToken:str(x.concurrencyToken,'token'),source:endpoint(x.source),target:endpoint(x.target),relationType:relation(x.relationType),description:nullableStr(x.description,'description'),knowledgeStatus:status(x.knowledgeStatus),evidence:x.evidence.map(raw=>{const e=obj(raw,'evidence');return{id:id(e.id,'evidence.id'),evidenceType:str(e.evidenceType,'type'),sourceTitle:str(e.sourceTitle,'title')}}),unknownItems:x.unknownItems,created:person(x.created),statusChanged:person(x.statusChanged),availableActions:strings(x.availableActions)}
}
export function decodeAddRelationship(v: unknown): AddRelationshipResponse { const x=obj(v,'created'); const ks=status(x.knowledgeStatus); if(ks!=='Unknown') throw new Error('new relationship must be Unknown'); return{id:id(x.id,'id'),source:target(x.source),relationType:relation(x.relationType),target:target(x.target),knowledgeStatus:ks,concurrencyToken:str(x.concurrencyToken,'token')} }
export function decodeDescription(v: unknown): {id:number;description:string|null;knowledgeStatus:KnowledgeStatus;concurrencyToken:string} { const x=obj(v,'description');return{id:id(x.id,'id'),description:nullableStr(x.description,'description'),knowledgeStatus:status(x.knowledgeStatus),concurrencyToken:str(x.concurrencyToken,'token')} }
export function decodeStatusChange(v: unknown): {relationshipId:number;knowledgeStatus:KnowledgeStatus;concurrencyToken:string} {const x=obj(v,'status');return{relationshipId:id(x.relationshipId,'id'),knowledgeStatus:status(x.knowledgeStatus),concurrencyToken:str(x.concurrencyToken,'token')}}
export function decodeRelatedKnowledge(v:unknown):readonly RelatedKnowledge[]{if(!Array.isArray(v))throw new Error('relations invalid');return v.map((raw,index)=>{const x=obj(raw,`relations[${index}]`);const direction=str(x.direction,'direction');if(direction!=='Incoming'&&direction!=='Outgoing')throw new Error('direction invalid');return{id:id(x.id,'id'),direction,relationType:relation(x.relationType),related:target(x.related),title:str(x.title,'title'),objectTypeLabel:str(x.objectTypeLabel,'objectTypeLabel')}})}
