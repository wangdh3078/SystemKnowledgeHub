import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import KnowledgeDocumentMarkdown from './KnowledgeDocumentMarkdown.vue'
import { renderMarkdown } from './renderMarkdown'

const mermaidMock = vi.hoisted(() => ({
  initialize: vi.fn(),
  render: vi.fn(),
}))

vi.mock('mermaid', () => ({ default: mermaidMock }))

beforeEach(() => {
  mermaidMock.initialize.mockReset()
  mermaidMock.render.mockReset()
})

describe('KnowledgeDocumentMarkdown', () => {
  it('renders controlled Mermaid placeholders and GFM-compatible task items safely', () => {
    const rendered = renderMarkdown(
      [
        '- [ ] 待处理',
        '- [x] 已完成',
        '- \\[ ] 普通文本',
        '',
        '```mermaid',
        'flowchart TD',
        '  A["<script>alert(1)</script>"] --> B',
        '```',
        '',
        '```javascript',
        'console.log("normal fence")',
        '```',
      ].join('\n'),
    )
    const container = document.createElement('div')
    container.innerHTML = rendered

    const checkboxes = container.querySelectorAll<HTMLInputElement>(
      '.knowledge-document-task-checkbox',
    )
    expect(checkboxes).toHaveLength(2)
    expect(checkboxes[0]?.disabled).toBe(true)
    expect(checkboxes[0]?.checked).toBe(false)
    expect(checkboxes[1]?.checked).toBe(true)
    expect(container.textContent).toContain('[ ] 普通文本')

    const placeholder = container.querySelector('[data-knowledge-document-mermaid]')
    expect(placeholder?.textContent).toContain('<script>alert(1)</script>')
    expect(placeholder?.querySelector('script')).toBeNull()
    expect(container.querySelector('code.language-javascript')).not.toBeNull()
  })

  it('does not load Mermaid when the rendered Markdown has no Mermaid placeholder', async () => {
    const wrapper = mount(KnowledgeDocumentMarkdown, { props: { markdown: '# 普通标题' } })

    await flushPromises()

    expect(wrapper.classes()).toContain('knowledge-document-markdown')
    expect(wrapper.classes()).toContain('knowledge-markdown-content')
    expect(wrapper.get('h1').text()).toBe('普通标题')
    expect(mermaidMock.initialize).not.toHaveBeenCalled()
    expect(mermaidMock.render).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('uses the fixed safe Mermaid configuration and replaces a valid source block with SVG', async () => {
    mermaidMock.render.mockResolvedValue({
      svg: '<svg data-testid="rendered-diagram"></svg>',
      diagramType: 'flowchart-v2',
    })
    const wrapper = mount(KnowledgeDocumentMarkdown, {
      props: { markdown: '```mermaid\nflowchart TD\n  A --> B\n```' },
    })

    await flushPromises()

    expect(mermaidMock.initialize).toHaveBeenCalledWith(
      expect.objectContaining({
        startOnLoad: false,
        securityLevel: 'strict',
        htmlLabels: false,
        suppressErrorRendering: true,
      }),
    )
    expect(mermaidMock.render).toHaveBeenCalledWith(
      expect.stringMatching(/^knowledge-document-mermaid-\d+$/u),
      'flowchart TD\n  A --> B\n',
    )
    expect(wrapper.find('[data-testid="rendered-diagram"]').exists()).toBe(true)
    expect(wrapper.find('.knowledge-document-mermaid--rendered').exists()).toBe(true)
    wrapper.unmount()
  })

  it('isolates a failed diagram and retains its source as inert text', async () => {
    mermaidMock.render
      .mockResolvedValueOnce({
        svg: '<svg data-testid="valid-diagram"></svg>',
        diagramType: 'flowchart-v2',
      })
      .mockRejectedValueOnce(new Error('<img src=x onerror=alert(1)>'))
    const wrapper = mount(KnowledgeDocumentMarkdown, {
      props: {
        markdown: [
          '```mermaid',
          'flowchart TD',
          '  A --> B',
          '```',
          '',
          '```mermaid',
          'flowchart TD',
          '  A["<script>alert(1)</script>"] -->',
          '```',
        ].join('\n'),
      },
    })

    await flushPromises()

    expect(wrapper.find('[data-testid="valid-diagram"]').exists()).toBe(true)
    const failed = wrapper.get('.knowledge-document-mermaid--error')
    expect(failed.text()).toContain('<script>alert(1)</script>')
    expect(failed.text()).toContain('Mermaid 图表无法渲染，已保留源码。')
    expect(failed.element.querySelector('script')).toBeNull()
    expect(failed.element.querySelector('img')).toBeNull()
    expect(mermaidMock.render).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })
})
