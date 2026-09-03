<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { EditPen, Plus, Search, Setting } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { formatDateTime } from '../../../app/formatters/dateTime'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import type { AccessLevel, UserSummary, UsersSort } from '../api/userContracts'
import { getUser, setUserActiveState } from '../api/usersApi'
import KnowledgeRoleManagementDialog from '../components/KnowledgeRoleManagementDialog.vue'
import UserManagementDrawer from '../components/UserManagementDrawer.vue'
import { useUsersManagement } from '../composables/useUsersManagement'

const actorStore = useActorStore()
const overlays = useOverlayStore()
const {
  keyword,
  isActive,
  sort,
  page,
  pageSize,
  loading,
  error,
  data,
  load,
  resetPageAndLoad,
  clearFilters,
} = useUsersManagement()
const activeActionId = ref<number | null>(null)
let keywordTimer: ReturnType<typeof setTimeout> | null = null

const accessLevelDescriptions: Readonly<Record<AccessLevel, string>> = {
  Viewer: '只读查看',
  Editor: '内容维护',
  Administrator: '系统管理',
}
const accessLevelLabels: Readonly<Record<AccessLevel, string>> = {
  Viewer: '查看者',
  Editor: '编辑者',
  Administrator: '管理员',
}

watch(keyword, () => {
  if (keywordTimer) clearTimeout(keywordTimer)
  keywordTimer = setTimeout(resetPageAndLoad, 280)
})

function openCreate(): void {
  overlays.openDrawer({ kind: 'user-management', id: null, mode: 'create' })
}

function openEdit(userId: number): void {
  overlays.openDrawer({ kind: 'user-management', id: userId, mode: 'edit' })
}

function openRoles(): void {
  overlays.openDialog({ kind: 'knowledge-role-management', id: null, mode: 'edit' })
}

function handleSortChange(change: {
  prop: string
  order: 'ascending' | 'descending' | null
}): void {
  const ascending = change.order === 'ascending'
  const next: UsersSort =
    change.prop === 'updatedAt'
      ? ascending
        ? 'updatedAt:asc'
        : 'updatedAt:desc'
      : ascending
        ? 'displayName:asc'
        : 'displayName:desc'
  sort.value = next
  resetPageAndLoad()
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage
  void load()
}

function handlePageSizeChange(nextPageSize: number): void {
  pageSize.value = nextPageSize
  resetPageAndLoad()
}

async function toggleUser(user: UserSummary): Promise<void> {
  const nextActive = !user.isActive
  const action = nextActive ? '启用' : '停用'
  let detail
  try {
    detail = await getUser(user.id)
  } catch (requestError: unknown) {
    ElMessage.error(
      requestError instanceof Error ? requestError.message : `读取用户资料失败，无法${action}。`,
    )
    return
  }

  try {
    await ElMessageBox.confirm(
      nextActive
        ? `确认启用“${user.displayName}”？启用后该用户可继续参与知识维护。`
        : `确认停用“${user.displayName}”？历史知识记录与角色映射会保留。`,
      `${action}用户`,
      {
        confirmButtonText: action,
        cancelButtonText: '取消',
        type: nextActive ? 'info' : 'warning',
      },
    )
  } catch {
    return
  }

  activeActionId.value = user.id
  try {
    await setUserActiveState(user.id, nextActive, detail.concurrencyToken, actorStore.actor)
    ElMessage.success(`用户已${action}。`)
    await load()
  } catch (requestError: unknown) {
    if (requestError instanceof ApiError && requestError.status === 409) {
      await ElMessageBox.alert(
        '该用户资料已被其他操作修改。系统未覆盖对方修改；请刷新列表后重试。',
        '并发修改冲突',
        { confirmButtonText: '刷新列表', type: 'warning' },
      )
      await load()
    } else {
      ElMessage.error(requestError instanceof Error ? requestError.message : `用户${action}失败。`)
    }
  } finally {
    activeActionId.value = null
  }
}

function handleRowClick(row: UserSummary): void {
  openEdit(row.id)
}

onMounted(() => void load())
</script>

