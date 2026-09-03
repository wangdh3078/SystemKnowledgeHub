<script setup lang="ts">
withDefaults(
  defineProps<{
    total: number
    currentPage: number
    pageSize: number
    pageSizes?: readonly number[]
    ariaLabel?: string
  }>(),
  {
    pageSizes: () => [20, 50, 100],
    ariaLabel: '列表分页',
  },
)

const emit = defineEmits<{
  'update:currentPage': [value: number]
  'update:pageSize': [value: number]
  'current-change': [value: number]
  'size-change': [value: number]
}>()

function changePage(value: number): void {
  emit('update:currentPage', value)
  emit('current-change', value)
}

function changePageSize(value: number): void {
  emit('update:pageSize', value)
  emit('size-change', value)
}
</script>

<template>
  <footer v-if="total > 0" class="skh-pagination" :aria-label="ariaLabel">
    <el-pagination
      background
      layout="total, sizes, prev, pager, next, jumper"
      :current-page="currentPage"
      :page-size="pageSize"
      :page-sizes="pageSizes"
      :total="total"
      @current-change="changePage"
      @size-change="changePageSize"
    />
  </footer>
</template>
