import type { RouteRecordRaw } from 'vue-router'
import FoundationView from '../../features/bootstrap/pages/FoundationView.vue'
import DashboardView from '../../features/dashboard/pages/DashboardView.vue'
import BusinessFunctionDetailView from '../../features/business-functions/pages/BusinessFunctionDetailView.vue'
import BusinessFunctionsListView from '../../features/business-functions/pages/BusinessFunctionsListView.vue'
import DatabaseObjectDetailView from '../../features/database-knowledge/pages/DatabaseObjectDetailView.vue'
import DatabaseObjectsListView from '../../features/database-knowledge/pages/DatabaseObjectsListView.vue'
import SystemsListView from '../../features/systems/pages/SystemsListView.vue'
import SystemDetailView from '../../features/systems/pages/SystemDetailView.vue'
import NotFoundView from '../../features/bootstrap/pages/NotFoundView.vue'
import UnknownItemsListView from '../../features/unknown-items/pages/UnknownItemsListView.vue'
import UnknownItemDetailView from '../../features/unknown-items/pages/UnknownItemDetailView.vue'
import BusinessRuleDetailView from '../../features/business-rules/pages/BusinessRuleDetailView.vue'
import IntegrationDetailView from '../../features/integrations/pages/IntegrationDetailView.vue'
import UsersManagementView from '../../features/users/pages/UsersManagementView.vue'
import AccessForbiddenView from '../../features/bootstrap/pages/AccessForbiddenView.vue'
import KnowledgeDocumentsListView from '../../features/knowledge-documents/pages/KnowledgeDocumentsListView.vue'
import KnowledgeDocumentDetailView from '../../features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue'
import AdministratorAttachmentsView from '../../features/attachment-administration/pages/AdministratorAttachmentsView.vue'
import ConnectionProfilesView from '../../features/database-discovery/pages/ConnectionProfilesView.vue'
import DiscoveryRunsView from '../../features/database-discovery/pages/DiscoveryRunsView.vue'
import DiscoverySnapshotsView from '../../features/database-discovery/pages/DiscoverySnapshotsView.vue'
import DiscoverySnapshotView from '../../features/database-discovery/pages/DiscoverySnapshotView.vue'
import DiscoveryDifferencesView from '../../features/database-discovery/pages/DiscoveryDifferencesView.vue'
import DiscoveryDifferenceView from '../../features/database-discovery/pages/DiscoveryDifferenceView.vue'
import DiscoverySyncView from '../../features/database-discovery/pages/DiscoverySyncView.vue'
import PortalManagementView from '../../features/portal-management/pages/PortalManagementView.vue'
import PortalHomeView from '../../features/portal-reading/pages/PortalHomeView.vue'
import PortalPageView from '../../features/portal-reading/pages/PortalPageView.vue'
import PortalNotFoundView from '../../features/portal-reading/pages/PortalNotFoundView.vue'

