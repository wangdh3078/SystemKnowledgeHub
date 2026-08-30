import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
} from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import type { AttachmentRuntimeCapabilities } from '../../runtime-capabilities/api/attachmentRuntimeCapabilities'
import KnowledgeDocumentEditor from './KnowledgeDocumentEditor.vue'

const uploadMock = vi.hoisted(() => vi.fn())
const runtimeCapabilitiesMock = vi.hoisted(() => vi.fn())
vi.mock('../api/knowledgeDocumentAttachmentsApi', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/knowledgeDocumentAttachmentsApi')>()
  return { ...original, uploadKnowledgeDocumentImage: uploadMock }
})
vi.mock('../../runtime-capabilities/api/attachmentRuntimeCapabilities', () => ({
  getAttachmentRuntimeCapabilities: runtimeCapabilitiesMock,
}))

const wrappers: VueWrapper[] = []
const components = {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
  ElTooltip,
}

const defaultRuntimeCapabilities: AttachmentRuntimeCapabilities = {
  allowedImageExtensions: ['.png', '.jpg', '.jpeg', '.gif', '.webp'],
  allowedFileExtensions: ['.pdf', '.txt'],
  maxImageBytes: 10 * 1024 * 1024,
  maxFileBytes: 50 * 1024 * 1024,
  maxStoredAttachmentsPerDocument: 100,
}

beforeEach(() => {
  uploadMock.mockReset()
  runtimeCapabilitiesMock.mockReset()
  runtimeCapabilitiesMock.mockResolvedValue(defaultRuntimeCapabilities)
  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    value: vi.fn((file: File) => `blob:https://local.test/${encodeURIComponent(file.name)}`),
  })
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
})

afterEach(() => wrappers.splice(0).forEach((wrapper) => wrapper.unmount()))

function mountEditor(markdown = '## 标题\n\n正文'): VueWrapper {
  const wrapper = mount(KnowledgeDocumentEditor, {
    attachTo: document.body,
    props: { modelValue: markdown, documentId: 7, attachmentReferences: [] },
    global: {
      components,
      stubs: {
        KnowledgeDocumentMarkdown: {
          props: ['markdown'],
          template: '<div class="rendered">{{ markdown }}</div>',
        },
      },
    },
  })
  wrappers.push(wrapper)
  return wrapper
}

function uploadedImage(attachmentId: number, name = 'diagram.png') {
  const suffixStart = name.lastIndexOf('.')
  const extension = suffixStart > 0 ? name.slice(suffixStart).toLowerCase() : '.png'
  const contentTypeByExtension: Readonly<Record<string, string>> = {
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.gif': 'image/gif',
    '.webp': 'image/webp',
  }
  return {
    attachmentId,
    kind: 'Image' as const,
    originalFileName: name,
    extension,
    contentType: contentTypeByExtension[extension] ?? 'image/png',
    sizeBytes: 24,
    sha256: 'a'.repeat(64),
    previewMode: 'Image' as const,
    canPreview: true,
    canDownload: false,
  }
}

async function selectFiles(wrapper: VueWrapper, files: readonly File[]): Promise<void> {
  const input = wrapper.get('input[type="file"]').element as HTMLInputElement
  Object.defineProperty(input, 'files', { configurable: true, value: files })
  input.dispatchEvent(new Event('change', { bubbles: true }))
  await flushPromises()
}

function dispatchEditorEvent(
  wrapper: VueWrapper,
  type: 'drop' | 'dragover' | 'paste',
  transfer: Readonly<Record<string, unknown>>,
): Event {
  const event = new Event(type, { bubbles: true, cancelable: true })
  Object.defineProperty(event, type === 'paste' ? 'clipboardData' : 'dataTransfer', {
    configurable: true,
    value: { getData: () => '', setData: () => undefined, ...transfer },
  })
  wrapper.get('.cm-content').element.dispatchEvent(event)
  return event
}

