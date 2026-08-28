import type { ActorContext } from '../../../app/stores/actor'
import type { KnowledgeStatus } from '../../../api/contracts/knowledge'

export interface BusinessRuleInputData { readonly name: string; readonly description: string | null }
export interface BusinessRuleRelationship { readonly relationshipId: number; readonly id: number; readonly name: string; readonly relationType: string }
export interface BusinessRuleEvidence { readonly id: number; readonly evidenceType: string; readonly sourceTitle: string }
export interface BusinessRuleUnknownItem { readonly id: number; readonly question: string; readonly status: string }
export interface BusinessRuleDetailResponse {
  readonly id: number
  readonly system: { readonly id: number; readonly name: string }
  readonly concurrencyToken: string
  readonly header: { readonly name: string; readonly knowledgeStatus: KnowledgeStatus }
  readonly description: string
  readonly condition: string | null
  readonly result: string | null
  readonly inputData: readonly BusinessRuleInputData[]
  readonly relatedFunctions: readonly BusinessRuleRelationship[]
  readonly relatedFields: readonly BusinessRuleRelationship[]
  readonly integrations: readonly BusinessRuleRelationship[]
  readonly evidence: readonly BusinessRuleEvidence[]
  readonly unknownItems: readonly BusinessRuleUnknownItem[]
  readonly contextRail: { readonly relationshipCount: number; readonly openUnknownCount: number }
  readonly canDelete: boolean
  readonly availableActions: readonly string[]
}
export interface BusinessRuleWriteInput {
  readonly name: string; readonly description: string; readonly condition: string | null
  readonly result: string | null; readonly inputData: readonly BusinessRuleInputData[]; readonly actor: ActorContext
}
export interface CreateBusinessRuleInput extends BusinessRuleWriteInput { readonly systemId: number }
export interface UpdateBusinessRuleInput extends BusinessRuleWriteInput { readonly concurrencyToken: string }
export interface BusinessRuleWriteResponse extends BusinessRuleDetailResponse {
  readonly evidence: readonly BusinessRuleEvidence[]
}

function object(value: unknown, field: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw new TypeError(`${field} 必须是对象。`)
  return value as Record<string, unknown>
}
function string(value: unknown, field: string): string { if (typeof value !== 'string') throw new TypeError(`${field} 必须是字符串。`); return value }
function nullableString(value: unknown, field: string): string | null { return value === null ? null : string(value, field) }
function number(value: unknown, field: string): number { if (!Number.isSafeInteger(value) || Number(value) < 1) throw new TypeError(`${field} 必须是安全正整数。`); return Number(value) }
function array(value: unknown, field: string): readonly unknown[] { if (!Array.isArray(value)) throw new TypeError(`${field} 必须是数组。`); return value }
function boolean(value: unknown, field: string): boolean { if (typeof value !== 'boolean') throw new TypeError(`${field} 必须是布尔值。`); return value }
function status(value: unknown): KnowledgeStatus { const text=string(value,'knowledgeStatus'); if (!['Unknown','Inferred','Confirmed'].includes(text)) throw new TypeError('knowledgeStatus 无效。'); return text as KnowledgeStatus }
function relation(value: unknown, field: string): BusinessRuleRelationship { const item=object(value,field); return { relationshipId:number(item.relationshipId,`${field}.relationshipId`), id:number(item.id,`${field}.id`), name:string(item.name,`${field}.name`), relationType:string(item.relationType,`${field}.relationType`) } }

export function decodeBusinessRuleDetail(value: unknown): BusinessRuleDetailResponse {
  const root=object(value,'businessRule'); const system=object(root.system,'system'); const header=object(root.header,'header'); const rail=object(root.contextRail,'contextRail')
  return {
    id:number(root.id,'id'), system:{id:number(system.id,'system.id'),name:string(system.name,'system.name')},
    concurrencyToken:string(root.concurrencyToken,'concurrencyToken'), header:{name:string(header.name,'header.name'),knowledgeStatus:status(header.knowledgeStatus)},
    description:string(root.description,'description'), condition:nullableString(root.condition,'condition'), result:nullableString(root.result,'result'),
    inputData:array(root.inputData,'inputData').map((value,index)=>{const item=object(value,`inputData[${index}]`);return{name:string(item.name,`inputData[${index}].name`),description:nullableString(item.description,`inputData[${index}].description`)}}),
    relatedFunctions:array(root.relatedFunctions,'relatedFunctions').map((value,index)=>relation(value,`relatedFunctions[${index}]`)),
    relatedFields:array(root.relatedFields,'relatedFields').map((value,index)=>relation(value,`relatedFields[${index}]`)),
    integrations:array(root.integrations,'integrations').map((value,index)=>relation(value,`integrations[${index}]`)),
    evidence:array(root.evidence,'evidence').map((value,index)=>{const item=object(value,`evidence[${index}]`);return{id:number(item.id,`evidence[${index}].id`),evidenceType:string(item.evidenceType,`evidence[${index}].evidenceType`),sourceTitle:string(item.sourceTitle,`evidence[${index}].sourceTitle`)}}),
    unknownItems:array(root.unknownItems,'unknownItems').map((value,index)=>{const item=object(value,`unknownItems[${index}]`);return{id:number(item.id,`unknownItems[${index}].id`),question:string(item.question,`unknownItems[${index}].question`),status:string(item.status,`unknownItems[${index}].status`)}}),
    contextRail:{relationshipCount:Number(rail.relationshipCount),openUnknownCount:Number(rail.openUnknownCount)}, canDelete:boolean(root.canDelete,'canDelete'), availableActions:array(root.availableActions,'availableActions').map((item,index)=>string(item,`availableActions[${index}]`)),
  }
}

export function decodeBusinessRuleWrite(value: unknown): BusinessRuleWriteResponse {
  const root=object(value,'businessRule'); const system=object(root.system,'system')
  return {
    id:number(root.id,'id'), system:{id:number(system.id,'system.id'),name:string(system.name,'system.name')}, concurrencyToken:string(root.concurrencyToken,'concurrencyToken'),
    header:{name:string(root.name,'name'),knowledgeStatus:status(root.knowledgeStatus)}, description:string(root.description,'description'),
    condition:nullableString(root.condition,'condition'),result:nullableString(root.result,'result'),inputData:array(root.inputData,'inputData').map((value,index)=>{const item=object(value,`inputData[${index}]`);return{name:string(item.name,`inputData[${index}].name`),description:nullableString(item.description,`inputData[${index}].description`)}}),
    relatedFunctions:[],relatedFields:[],integrations:[],evidence:[],unknownItems:[],contextRail:{relationshipCount:0,openUnknownCount:0},canDelete:false,availableActions:[],
  }
}
