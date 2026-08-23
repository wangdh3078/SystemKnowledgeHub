<script setup lang="ts">
import { ElMessageBox } from 'element-plus'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import {
  ChatLineSquare,
  CollectionTag,
  Grid,
  Link,
  List,
  Tickets,
} from '@element-plus/icons-vue'
import { Editor, defaultValueCtx, rootCtx } from '@milkdown/core'
import { listener, listenerCtx } from '@milkdown/plugin-listener'
import {
  createCodeBlockCommand,
  toggleEmphasisCommand,
  toggleInlineCodeCommand,
  toggleLinkCommand,
  toggleStrongCommand,
  wrapInBlockquoteCommand,
  wrapInBulletListCommand,
  wrapInHeadingCommand,
  wrapInOrderedListCommand,
  commonmark,
} from '@milkdown/preset-commonmark'
import { gfm, insertTableCommand } from '@milkdown/preset-gfm'
import { callCommand, getMarkdown, type $Command } from '@milkdown/utils'

const model = defineModel<string>({ required: true })
const emit = defineEmits<{ ready: [markdown: string] }>()
const editorRoot = ref<HTMLElement | null>(null)
const initializationError = ref<string | null>(null)
let editor: Editor | null = null

function run<T>(command: $Command<T>, payload?: T): void {
  if (editor) editor.action(callCommand(command.key, payload))
}

async function addLink(): Promise<void> {
  try {
    const { value } = await ElMessageBox.prompt(
      '请输入链接 URL；先在正文中选中需要显示的文字。',
      '插入链接',
      {
        confirmButtonText: '插入',
        cancelButtonText: '取消',
        inputPattern: /^(https?:\/\/|mailto:|\/)/,
        inputErrorMessage: '请输入 http(s)、mailto 或站内相对地址。',
      },
    )
    run(toggleLinkCommand, { href: value })
  } catch {
    // Cancelling the small link dialog does not change document content.
  }
}

onMounted(async () => {
  if (!editorRoot.value) return
  try {
    editor = await Editor.make()
      .config((ctx) => {
        ctx.set(rootCtx, editorRoot.value)
        ctx.set(defaultValueCtx, model.value)
        ctx.get(listenerCtx).markdownUpdated((_ctx, markdown) => {
          model.value = markdown
        })
      })
      .use(commonmark)
      .use(gfm)
      .use(listener)
      .create()
    const markdown = editor.action(getMarkdown())
    model.value = markdown
    emit('ready', markdown)
  } catch (reason: unknown) {
    initializationError.value =
      reason instanceof Error ? reason.message : '无法初始化 Markdown 编辑器。'
  }
})

onBeforeUnmount(() => {
  if (editor) void editor.destroy()
  editor = null
})
</script>

<template>
  <section class="knowledge-document-editor" aria-label="Markdown 编辑器">
    <div class="knowledge-document-editor__toolbar" role="toolbar" aria-label="文本格式工具">
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="二级标题" placement="top">
          <el-button class="knowledge-document-editor__icon-button knowledge-document-editor__heading-button" aria-label="二级标题" size="small" @click="run(wrapInHeadingCommand, 2)">H2</el-button>
        </el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="粗体" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="粗体" size="small" @click="run(toggleStrongCommand)"><strong>B</strong></el-button></el-tooltip>
        <el-tooltip content="斜体" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="斜体" size="small" @click="run(toggleEmphasisCommand)"><em>I</em></el-button></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="项目符号列表" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="项目符号列表" size="small" @click="run(wrapInBulletListCommand)"><el-icon><List /></el-icon></el-button></el-tooltip>
        <el-tooltip content="编号列表" placement="top"><el-button class="knowledge-document-editor__icon-button knowledge-document-editor__numbered-button" aria-label="编号列表" size="small" @click="run(wrapInOrderedListCommand)">1.</el-button></el-tooltip>
        <el-tooltip content="引用" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="引用" size="small" @click="run(wrapInBlockquoteCommand)"><el-icon><ChatLineSquare /></el-icon></el-button></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="行内代码" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="行内代码" size="small" @click="run(toggleInlineCodeCommand)"><el-icon><CollectionTag /></el-icon></el-button></el-tooltip>
        <el-tooltip content="代码块" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="代码块" size="small" @click="run(createCodeBlockCommand)"><el-icon><Tickets /></el-icon></el-button></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="链接" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="链接" size="small" @click="addLink"><el-icon><Link /></el-icon></el-button></el-tooltip>
        <el-tooltip content="表格" placement="top"><el-button class="knowledge-document-editor__icon-button" aria-label="表格" size="small" @click="run(insertTableCommand, { row: 3, col: 3 })"><el-icon><Grid /></el-icon></el-button></el-tooltip>
      </div>
    </div>
    <p v-if="initializationError" class="knowledge-document-error">
      编辑器加载失败，请刷新后重试。
    </p>
    <div v-else ref="editorRoot" class="knowledge-document-editor__surface"></div>
  </section>
</template>
