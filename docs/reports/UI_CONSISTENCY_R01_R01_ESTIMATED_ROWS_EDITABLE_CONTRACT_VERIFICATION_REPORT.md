# UI-CONSISTENCY-R01-R01 DatabaseObject EstimatedRows 可编辑契约修正验证报告

## 结论

`UI-CONSISTENCY-R01-R01`：**PASS**。

本收口任务以产品明确批准的 amendment 最小修订冻结 C11，使 `DatabaseObject.EstimatedRows` 成为 Database Knowledge 的人工维护字段。数据库对象编辑抽屉现在可在既有 C11 `PUT /api/database-objects/{id}/knowledge` 中保存、清空估算行数；后端严格验证 nullable JavaScript safe integer，持久化后返回新值和新 opaque concurrency token。Database Discovery / Manual Sync 仍只维护技术结构，不取得 `EstimatedRows` ownership。

历史报告 `UI_CONSISTENCY_R01_GLOBAL_PAGINATION_CHINESE_UX_VERIFICATION_REPORT.md` 保持原 **FAIL** 状态和原始证据不变；本报告是其唯一失败项的后续收口记录。

## C11 Contract Amendment

`docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md` 新增了带日期、任务号和修订原因的显式 amendment，并同步 C11 表格与请求示例。完整 PUT Section 现在正式包含：

- `businessDescription`
- `estimatedRows`
- `accessMode`
- `businessKeyColumns`
- `actor`
- `concurrencyToken`

`estimatedRows` 表示“数据库对象当前人工维护的估算行数”；`null` 表示未维护。它不是实时统计、自动采集、Discovery 同步字段、KnowledgeStatus 或 Evidence。未新增 endpoint、generic PATCH 或 EF migration。

## Backend Implementation

- C11 request boundary 对原始 JSON `estimatedRows` 做严格类型和范围解析，保证负数、小数、数字字符串及超过 `9007199254740991` 的值统一返回 `400 validation_error` 与 `fieldErrors.estimatedRows`，不静默截断或转换。
- command/use case 使用 `long?`，service 进行第二层范围校验并写入现有 `DatabaseObject.EstimatedRows` mapping。
- C11 response projection 返回最新 `estimatedRows`、保持不变的 KnowledgeStatus 和新 concurrency token。
- 既有 opaque token 语义保持；旧 token 返回 `409 conflict`。
- 现有实体、列映射和数据库列已经存在，没有模型或 schema 变更。

## Frontend Implementation

- 数据库对象编辑抽屉保留 label“估算行数（可选）”，改为无 spinner 的可编辑数字输入。
- 初始显示当前值；清空发送 `null`；保存请求总是包含正式字段 `estimatedRows`。
- 前端在发送前拒绝负数、小数和非 safe integer，并保持原始输入，不做截断或取整。
- API 返回字段错误时显示 `estimatedRows` 错误；其他失败及并发冲突继续使用既有提示，并保留用户输入、不关闭抽屉。
- 成功后继续触发既有 `database-object:changed` 刷新链路；对象详情立即重新读取并显示新值。

## Discovery Ownership Protection

B04 Apply allowlist 未增加 `EstimatedRows`：`CreateObject` 未设置该属性，因此 Discovery 新增对象保持 `null`；`ApplyObjectStructure` 仅更新 ObjectType、DatabaseComment、PrimaryKeyColumns、TechnicalIdentity 及更新时间/版本，不读取或写入 `EstimatedRows`。

回归测试证明：已有对象的 `EstimatedRows = 48000` 在 DatabaseComment、PrimaryKey、字段类型/顺序等结构变化并 Apply 后仍为 `48000`；显式 Link Existing 同样不覆盖；新建 Discovery Object 初始为 `null`。

## Verification

| 检查 | 结果 |
| --- | --- |
| Database Knowledge authoring focused tests | PASS — 12/12 |
| B04 human-knowledge-protection focused regression | PASS — 2/2 |
| Backend Release build | PASS — 0 warnings / 0 errors |
| EF `has-pending-model-changes` | PASS — `No changes have been made to the model since the last migration.` |
| Frontend Database Object detail/edit/API focused tests | PASS — 3 files / 13 tests |
| UI-CONSISTENCY affected frontend tests | PASS — 26 files / 134 tests |
| `npm run type-check` | PASS |
| `npm run lint` | PASS |
| affected Prettier formatting | PASS |
| `npm run build` | PASS；仅保留既有 chunk-size advisory |
| `git diff --check` | PASS |

EF 检查使用绝对、任务专属设计时 SQLite 路径；命令没有创建该文件。测试使用测试工厂隔离数据，没有启动持久运行时或留下验证进程。仓库数据库 `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` 在验证前后均为 1,257,472 bytes，SHA-256 均为 `7235E95889845F72C52FFD9C685FA99B5BF62582AA4BA03057128265CAFE4EC2`；未出现 WAL/SHM 文件，仓库持久数据未被修改。

EF 命令仍打印既有 required-navigation/global-query-filter advisory；它没有形成 pending model change，也不是本任务新增问题。

## Final Status

```text
UI-CONSISTENCY-R01-R01 PASS

C11 CONTRACT AMENDMENT: PASS
DATABASE OBJECT ESTIMATED ROWS EDIT: PASS
NULL / INTEGER VALIDATION: PASS
CONCURRENCY: PASS
DISCOVERY OWNERSHIP PROTECTION: PASS
FRONTEND REGRESSION: PASS
BACKEND REGRESSION: PASS

UI-CONSISTENCY-R01 COMPLETE: YES
```

## Cleanup and Scope

未创建 migration、数据库、WAL/SHM、临时服务或残留验证进程；未执行整个 Database Discovery Final Verification，也未开始其他 UI 重构。
