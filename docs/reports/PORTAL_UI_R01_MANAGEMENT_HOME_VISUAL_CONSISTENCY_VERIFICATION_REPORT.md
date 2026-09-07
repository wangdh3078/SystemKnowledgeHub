# PORTAL-UI-R01 — Portal Management / Home Visual Consistency

**PORTAL-UI-R01 PASS** — 2026-09-07，基线 `51b50064d05466c3ba376cc79fa3e722b5d843df`。

管理页收紧页面树、页面库、标题、主知识对象、发布检查、章节和保存区。目录/页面增加现有图标区分；主知识对象显示现有状态。保存区状态文字选择器改为直属 span，避免覆盖按钮内部文字；启用态复用系统主按钮，禁用态使用页面内的现有文字/背景 token。

首页移除非必要英文标签、缩小标题和留白；无内容时只有一个主空态，侧栏显示“暂无目录”。最近发布改为标题、类型/目录上下文和日期列表。保留现有唯一搜索入口及查询交互。

验证：相关 Management surface、Home、Tree、PortalLayout 测试 **12/12 PASS**；`npm run type-check`、`npm run build`、修改文件 ESLint/Prettier、`git diff --check` PASS。构建只有既有 chunk-size 提示。更新 Home/Tree 空态与日期断言，PortalLayout 将旧的整段 header 文本断言收窄到品牌并验证单一搜索入口。

默认桌面浏览器检查使用真实页面组件、现有样式/API decoder 和临时前端 fixture：管理页标题/树/库/发布提示/章节可读，章节下移后保存按钮启用，保存后回到禁用和“所有修改已保存”；启用文字白色、禁用文字 `rgb(52,64,84)`，无内部 span 错误覆盖。首页统一空态、两条 recent 内容及日期、搜索提交后的查询路由和结果展示正常。此检查验证前端显示与交互，不作为真实后端发布或搜索验收。

未修改后端、API、权限、匿名边界、publication、投影、数据模型或迁移。未启动后端、未打开任何 SQLite；临时 fixture/Vite 已停止并清理。既有 HC-B01 和 DBDISC 修改保留且排除在本任务提交之外。

| Final gate | Result |
| --- | --- |
| PORTAL MANAGEMENT VISUAL HIERARCHY | PASS |
| SAVE COMPOSITION BUTTON CONSISTENCY | PASS |
| DISABLED TEXT READABILITY | PASS |
| SECTION LIST READABILITY | PASS |
| PORTAL HOME VISUAL HIERARCHY | PASS |
| PORTAL EMPTY STATE | PASS |
| PORTAL RECENT LIST | PASS |
| PORTAL SEARCH UI REGRESSION | PASS |
| FOCUSED FRONTEND TESTS | PASS |
| TYPE CHECK | PASS |
| BUILD | PASS |
| DEFAULT BROWSER CHECK | PASS |
| NO FUNCTIONAL CONTRACT CHANGE | PASS |

**PORTAL-UI-R01 COMPLETE**。交付结果以提交/push 输出为准。
