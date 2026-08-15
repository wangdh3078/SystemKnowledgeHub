import { onBeforeUnmount, ref } from 'vue'
import { ApiError } from '../../../api/errors/ApiError'
import { businessRulesApi } from '../api/businessRulesApi'
import type { BusinessRuleDetailResponse, UpdateBusinessRuleInput } from '../api/businessRuleContracts'

export function useBusinessRuleDetail() {
  const detail=ref<BusinessRuleDetailResponse|null>(null); const loading=ref(false); const error=ref<string|null>(null); const saving=ref(false); const conflict=ref(false)
  let controller:AbortController|null=null
  async function load(id:number):Promise<boolean>{controller?.abort();controller=new AbortController();loading.value=!detail.value;error.value=null;try{detail.value=await businessRulesApi.detail(id,controller.signal);conflict.value=false;return true}catch(caught:unknown){if(caught instanceof DOMException&&caught.name==='AbortError')return false;error.value=caught instanceof Error?caught.message:'业务规则详情加载失败。';return false}finally{loading.value=false}}
  async function save(values:Omit<UpdateBusinessRuleInput,'concurrencyToken'>):Promise<boolean>{if(!detail.value)return false;saving.value=true;error.value=null;conflict.value=false;try{await businessRulesApi.update(detail.value.id,{...values,concurrencyToken:detail.value.concurrencyToken});return await load(detail.value.id)}catch(caught:unknown){conflict.value=caught instanceof ApiError&&caught.status===409;error.value=caught instanceof Error?caught.message:'业务规则保存失败。';return false}finally{saving.value=false}}
  onBeforeUnmount(()=>controller?.abort())
  return{detail,loading,error,saving,conflict,load,save}
}