<template>
  <main class="users-page skh-page">
    <header class="users-page__header skh-page-header">
      <div>
        <nav>管理 / 用户管理</nav>
        <h1>用户管理</h1>
        <p>维护人员资料、系统权限与知识身份；系统权限写操作由后端授权最终裁决。</p>
      </div>
      <div class="users-page__header-actions skh-page-header__actions">
        <el-button :icon="Setting" @click="openRoles">知识身份管理</el-button>
        <el-button class="skh-page-primary-action" type="primary" :icon="Plus" @click="openCreate"
          >新增用户</el-button
        >
      </div>
    </header>

    <section class="users-page__summary" aria-label="用户管理说明">
      <div>
        <span>人员资料</span><strong>{{ data?.total ?? '—' }}</strong
        ><small>当前筛选结果</small>
      </div>
      <p>
        <strong>启用 / 停用</strong
        ><span>用户与知识身份均通过明确的启用 / 停用操作维护，不提供物理删除。</span>
      </p>
    </section>

    <section class="users-filter-bar skh-filter-bar" aria-label="用户筛选">
      <el-input
        v-model="keyword"
        clearable
        :prefix-icon="Search"
        placeholder="搜索姓名、工号或邮箱"
        aria-label="搜索用户"
      />
      <el-select v-model="isActive" placeholder="用户状态：全部" @change="resetPageAndLoad">
        <el-option label="全部用户状态" value="" />
        <el-option label="用户启用" :value="true" />
        <el-option label="用户停用" :value="false" />
      </el-select>
      <el-button v-if="keyword || isActive !== ''" text type="primary" @click="clearFilters"
        >清除筛选</el-button
      >
      <span v-if="data">共 {{ data.total }} 位用户</span>
    </section>

    <LoadingState v-if="loading && !data" message="正在读取用户列表…" />
    <ErrorState
      v-else-if="error && !data"
      title="用户列表加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="users-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="data && data.items.length === 0"
        title="没有找到用户"
        description="调整筛选条件，或创建第一个可维护知识的用户。"
      />
      <el-table
        v-else
        :data="data?.items ?? []"
        row-key="id"
        class="users-table skh-data-table skh-data-table--comfortable"
        @row-click="handleRowClick"
        @sort-change="handleSortChange"
      >
        <el-table-column prop="displayName" label="姓名" min-width="120" sortable="custom"
          ><template #default="scope"
            ><button
              class="users-table__name skh-table-link"
              type="button"
              @click.stop="openEdit(scope.row.id)"
            >
              {{ scope.row.displayName }}
            </button></template
          ></el-table-column
        >
        <el-table-column prop="employeeNo" label="工号" min-width="105"
          ><template #default="scope"
            ><span :class="{ 'text-muted': !scope.row.employeeNo }" class="technical-text">{{
              scope.row.employeeNo ?? '未记录'
            }}</span></template
          ></el-table-column
        >
        <el-table-column prop="email" label="邮箱" min-width="180" show-overflow-tooltip
          ><template #default="scope"
            ><span :class="{ 'text-muted': !scope.row.email }">{{
              scope.row.email ?? '未记录'
            }}</span></template
          ></el-table-column
        >
        <el-table-column
          prop="departmentOrTeam"
          label="部门 / 团队"
          min-width="140"
          show-overflow-tooltip
          ><template #default="scope"
            ><span :class="{ 'text-muted': !scope.row.departmentOrTeam }">{{
              scope.row.departmentOrTeam ?? '未记录'
            }}</span></template
          ></el-table-column
        >
        <el-table-column prop="jobTitle" label="职位" min-width="130" show-overflow-tooltip
          ><template #default="scope"
            ><span :class="{ 'text-muted': !scope.row.jobTitle }">{{
              scope.row.jobTitle ?? '未记录'
            }}</span></template
          ></el-table-column
        >
        <el-table-column prop="accessLevel" label="系统权限" min-width="142"
          ><template #default="scope"
            ><div class="users-table__access-level">
              <strong>{{ accessLevelLabels[scope.row.accessLevel as AccessLevel] }}</strong
              ><small>{{ accessLevelDescriptions[scope.row.accessLevel as AccessLevel] }}</small>
            </div></template
          ></el-table-column
        >
        <el-table-column label="知识身份" min-width="210"
          ><template #default="scope"
            ><div v-if="scope.row.knowledgeRoles.length" class="users-table__roles">
              <el-tag
                v-for="role in scope.row.knowledgeRoles"
                :key="role.id"
                :type="role.isActive ? 'primary' : 'info'"
                effect="plain"
                size="small"
                >{{ role.name }}<template v-if="!role.isActive"> · 停用</template></el-tag
              >
            </div>
            <span v-else class="text-muted">未配置</span></template
          ></el-table-column
        >
        <el-table-column prop="isActive" label="用户状态" width="100" align="center"
          ><template #default="scope"
            ><el-tag :type="scope.row.isActive ? 'success' : 'info'" effect="plain" size="small">{{
              scope.row.isActive ? '用户启用' : '用户停用'
            }}</el-tag></template
          ></el-table-column
        >
        <el-table-column prop="updatedAt" label="更新于" width="156" sortable="custom"
          ><template #default="scope">{{
            formatDateTime(scope.row.updatedAt)
          }}</template></el-table-column
        >
        <el-table-column
          label="操作"
          width="164"
          fixed="right"
          class-name="users-table__actions-column"
          ><template #default="scope"
            ><div class="users-table__actions" @click.stop>
              <el-button text type="primary" :icon="EditPen" @click="openEdit(scope.row.id)"
                >编辑</el-button
              ><el-button
                text
                :type="scope.row.isActive ? 'danger' : 'success'"
                :loading="activeActionId === scope.row.id"
                @click="toggleUser(scope.row)"
                >{{ scope.row.isActive ? '停用' : '启用' }}</el-button
              >
            </div></template
          ></el-table-column
        >
      </el-table>

      <SkhPagination
        v-if="data"
        class="users-pagination"
        :total="data.total"
        :current-page="data.page"
        :page-size="data.pageSize"
        aria-label="用户列表分页"
        @current-change="handlePageChange"
        @size-change="handlePageSizeChange"
      />
      <p v-if="error && data" class="users-inline-error">刷新失败：{{ error }}</p>
    </section>

    <Teleport
      v-if="overlays.currentDrawer?.kind === 'user-management'"
      defer
      to="#drawer-feature-content"
    >
      <UserManagementDrawer :user-id="overlays.currentDrawer.id" @saved="load" />
    </Teleport>
    <Teleport
      v-if="overlays.currentDialog?.kind === 'knowledge-role-management'"
      defer
      to="#dialog-feature-content"
    >
      <KnowledgeRoleManagementDialog @changed="load" />
    </Teleport>
  </main>
</template>

<style src="../users.css"></style>
