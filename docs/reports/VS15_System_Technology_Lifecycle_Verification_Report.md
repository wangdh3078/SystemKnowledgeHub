# VS15 — System Technology + Lifecycle Verification Report

状态：**VS15 PASS**  
范围：System Knowledge Hub MVP 最后一条功能型 Vertical Slice

## Implemented Use Cases

- C03 `UpdateSystemTechnology`：替换一个 System 的完整技术标签集合。
- C04 `UpdateSystemLifecycle`：显式更新一个 System 的生命周期。

## API

- `PUT /api/systems/{id}/technology`
- `PUT /api/systems/{id}/lifecycle`

两条路由均使用现有 opaque `concurrencyToken`；过期 token 返回 409。生命周期值相同返回 422 `business_rule_violation`，不伪造更新。

## Schema / Migration

- 未新增 Entity、表或 Migration。
- C03 复用 canonical `systems` 与 `system_technology_tags`；C04 复用 `systems.lifecycle`。

## Technology edit / Lifecycle edit

- 技术标签会去空白、禁止空值和不区分大小写的重复值，并在单一保存中替换集合。
- 生命周期仅接受冻结的 `SystemLifecycle` 枚举；`Retired` 继续代表退役，不提供物理删除。
- 两个编辑动作均位于 RP-03 的内联编辑状态；未新增 Route、Drawer、Wizard 或通用编辑器。

## Validation / Concurrency

- 完整集合、编辑人姓名、合法枚举和 opaque token 均经后端校验。
- C03/C04 成功后递增同一个 app-managed System version，并由详情刷新获取最新 token。
- 前端不会解析 token；409 显示重载路径，不做自动重试或静默合并。

## KnowledgeStatus side-effect check

- C03/C04 不更新 `KnowledgeStatus`、其原因或修改人快照。
- 两条聚焦测试分别验证技术/生命周期更新不会互相覆盖，也不会改变知识状态。

## Existing Evidence / Relationship / UnknownItem compatibility

- System Detail 刷新后，既有业务功能、数据库对象、集成关系、系统级 Context Rail 与待确认事项区域仍可读取。
- 本 Slice 不创建或修改 Evidence、KnowledgeRelation、UnknownItem、Resolution 或 KnowledgeUpdate。

## C32a compatibility

- 检查冻结 API 和既有 `KnowledgeResolutionService`：冻结 C32a 是 `ApplyColumnKnownValueUpdate`，并不存在名为 `UpdateSystem` 的 C32a。
- 因此未修改 Knowledge Resolution；C03/C04 继续只使用唯一 canonical System 实体、字段和版本列，不影响既有具体 Apply 用例。

## Focused tests

- 新增 2 条 SQLite API integration tests。
- 运行 `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --filter FullyQualifiedName~SystemsApiTests`：**6 passed**。

## Build / Type Check

- `dotnet build`：通过，0 warnings / 0 errors。
- `npm run type-check`：通过。
- `npm run build`：通过；仅有既有 Vite chunk-size 提示，非失败。

## Runtime Verification

在浏览器中实际完成：

`Systems List → MES → Edit Technology → 保存 .NET Framework 4.8 / Oracle / RabbitMQ → 刷新 → Edit Lifecycle → 保存维护中 → 刷新`

确认 SQLite 持久化、技术与生命周期互不覆盖、概览内容与 KnowledgeStatus=`Inferred` 保持不变，既有集成及 Context Rail 继续可见。

## Golden UI Review

- 复核 RP-03 和 ES-01：保持浅色桌面 Shell、系统级 Context Rail、紧凑信息密度和中文 UI。
- 技术与生命周期使用同一详情页的独立内联编辑动作；没有全页编辑或叠加 Drawer。

## Specification Deviation

无。任务说明中的“C32a `UpdateSystem`”与冻结 API 的名称不一致，已按冻结 API 处理并记录在上方兼容性说明中，不构成实现偏离。

## Process Cleanup

- 本轮 ASP.NET Core、Vite、浏览器验证标签和临时运行日志目录在报告完成前均已关闭/清理。
- 端口 `5090` 与 `5173` 已确认释放。

## Deferred

- Person / Role
- AI Search
- Semantic Search
- Embedding
- Vector Search
- RAG
- Final MVP Verification
- Architecture Review
