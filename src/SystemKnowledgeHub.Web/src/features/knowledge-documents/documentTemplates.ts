import type { DocumentType } from './api/knowledgeDocumentContracts'

export const documentTemplates: Readonly<Record<DocumentType, string>> = {
  Requirement: '## 背景\n\n## 需求说明\n\n## 验收标准\n\n## 依赖与约束',
  Specification: '## 概述\n\n## 范围\n\n## 设计\n\n## 接口\n\n## 数据\n\n## 约束',
  TestCase: '## 前置条件\n\n## 测试步骤\n\n## 预期结果',
  Sop: '## 目的\n\n## 前置条件\n\n## 操作步骤\n\n## 验证\n\n## 回滚',
  Troubleshooting:
    '## 现象\n\n## 影响\n\n## 可能原因\n\n## 排查步骤\n\n## 解决方案\n\n## 验证\n\n## 预防',
  KnowledgeArticle: '## 概述\n\n## 正文',
  DesignNote: '## 背景\n\n## 设计说明\n\n## 决策\n\n## 影响',
}