export const routes: readonly RouteRecordRaw[] = [
  {
    path: '/',
    redirect: { name: 'dashboard' },
  },
  {
    path: '/portal',
    name: 'portal-home',
    component: PortalHomeView,
    meta: { title: '', layout: 'portal', navigationKey: null },
  },
  {
    path: '/portal/pages/:id',
    name: 'portal-page',
    component: PortalPageView,
    meta: { title: '知识页面', layout: 'portal', navigationKey: null },
  },
  {
    path: '/portal/:pathMatch(.*)*',
    name: 'portal-not-found',
    component: PortalNotFoundView,
    meta: { title: '页面未找到', layout: 'portal', navigationKey: null },
  },
  {
    path: '/dashboard',
    name: 'dashboard',
    component: DashboardView,
    meta: {
      title: '总览',
      layout: 'app-shell',
      navigationKey: 'dashboard',
      hasContextRail: false,
    },
  },
  {
    path: '/foundation',
    name: 'foundation',
    component: FoundationView,
    meta: {
      title: '基础工程',
      layout: 'app-shell',
      navigationKey: null,
    },
  },
  {
    path: '/systems',
    name: 'systems-list',
    component: SystemsListView,
    meta: {
      title: '系统',
      layout: 'app-shell',
      navigationKey: 'systems',
      hasContextRail: false,
    },
  },
  {
    path: '/systems/:id',
    name: 'system-detail',
    component: SystemDetailView,
    meta: {
      title: '系统详情',
      layout: 'app-shell',
      navigationKey: 'systems',
      hasContextRail: true,
    },
  },
  {
    path: '/business-functions',
    name: 'business-functions-list',
    component: BusinessFunctionsListView,
    meta: {
      title: '业务功能',
      layout: 'app-shell',
      navigationKey: 'business-functions',
      hasContextRail: false,
    },
  },
  {
    path: '/business-functions/:id',
    name: 'business-function-detail',
    component: BusinessFunctionDetailView,
    meta: {
      title: '业务功能详情',
      layout: 'app-shell',
      navigationKey: 'business-functions',
      hasContextRail: true,
    },
  },
  {
    path: '/business-rules/:id',
    name: 'business-rule-detail',
    component: BusinessRuleDetailView,
    meta: {
      title: '业务规则详情',
      layout: 'app-shell',
      navigationKey: 'business-functions',
      hasContextRail: true,
    },
  },
  {
    path: '/integrations/:id',
    name: 'integration-detail',
    component: IntegrationDetailView,
    meta: {
      title: '集成关系详情',
      layout: 'app-shell',
      navigationKey: 'systems',
      hasContextRail: true,
    },
  },
  {
    path: '/unknown-items',
    name: 'unknown-items-list',
    component: UnknownItemsListView,
    meta: {
      title: '待确认事项',
      layout: 'app-shell',
      navigationKey: 'unknown-items',
      hasContextRail: false,
    },
  },
  {
    path: '/unknown-items/:id',
    name: 'unknown-item-detail',
    component: UnknownItemDetailView,
    meta: {
      title: '待确认事项详情',
      layout: 'app-shell',
      navigationKey: 'unknown-items',
      hasContextRail: true,
    },
  },
  {
    path: '/database-objects',
    name: 'database-objects-list',
    component: DatabaseObjectsListView,
    meta: {
      title: '数据库对象',
      layout: 'app-shell',
      navigationKey: 'database',
      hasContextRail: false,
    },
  },
  {
    path: '/database/:id',
    name: 'database-object-detail',
    component: DatabaseObjectDetailView,
    meta: {
      title: '数据库对象详情',
      layout: 'app-shell',
      navigationKey: 'database',
      hasContextRail: true,
    },
  },
  {
    path: '/knowledge-documents',
    name: 'knowledge-documents-list',
    component: KnowledgeDocumentsListView,
    meta: {
      title: '知识内容',
      layout: 'app-shell',
      navigationKey: 'knowledge-documents',
      hasContextRail: false,
    },
  },
  {
    path: '/knowledge-documents/:id',
    name: 'knowledge-document-detail',
    component: KnowledgeDocumentDetailView,
    meta: {
      title: '知识内容详情',
      layout: 'app-shell',
      navigationKey: 'knowledge-documents',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/connections',
    alias: ['/database-discovery', '/admin/database-discovery/connections'],
    name: 'database-discovery-connections',
    component: ConnectionProfilesView,
    meta: {
      title: '数据库连接配置',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
      minimumAccessLevel: 'Administrator',
    },
  },
  {
    path: '/database-discovery/runs',
    name: 'database-discovery-runs',
    component: DiscoveryRunsView,
    meta: {
      title: '发现运行',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/snapshots',
    name: 'database-discovery-snapshots',
    component: DiscoverySnapshotsView,
    meta: {
      title: '发现快照',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/snapshots/:id',
    name: 'database-discovery-snapshot',
    component: DiscoverySnapshotView,
    meta: {
      title: '发现快照',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/differences',
    name: 'database-discovery-differences',
    component: DiscoveryDifferencesView,
    meta: {
      title: '差异审查',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/differences/:id',
    name: 'database-discovery-difference',
    component: DiscoveryDifferenceView,
    meta: {
      title: '差异审查',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/database-discovery/sync',
    name: 'database-discovery-sync',
    component: DiscoverySyncView,
    meta: {
      title: '手工同步',
      layout: 'app-shell',
      navigationKey: 'database-discovery',
      hasContextRail: false,
    },
  },
  {
    path: '/portal-management',
    name: 'portal-management',
    component: PortalManagementView,
    meta: {
      title: '知识门户管理',
      layout: 'app-shell',
      navigationKey: 'portal-management',
      hasContextRail: false,
      minimumAccessLevel: 'Administrator',
    },
  },
  {
    path: '/admin/users',
    name: 'users-management',
    component: UsersManagementView,
    meta: {
      title: '用户管理',
      layout: 'app-shell',
      navigationKey: 'users',
      hasContextRail: false,
      minimumAccessLevel: 'Administrator',
    },
  },
  {
    path: '/admin/attachments',
    name: 'attachment-administration',
    component: AdministratorAttachmentsView,
    meta: {
      title: '附件管理',
      layout: 'app-shell',
      navigationKey: 'attachments',
      hasContextRail: false,
      minimumAccessLevel: 'Administrator',
    },
  },
  {
    path: '/forbidden',
    name: 'access-forbidden',
    component: AccessForbiddenView,
    meta: { title: '没有访问权限', layout: 'app-shell', navigationKey: null },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: NotFoundView,
    meta: {
      title: '页面未找到',
      layout: 'app-shell',
      navigationKey: null,
    },
  },
]
