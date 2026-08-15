import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export interface ActorContext {
  readonly displayName: string
  readonly role: string | null
}

export const useActorStore = defineStore('actor', () => {
  const displayName = ref('王敏')
  const role = ref<string | null>('知识整理人员')
  const actor = computed<ActorContext>(() => ({
    displayName: displayName.value,
    role: role.value,
  }))

  return { displayName, role, actor }
})
