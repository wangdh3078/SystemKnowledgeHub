import type { Mermaid, MermaidConfig } from 'mermaid'

export type MermaidRuntime = Pick<Mermaid, 'initialize' | 'render'>
export type MermaidLoader = () => Promise<MermaidRuntime>

const mermaidConfiguration: MermaidConfig = {
  startOnLoad: false,
  securityLevel: 'strict',
  htmlLabels: false,
  suppressErrorRendering: true,
  secure: [
    'secure',
    'securityLevel',
    'startOnLoad',
    'maxTextSize',
    'suppressErrorRendering',
    'maxEdges',
    'htmlLabels',
  ],
}

const placeholderSelector = '[data-knowledge-document-mermaid]'
let diagramSequence = 0

async function loadMermaid(): Promise<MermaidRuntime> {
  const module = await import('mermaid')
  return module.default
}

function nextDiagramId(): string {
  diagramSequence += 1
  return `knowledge-document-mermaid-${diagramSequence}`
}

function readSource(block: HTMLElement): string {
  return block.querySelector('code')?.textContent ?? ''
}

function createSourceFallback(source: string): DocumentFragment {
  const fragment = document.createDocumentFragment()
  const caption = document.createElement('figcaption')
  const pre = document.createElement('pre')
  const code = document.createElement('code')
  const error = document.createElement('p')

  caption.className = 'knowledge-document-mermaid__caption'
  caption.textContent = 'Mermaid 图表源码'
  pre.className = 'knowledge-document-mermaid__source'
  code.textContent = source
  pre.append(code)
  error.className = 'knowledge-document-mermaid__error'
  error.setAttribute('role', 'status')
  error.textContent = 'Mermaid 图表无法渲染，已保留源码。'
  fragment.append(caption, pre, error)
  return fragment
}

function showFailure(block: HTMLElement, source: string): void {
  block.classList.remove('knowledge-document-mermaid--rendered')
  block.classList.add('knowledge-document-mermaid--error')
  block.replaceChildren(createSourceFallback(source))
}

function showDiagram(block: HTMLElement, svg: string): HTMLElement {
  const output = document.createElement('div')
  output.className = 'knowledge-document-mermaid__output'
  output.setAttribute('role', 'img')
  output.setAttribute('aria-label', 'Mermaid 图表')
  output.innerHTML = svg

  block.classList.remove('knowledge-document-mermaid--error')
  block.classList.add('knowledge-document-mermaid--rendered')
  block.replaceChildren(output)
  return output
}

export async function hydrateMermaidBlocks(
  root: HTMLElement,
  loader: MermaidLoader = loadMermaid,
): Promise<void> {
  const blocks = Array.from(root.querySelectorAll<HTMLElement>(placeholderSelector))
  if (blocks.length === 0) return

  const sources = new Map(blocks.map((block) => [block, readSource(block)]))
  blocks.forEach((block) => block.removeAttribute('data-knowledge-document-mermaid'))

  let mermaid: MermaidRuntime
  try {
    mermaid = await loader()
    mermaid.initialize(mermaidConfiguration)
  } catch {
    blocks.forEach((block) => {
      if (root.contains(block)) showFailure(block, sources.get(block) ?? '')
    })
    return
  }

  for (const block of blocks) {
    if (!root.contains(block)) continue
    const source = sources.get(block) ?? ''

    try {
      const { svg, bindFunctions } = await mermaid.render(nextDiagramId(), source)
      if (!root.contains(block)) continue
      const output = showDiagram(block, svg)
      bindFunctions?.(output)
    } catch {
      if (root.contains(block)) showFailure(block, source)
    }
  }
}
