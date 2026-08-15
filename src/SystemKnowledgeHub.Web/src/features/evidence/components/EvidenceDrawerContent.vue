<script setup lang="ts">
import type { OverlayDescriptor } from '../../../app/stores/overlays'
import AddEvidenceDrawer from './AddEvidenceDrawer.vue'
import AddHumanConfirmationDrawer from './AddHumanConfirmationDrawer.vue'
import EvidenceDetailDrawer from './EvidenceDetailDrawer.vue'

defineProps<{ drawer: OverlayDescriptor }>()
</script>

<template>
    <AddEvidenceDrawer v-if="drawer.kind === 'add-evidence' || drawer.kind === 'add-investigation-evidence'" :payload="drawer.payload" />
  <AddHumanConfirmationDrawer v-else-if="drawer.kind === 'human-confirmation'" :payload="drawer.payload" />
  <EvidenceDetailDrawer v-else-if="drawer.kind === 'evidence'" :evidence-id="drawer.id" />
</template>
