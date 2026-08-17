# VS13 — Global Search Verification Report

状态：**VS13 PASS**

## 实现范围

- 实现 Q02 `SearchKnowledge` 与唯一 canonical route：`GET /api/search`。
- 搜索对象：System、BusinessFunction、DatabaseObject、DatabaseColumn、BusinessRule、Integration、UnknownItem。
- TopBar 启用唯一全局搜索入口，并支持 `⌘ / Ctrl + K`、Arrow Up / Down、Enter、Esc。
- 实现 OV-01 最近搜索/访问、OV-02 分组结果与 OV-03 无结果恢复路径；Global Search 保持 Overlay，不新增 Route。

## 查询与持久化

- 策略：SQLite 具体表上的受限 `LIKE` 投影；每个分组默认最多 5 条、最大 20 条。
- 未启用 FTS5 / trigram：它们仍是可选派生加速，不是 Q02 或领域 Schema 的前置条件。
- 搜索字段覆盖名称、中文业务描述、Schema/Object/Column 技术标识、Known Values、规则条件/结果、Integration endpoint/topic，以及待确认事项问题/上下文。
- DatabaseColumn 结果返回 DatabaseObject route 和 `DatabaseColumn` Drawer 导航意图。
- 无 Schema / Migration；未创建 Search Domain Entity、Search Aggregate、Search Repository 或独立搜索事实表。

## API 与分组

- `q`：trim 后 1–100 字符；空查询不调用 API，由客户端展示会话级辅助状态。
- `types`：仅允许冻结的七类对象；`limitPerGroup`：默认 5、最大 20。
- Result 按对象类型分组；所有项带 System Context、短描述与正确状态。
- KnowledgeStatus 与 UnknownItemStatus 独立返回、独立显示；搜索不写任何领域数据。

## Focused tests

运行 `GlobalSearchApiTests`：**3 passed**。

1. 跨 System、BusinessFunction、DatabaseObject、DatabaseColumn 的分组搜索与状态字段。
2. `STATE_FLAG` 技术标识、type filter、group limit 和 Column Drawer navigation。
3. UnknownItem 的 `Investigating` 状态不混入 KnowledgeStatus。

## Build / Type Check

- `dotnet build SystemKnowledgeHub.sln --no-restore`：PASS（0 warnings / 0 errors）。
- `dotnet test ... --filter FullyQualifiedName~GlobalSearchApiTests`：PASS（3 passed）。
- `npm run type-check`：PASS。
- `npm run build`：PASS；仅有既有 Vite bundle size 提示。

## Runtime verification

- TopBar 打开 Search Overlay；空查询显示最近搜索与最近访问的会话级状态。
- `STATE_FLAG`：字段、业务规则和待确认事项分组正确；Arrow Down / Up 选择后 Enter 进入 `MES.TABLE_EQP`，URL 带 `selectedColumnId=123`，并自动打开 `STATE_FLAG` Column Drawer。
- `Equipment Status Query`：业务功能分组正确；Enter 进入现有 Business Function Detail，Overlay 关闭。
- `STATE_FLAG=90`：待确认事项分组显示独立“调查中”状态；无结果状态显示技术标识/业务描述恢复建议；Esc 关闭 Overlay。
- 对同一 Database Object Detail 的字段搜索已验证：route query 变化后仍重新加载并打开现有 Drawer。

## Golden UI Review

- 对照：OV-01、OV-02、OV-03。
- 保持浅色 desktop Application Shell、居中 Overlay、搜索框视觉重点、分组标题、紧凑结果行、技术标识原文、系统上下文、状态标签和键盘 active state。
- 结果不会变成 Generic List；字段导航复用 RP-07 + DR-03，不新增 Column Route 或第二个 Drawer / Overlay manager。

## Specification Deviation

无阻塞性 Specification Deviation。

## Process cleanup

- 本轮 ASP.NET Core、Vite、浏览器自动化与 `.runtime-vs13` 临时日志：**已停止并清理**。
- 验证端口 `5090`、`5173`：**已释放**。

## Deferred

- AI Search。
- Semantic Search。
- Embedding。
- Vector Search。
- RAG。
- Dashboard。
- FTS5 / trigram 派生索引的运行时能力验证与启用；当前小规模 MVP 使用冻结允许的 LIKE fallback。
