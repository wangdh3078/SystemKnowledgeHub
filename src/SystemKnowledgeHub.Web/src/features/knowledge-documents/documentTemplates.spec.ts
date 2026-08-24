import { describe, expect, it } from 'vitest'
import { documentTemplates } from './documentTemplates'

describe('KnowledgeDocument source templates', () => {
  it('provides visible raw Markdown headings for every create type', () => {
    Object.values(documentTemplates).forEach((template) => {
      expect(template).toMatch(/^## /u)
      expect(template).toContain('\n\n## ')
      expect(template).not.toContain('<h2>')
    })
  })
})
