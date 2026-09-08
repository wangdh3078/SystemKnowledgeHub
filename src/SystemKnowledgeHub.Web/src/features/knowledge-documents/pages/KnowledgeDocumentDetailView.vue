<script setup lang="ts">
import { computed, ref } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute } from 'vue-router'
import KnowledgeDocumentDetailPanel from '../components/KnowledgeDocumentDetailPanel.vue'

const route = useRoute()
const panel = ref<InstanceType<typeof KnowledgeDocumentDetailPanel> | null>(null)
const documentId = computed(() => {
  const id = Number(route.params.id)
  return Number.isSafeInteger(id) && id > 0 ? id : null
})
onBeforeRouteLeave(() => panel.value?.requestLeave() ?? true)
onBeforeRouteUpdate((to, from) =>
  to.params.id !== from.params.id || to.query.view !== from.query.view
    ? (panel.value?.requestLeave() ?? true)
    : true,
)
</script>

<template>
  <KnowledgeDocumentDetailPanel ref="panel" :document-id="documentId" />
</template>
