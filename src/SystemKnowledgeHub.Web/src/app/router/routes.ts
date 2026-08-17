import type { RouteRecordRaw } from 'vue-router'
import FoundationView from '../../features/bootstrap/pages/FoundationView.vue'
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

export const routes: readonly RouteRecordRaw[] = [
  {
    path: '/',
    redirect: { name: 'database-objects-list' },
  },
  {
    path: '/foundation',
    name: 'foundation',
    component: FoundationView,
    meta: {
      title: '基础工程',
      layout: 'app-shell',
      navigationKey: 'dashboard',
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
    meta: { title: '业务规则详情', layout: 'app-shell', navigationKey: 'business-functions', hasContextRail: true },
  },
  {
    path: '/integrations/:id',
    name: 'integration-detail',
    component: IntegrationDetailView,
    meta: { title: '集成关系详情', layout: 'app-shell', navigationKey: 'systems', hasContextRail: true },
  },
  {
    path: '/unknown-items',
    name: 'unknown-items-list',
    component: UnknownItemsListView,
    meta: { title: '待确认事项', layout: 'app-shell', navigationKey: 'unknown-items', hasContextRail: false },
  },
  {
    path: '/unknown-items/:id',
    name: 'unknown-item-detail',
    component: UnknownItemDetailView,
    meta: { title: '待确认事项详情', layout: 'app-shell', navigationKey: 'unknown-items', hasContextRail: true },
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
