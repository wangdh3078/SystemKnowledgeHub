<script setup lang="ts">
import { computed } from 'vue'
import {
  knowledgeStatusLabels,
  type KnowledgeStatus,
} from '../../api/contracts/knowledge'

const props = defineProps<{ status: KnowledgeStatus }>()

const label = computed(() => knowledgeStatusLabels[props.status])
</script>

<template>
  <span class="knowledge-status-badge" :class="`knowledge-status-badge--${status.toLowerCase()}`">
    {{ label }}
  </span>
</template>
