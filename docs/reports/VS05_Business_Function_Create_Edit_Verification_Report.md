# VS-05 — Business Function Create + Edit Verification Report

## Result

**VS05 PASS**

## Implemented scope

- `C05 CreateBusinessFunction`：在明确 System Context 下最小创建业务功能；初始 `KnowledgeStatus = Unknown`。
- `C06 UpdateBusinessFunctionOverview`：在 Business Function Detail 中以内联编辑更新名称、类型、用途、调用方、输入、输出与改写状态。
- `C07 ReplaceBusinessProcessSteps`：以一个完整、有序集合新增、编辑、删除和移动简单业务流程步骤。
- 创建成功后关闭创建界面、刷新列表并立即定位新对象；保存概览/流程后刷新当前详情。
- 未实现 Evidence、Relationship、UnknownItem、KnowledgeStatus 推进或删除。

## Schema / Migration

- **Schema change: No**。
- 复用 VS-04 已落地的 canonical `business_functions` 与 `business_process_steps`。
- 未新增 Migration；继续使用 app-managed integer `version`，HTTP 中只暴露 opaque `concurrencyToken`。

## Canonical API

- `POST /api/business-functions`
- `PUT /api/business-functions/{id}/overview`
- `PUT /api/business-functions/{id}/process-steps`

未增加第二套路由。创建返回 `201`；两类更新返回新的 opaque token；旧 token 返回 frozen error contract 的 `409 conflict`。

## Main files

- Backend：`BusinessFunctionService.cs`、`BusinessFunctionWriteModels.cs`、`BusinessFunctionRequests.cs`、`BusinessFunctionsController.cs`。
- Frontend：`CreateBusinessFunctionFlow.vue`、`CreateBusinessFunctionDialog.vue`、`BusinessFunctionOverviewSection.vue`、`BusinessProcessSection.vue`、`useBusinessFunctionDetail.ts`。
- Existing list/detail/API contracts were extended in place; no Repository、Handler、Mapper、CQRS 或 generic authoring framework was added.

## Focused tests

Command:

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter FullyQualifiedName~BusinessFunctionsApiTests
```

Result: **PASS — 4/4**。

新增 2 个高价值测试：

1. C05 写入真实 SQLite、返回 `Unknown`，并可立即由 Q06 列表读取。
2. C06/C07 持久化概览和有序流程；token 随保存推进；旧 token 返回 409。

## Build / static verification

- `dotnet build SystemKnowledgeHub.sln --no-restore` — **PASS**, 0 warnings / 0 errors。
- `npm run type-check` — **PASS**。
- `npm run lint` — **PASS**。
- `npm run build` — **PASS**；只有既有 Vite chunk size 提示，无构建错误。
- Frontend 没有新增自动化测试：本 Slice 的表单/排序交互通过真实浏览器链路验证，未引入新的复杂前端算法或测试框架。

## Runtime verification

Headless Microsoft Edge 对实际 Vue UI 执行了一次完整链路：

```text
业务功能列表
→ 全局“新增”
→ 选择业务功能
→ 填写最小信息并创建
→ 新对象出现在列表
→ 进入 Business Function Detail
→ 编辑并保存概览
→ 新增、填写并保存 3 个业务流程步骤
→ 刷新页面
→ 概览和有序流程仍从 SQLite 正确读取
```

运行结果：创建 UI、概览编辑器和流程编辑器均在成功后关闭；最终路由为新对象的 `/business-functions/{id}`，刷新后 3 个步骤保持顺序。另对同一对象以旧 token 重复提交概览，实测第一次 `200`、第二次 `409`，错误文案提示重新加载且没有自动重试。

## Golden UI review

- Golden：`OV-05` 最小创建模式、`ES-02` Business Function Inline Edit；RP-04/RP-05 继续作为 Shell、列表和详情布局基线。
- 1920×1080 与 1440×900 均完成浏览器截图检查。
- 新增保持单 Dialog、最小信息、创建后“未知”和渐进补充说明。
- 概览编辑保留在 Main Content 原 Section；业务流程保持简单步骤表达；Context Rail 未复制编辑内容。
- 组合对照和结论记录于仓库根目录 `design-qa.md`，最终结果为 `passed`。

## Specification deviation

**None.**

Q07 read contract 不返回 `displayName`，因此 ES-02 当前概览表单不暴露该字段；C06 请求中的 `displayName = null` 按“未编辑此字段”处理并保留已有值，避免只读 Projection 导致静默清空。该处理未改变 frozen route、request/response shape 或数据库模型。

## Process cleanup

- 验证使用 API `5190`、Vite `5191`；完成后已停止本任务启动的 ASP.NET Core、Vite 和临时 Headless Edge 进程。
- 已确认验证端口释放。
- 临时 runtime harness / Vite config 已删除，不属于产品源码。

## Deferred

- Business Function Evidence、Relationship、UnknownItem、KnowledgeStatus change。
- Business Function 与 Process 之外的编辑 Section。
- 下一条 Vertical Slice；本任务未开始 VS-06。