async function waitForSourceEditor(wrapper: VueWrapper): Promise<void> {
  await vi.waitFor(() => expect(wrapper.find('.cm-content').exists()).toBe(true))
  await flushPromises()
}

async function fileBytes(file: File): Promise<number[]> {
  return Array.from(new Uint8Array(await file.arrayBuffer()))
}

describe('KnowledgeDocumentEditor', () => {
  it('uses CodeMirror raw Markdown as the only authoring surface and removes color/save actions', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('.cm-content').text()).toContain('## 标题')
    expect(wrapper.get('.cm-content').text()).toContain('正文')
    expect(wrapper.find('.ProseMirror').exists()).toBe(false)
    expect(wrapper.find('[aria-label="文字颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="背景颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="清除文字颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="清除背景颜色"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="保存"]').exists()).toBe(false)
    expect(wrapper.get('[aria-label="插入图片"]').attributes('disabled')).toBeUndefined()
  })

  it('uses on-demand Font Awesome Free icons with icon-only source, preview, and fullscreen controls', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('.knowledge-document-editor__toolbar').classes()).toContain(
      'knowledge-document-editor__toolbar',
    )
    const expectedIcons: ReadonlyArray<readonly [string, string]> = [
      ['无序列表', 'list-ul'],
      ['有序列表', 'list-ol'],
      ['任务列表', 'list-check'],
      ['引用', 'quote-left'],
      ['行内代码', 'code'],
      ['插入代码块', 'file-code'],
      ['插入链接', 'link'],
      ['插入表格', 'table'],
      ['插入图表', 'diagram-project'],
      ['插入图片', 'image'],
      ['撤销', 'rotate-left'],
      ['重做', 'rotate-right'],
      ['源码编辑', 'code'],
      ['预览', 'eye'],
      ['全屏', 'expand'],
    ]
    expectedIcons.forEach(([label, icon]) => {
      expect(wrapper.find(`[aria-label="${label}"] svg[data-icon="${icon}"]`).exists()).toBe(true)
    })
    expect(wrapper.get('[aria-label="源码编辑"]').text()).toBe('')
    expect(wrapper.get('[aria-label="预览"]').text()).toBe('')
    expect(wrapper.get('[aria-label="全屏"]').text()).toBe('')
    expect(wrapper.find('[aria-label="源码"]').exists()).toBe(false)
    expect(wrapper.get('[aria-label="插入图片"]').attributes('disabled')).toBeUndefined()

    await wrapper.setProps({ fullscreen: true })
    expect(wrapper.find('[aria-label="退出全屏"] svg[data-icon="compress"]').exists()).toBe(true)
  })

  it('exposes all eight Mermaid source templates in the compact diagram menu', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    await wrapper.get('[aria-label="插入图表"]').trigger('click')

    expect(wrapper.get('[role="menu"]').attributes('aria-label')).toBe('图表类型')
    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(8)
    expect(wrapper.findAll('[role="menuitem"]').map((item) => item.text())).toEqual([
      '流程图',
      '时序图',
      '甘特图',
      '类图',
      '状态图',
      '饼图',
      '关系图',
      '旅程图',
    ])
  })

  it('keeps the toolbar outside bounded detail and dialog source/preview regions', async () => {
    const detail = mountEditor(
      Array.from({ length: 600 }, (_, index) => `第 ${index + 1} 行`).join('\n'),
    )
    await waitForSourceEditor(detail)

    expect(detail.classes()).toContain('knowledge-document-editor--detail')
    expect(detail.find('.knowledge-document-editor__toolbar').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__source').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__preview').exists()).toBe(true)
    expect(detail.find('.knowledge-document-editor__source .cm-scroller').exists()).toBe(true)

    const dialog = mount(KnowledgeDocumentEditor, {
      props: { modelValue: '正文', viewport: 'dialog' },
      global: { components, stubs: { KnowledgeDocumentMarkdown: true } },
    })
    wrappers.push(dialog)
    await waitForSourceEditor(dialog)
    expect(dialog.classes()).toContain('knowledge-document-editor--dialog')
    expect(dialog.get('[aria-label="插入图片"]').attributes('disabled')).toBeDefined()
  })

  it('emits the page-level save request from Ctrl/Cmd+S without a toolbar save control', async () => {
    const wrapper = mountEditor()
    await waitForSourceEditor(wrapper)

    await wrapper.get('.cm-content').trigger('keydown', { key: 's', ctrlKey: true })

    expect(wrapper.emitted('request-save')).toHaveLength(1)
  })

  it('keeps preview as a rendered boundary and leaves the source editor mounted', async () => {
    const wrapper = mountEditor('`inline`')
    await waitForSourceEditor(wrapper)

    await wrapper.get('[aria-label="预览"]').trigger('click')
    expect(wrapper.emitted('preview')).toHaveLength(1)
    await wrapper.setProps({ previewing: true })
    expect(wrapper.get('.rendered').text()).toContain('`inline`')
    expect(wrapper.find('.cm-content').exists()).toBe(true)
  })

  it('uploads a picker image once and inserts the exact token at the caret without losing content', async () => {
    uploadMock.mockResolvedValue(uploadedImage(123, 'MES设备状态页面.png'))
    const wrapper = mountEditor('原有正文')
    await waitForSourceEditor(wrapper)

    await selectFiles(wrapper, [new File(['png'], 'MES设备状态页面.png', { type: 'image/png' })])

    expect(uploadMock).toHaveBeenCalledTimes(1)
    expect(uploadMock).toHaveBeenCalledWith(7, expect.any(File), expect.any(AbortSignal))
    const updates = wrapper.emitted('update:modelValue') ?? []
    expect(updates.at(-1)?.[0]).toBe('![MES设备状态页面](attachment:123)原有正文')
    expect(wrapper.text()).toContain('保存文档后才会写入修订')
    expect(wrapper.emitted('uploading-change')).toEqual([[true], [false]])
  })

  it('uses the final filename suffix for picker JPEG validation', async () => {
    uploadMock.mockResolvedValue(uploadedImage(124, '照片.jpg1.jpg'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)

    const originalBytes = [0xff, 0xd8, 0xff, 0xe0, 0x4a, 0x46, 0x49, 0x46, 0xff, 0xd9]
    const jpeg = new File([new Uint8Array(originalBytes)], '照片.jpg1.jpg', {
      type: 'image/jpeg',
    })
    await selectFiles(wrapper, [jpeg])

    expect(uploadMock).toHaveBeenCalledWith(7, jpeg, expect.any(AbortSignal))
    expect(await fileBytes(uploadMock.mock.calls[0]?.[1] as File)).toEqual(originalBytes)
    expect((wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]).toBe(
      '![照片.jpg1](attachment:124)正文',
    )
  })

  it('uses the same final-suffix validation for a dragged JPEG with a browser MIME variant', async () => {
    uploadMock.mockResolvedValue(uploadedImage(125, '照片.jpg1.jpg'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)
    const originalBytes = [0xff, 0xd8, 0xff, 0xe1, 0x45, 0x78, 0x69, 0x66, 0xff, 0xd9]
    const jpeg = new File([new Uint8Array(originalBytes)], '照片.jpg1.jpg', {
      type: 'image/jpg',
    })

    const event = dispatchEditorEvent(wrapper, 'drop', {
      types: [],
      files: [jpeg],
      items: [],
      dropEffect: 'none',
    })
    await flushPromises()

    expect(event.defaultPrevented).toBe(true)
    expect(uploadMock).toHaveBeenCalledWith(7, jpeg, expect.any(AbortSignal))
    expect(await fileBytes(uploadMock.mock.calls[0]?.[1] as File)).toEqual(originalBytes)
    expect((wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]).toBe(
      '![照片.jpg1](attachment:125)正文',
    )
  })

  it('uploads multiple dropped images sequentially and preserves success order across one failure', async () => {
    uploadMock
      .mockResolvedValueOnce(uploadedImage(201, '第一张.png'))
      .mockRejectedValueOnce(
        new ApiError(415, {
          code: 'unsupported_media_type',
          message: '附件类型或内容不符合允许规则。',
          fieldErrors: null,
          details: null,
        }),
      )
      .mockResolvedValueOnce(uploadedImage(203, '第三张.png'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)
    const files = [
      new File(['one'], '第一张.png', { type: 'image/png' }),
      new File(['two'], '第二张.png', { type: 'image/png' }),
      new File(['three'], '第三张.png', { type: 'image/png' }),
    ]

    const event = dispatchEditorEvent(wrapper, 'drop', {
      types: ['Files'],
      files,
      dropEffect: 'none',
    })
    await flushPromises()

    expect(event.defaultPrevented).toBe(true)
    expect(uploadMock).toHaveBeenCalledTimes(3)
    const source = (wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]
    expect(source).toBe('![第一张](attachment:201)![第三张](attachment:203)正文')
    expect(wrapper.text()).toContain('第二张.png 不是当前部署支持的')
  })

  it('does not intercept ordinary text drag or paste', async () => {
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)

    const drop = dispatchEditorEvent(wrapper, 'drop', {
      types: ['text/plain'],
      files: [],
      dropEffect: 'none',
    })
    const paste = dispatchEditorEvent(wrapper, 'paste', {
      items: [],
      getData: (format: string) => (format === 'text/plain' ? '普通文本' : ''),
    })
    await flushPromises()

    expect(drop.defaultPrevented).toBe(false)
    expect(paste.defaultPrevented).toBe(true)
    expect(uploadMock).not.toHaveBeenCalled()
    expect((wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]).toBe('普通文本正文')
  })

  it('prioritizes clipboard images, generates a safe name, and uses 截图 alt text', async () => {
    uploadMock.mockResolvedValue(uploadedImage(301, '截图-accepted.png'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)
    const originalBytes = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x49, 0x48]
    const pasted = new File([new Uint8Array(originalBytes)], '', { type: 'image/png' })

    const event = dispatchEditorEvent(wrapper, 'paste', {
      items: [
        { kind: 'string', type: 'text/plain', getAsFile: () => null },
        { kind: 'file', type: 'image/png', getAsFile: () => pasted },
      ],
    })
    await flushPromises()

    expect(event.defaultPrevented).toBe(true)
    const uploadedFile = uploadMock.mock.calls[0]?.[1] as File
    expect(uploadedFile.name).toMatch(/^截图-\d{8}-\d{6}\.png$/u)
    expect(uploadedFile.type).toBe('image/png')
    expect(await fileBytes(uploadedFile)).toEqual(originalBytes)
    expect((wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]).toBe(
      '![截图](attachment:301)正文',
    )
  })

  it('uses the clipboard item JPEG MIME when the pasted File has no name or MIME', async () => {
    uploadMock.mockResolvedValue(uploadedImage(302, '截图-accepted.jpg'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)
    const originalBytes = [0xff, 0xd8, 0xff, 0xe0, 0x4a, 0x46, 0x49, 0x46, 0xff, 0xd9]
    const pasted = new File([new Uint8Array(originalBytes)], '', { type: '' })

    const event = dispatchEditorEvent(wrapper, 'paste', {
      items: [{ kind: 'file', type: 'image/jpeg', getAsFile: () => pasted }],
    })
    await flushPromises()

    expect(event.defaultPrevented).toBe(true)
    const uploadedFile = uploadMock.mock.calls[0]?.[1] as File
    expect(uploadedFile.name).toMatch(/^截图-\d{8}-\d{6}\.jpg$/u)
    expect(uploadedFile.type).toBe('image/jpeg')
    expect(await fileBytes(uploadedFile)).toEqual(originalBytes)
    expect((wrapper.emitted('update:modelValue') ?? []).at(-1)?.[0]).toBe(
      '![截图](attachment:302)正文',
    )
  })

  it('keeps ordinary editor content usable when a clipboard image upload fails', async () => {
    uploadMock.mockRejectedValueOnce(
      new ApiError(503, {
        code: 'attachment_storage_unavailable',
        message: '附件存储暂不可用。',
        fieldErrors: null,
        details: null,
      }),
    )
    const wrapper = mountEditor('不能丢失的正文')
    await waitForSourceEditor(wrapper)

    dispatchEditorEvent(wrapper, 'paste', {
      items: [
        {
          kind: 'file',
          type: 'image/png',
          getAsFile: () => new File(['png'], '', { type: 'image/png' }),
        },
      ],
    })
    await flushPromises()

    expect(wrapper.text()).toContain('附件存储暂不可用')
    expect(wrapper.get('.cm-content').text()).toContain('不能丢失的正文')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('rejects unsupported files locally and keeps backend failures from corrupting source', async () => {
    const wrapper = mountEditor('保留正文')
    await waitForSourceEditor(wrapper)
    await selectFiles(wrapper, [new File(['<svg/>'], 'unsafe.svg', { type: 'image/svg+xml' })])

    expect(uploadMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('不是当前部署支持的 PNG、JPG、JPEG、GIF、WEBP 类型')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()

    uploadMock.mockRejectedValueOnce(
      new ApiError(413, {
        code: 'payload_too_large',
        message: '附件超过配置的大小限制。',
        fieldErrors: null,
        details: null,
      }),
    )
    await selectFiles(wrapper, [new File(['large'], 'large.png', { type: 'image/png' })])

    expect(wrapper.text()).toContain('large.png 超过图片大小限制')
    expect(wrapper.get('.cm-content').text()).toContain('保留正文')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('derives image accept and local extension/size prechecks from runtime capabilities', async () => {
    runtimeCapabilitiesMock.mockResolvedValueOnce({
      ...defaultRuntimeCapabilities,
      allowedImageExtensions: ['.png'],
      maxImageBytes: 3,
    })
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('input[type="file"]').attributes('accept')).toBe('.png')
    await selectFiles(wrapper, [
      new File(['jpg'], 'disabled.jpg', { type: 'image/jpeg' }),
      new File(['four'], 'oversize.png', { type: 'image/png' }),
    ])

    expect(uploadMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('1 个文件不是当前部署支持的 PNG 类型')
    expect(wrapper.text()).toContain('1 个文件超过图片大小限制（3 B）')
    expect(wrapper.get('.cm-content').text()).toContain('正文')
  })

  it('fails closed when runtime image capabilities cannot be loaded', async () => {
    runtimeCapabilitiesMock.mockRejectedValueOnce(new Error('offline'))
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)

    expect(wrapper.get('[aria-label="插入图片"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('input[type="file"]').attributes('accept')).toBe('')
    expect(wrapper.get('input[type="file"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('无法读取当前部署的图片上传能力，上传已禁用')
    expect(uploadMock).not.toHaveBeenCalled()
  })

  it('blocks a duplicate upload start while pending and revokes transient preview URLs on unmount', async () => {
    let resolveUpload: ((value: ReturnType<typeof uploadedImage>) => void) | undefined
    uploadMock.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveUpload = resolve
        }),
    )
    const wrapper = mountEditor('正文')
    await waitForSourceEditor(wrapper)
    const first = new File(['one'], 'first.png', { type: 'image/png' })
    const second = new File(['two'], 'second.png', { type: 'image/png' })

    const firstSelection = selectFiles(wrapper, [first])
    await flushPromises()
    await selectFiles(wrapper, [second])
    expect(uploadMock).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('已有图片正在上传')

    resolveUpload?.(uploadedImage(401, 'first.png'))
    await firstSelection
    await flushPromises()
    wrapper.unmount()

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:https://local.test/first.png')
  })
})
