<script setup lang="ts">
import { ElMessageBox } from 'element-plus'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import {
  ChatLineSquare,
  Grid,
  Link,
  List,
  Minus,
} from '@element-plus/icons-vue'
import { Editor, defaultValueCtx, rootCtx } from '@milkdown/core'
import { listener, listenerCtx } from '@milkdown/plugin-listener'
import {
  createCodeBlockCommand,
  insertHrCommand,
  toggleEmphasisCommand,
  toggleInlineCodeCommand,
  toggleLinkCommand,
  toggleStrongCommand,
  turnIntoTextCommand,
  wrapInBlockquoteCommand,
  wrapInBulletListCommand,
  wrapInHeadingCommand,
  wrapInOrderedListCommand,
} from '@milkdown/preset-commonmark'
import {
  gfm,
  insertTableCommand,
  toggleStrikethroughCommand,
} from '@milkdown/preset-gfm'
import { callCommand, getMarkdown, type $Command } from '@milkdown/utils'
import { canonicalizeLegacyBreakParagraphs } from '../markdown/legacyMarkdownBreaks'
import { knowledgeDocumentCommonmark } from './milkdownConfig'

const model = defineModel<string>({ required: true })
const emit = defineEmits<{ ready: [markdown: string] }>()
const editorRoot = ref<HTMLElement | null>(null)
const initializationError = ref<string | null>(null)
const editorReady = ref(false)
const tooltipTriggers: ('hover' | 'focus')[] = ['hover', 'focus']
let editor: Editor | null = null

function run<T>(command: $Command<T>, payload?: T): void {
  if (editor && editorReady.value) editor.action(callCommand(command.key, payload))
}

async function addLink(): Promise<void> {
  if (!editorReady.value) return
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
        ctx.set(defaultValueCtx, canonicalizeLegacyBreakParagraphs(model.value))
        ctx.get(listenerCtx).markdownUpdated((_ctx, markdown) => {
          model.value = canonicalizeLegacyBreakParagraphs(markdown)
        })
      })
      .use(knowledgeDocumentCommonmark)
      .use(gfm)
      .use(listener)
      .create()
    editorReady.value = true
    const markdown = canonicalizeLegacyBreakParagraphs(editor.action(getMarkdown()))
    model.value = markdown
    emit('ready', markdown)
  } catch (reason: unknown) {
    editorReady.value = false
    initializationError.value =
      reason instanceof Error ? reason.message : '无法初始化 Markdown 编辑器。'
  }
})

onBeforeUnmount(() => {
  editorReady.value = false
  if (editor) void editor.destroy()
  editor = null
})
</script>

<template>
  <section class="knowledge-document-editor" aria-label="Markdown 编辑器">
    <div class="knowledge-document-editor__toolbar" role="toolbar" aria-label="文本格式工具">
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="正文" placement="top" :trigger="tooltipTriggers">
          <el-button class="knowledge-document-editor__icon-button knowledge-document-editor__heading-button" aria-label="正文" size="small" :disabled="!editorReady" @click="run(turnIntoTextCommand)">正文</el-button>
        </el-tooltip>
        <el-tooltip content="一级标题" placement="top" :trigger="tooltipTriggers">
          <el-button class="knowledge-document-editor__icon-button knowledge-document-editor__heading-button" aria-label="一级标题" size="small" :disabled="!editorReady" @click="run(wrapInHeadingCommand, 1)">H1</el-button>
        </el-tooltip>
        <el-tooltip content="二级标题" placement="top" :trigger="tooltipTriggers">
          <el-button class="knowledge-document-editor__icon-button knowledge-document-editor__heading-button" aria-label="二级标题" size="small" :disabled="!editorReady" @click="run(wrapInHeadingCommand, 2)">H2</el-button>
        </el-tooltip>
        <el-tooltip content="三级标题" placement="top" :trigger="tooltipTriggers">
          <el-button class="knowledge-document-editor__icon-button knowledge-document-editor__heading-button" aria-label="三级标题" size="small" :disabled="!editorReady" @click="run(wrapInHeadingCommand, 3)">H3</el-button>
        </el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="加粗" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="加粗" size="small" :disabled="!editorReady" @click="run(toggleStrongCommand)"><strong>B</strong></el-button></el-tooltip>
        <el-tooltip content="斜体" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="斜体" size="small" :disabled="!editorReady" @click="run(toggleEmphasisCommand)"><em>I</em></el-button></el-tooltip>
        <el-tooltip content="删除线" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button knowledge-document-editor__strike-button" aria-label="删除线" size="small" :disabled="!editorReady" @click="run(toggleStrikethroughCommand)">S</el-button></el-tooltip>
        <el-tooltip content="行内代码" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button knowledge-document-editor__code-label" aria-label="行内代码" size="small" :disabled="!editorReady" @click="run(toggleInlineCodeCommand)">&lt;/&gt;</el-button></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="无序列表" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="无序列表" size="small" :disabled="!editorReady" @click="run(wrapInBulletListCommand)"><el-icon><List /></el-icon></el-button></el-tooltip>
        <el-tooltip content="有序列表" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button knowledge-document-editor__numbered-button" aria-label="有序列表" size="small" :disabled="!editorReady" @click="run(wrapInOrderedListCommand)">1.</el-button></el-tooltip>
        <el-tooltip content="引用" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="引用" size="small" :disabled="!editorReady" @click="run(wrapInBlockquoteCommand)"><el-icon><ChatLineSquare /></el-icon></el-button></el-tooltip>
        <el-tooltip content="代码块" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button knowledge-document-editor__code-label" aria-label="代码块" size="small" :disabled="!editorReady" @click="run(createCodeBlockCommand)">{ }</el-button></el-tooltip>
      </div>
      <div class="knowledge-document-editor__tool-group">
        <el-tooltip content="插入链接" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="插入链接" size="small" :disabled="!editorReady" @click="addLink"><el-icon><Link /></el-icon></el-button></el-tooltip>
        <el-tooltip content="插入表格" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="插入表格" size="small" :disabled="!editorReady" @click="run(insertTableCommand, { row: 3, col: 3 })"><el-icon><Grid /></el-icon></el-button></el-tooltip>
        <el-tooltip content="分隔线" placement="top" :trigger="tooltipTriggers"><el-button class="knowledge-document-editor__icon-button" aria-label="分隔线" size="small" :disabled="!editorReady" @click="run(insertHrCommand)"><el-icon><Minus /></el-icon></el-button></el-tooltip>
      </div>
    </div>
    <p v-if="initializationError" class="knowledge-document-error">
      编辑器加载失败，请刷新后重试。
    </p>
    <div v-else ref="editorRoot" class="knowledge-document-editor__surface"></div>
  </section>
</template>
