import type { Component } from 'vue'
import {
  Coin,
  DataBoard,
  Document,
  Files,
  Grid,
  Paperclip,
  Search,
  QuestionFilled,
  UserFilled,
} from '@element-plus/icons-vue'

export type NavigationKey =
  | 'dashboard'
  | 'systems'
  | 'business-functions'
  | 'database'
  | 'knowledge-documents'
  | 'unknown-items'
  | 'users'
  | 'attachments'
  | 'database-discovery'

export interface NavigationItem {
  readonly key: NavigationKey
  readonly label: string
  readonly icon: Component
  readonly enabled: boolean
  readonly groupLabel?: string
  readonly routeName?:
    | 'dashboard'
    | 'systems-list'
    | 'business-functions-list'
    | 'database-objects-list'
    | 'knowledge-documents-list'
    | 'unknown-items-list'
    | 'users-management'
    | 'attachment-administration'
    | 'database-discovery-runs'
  readonly minimumAccessLevel?: 'Administrator'
}

export const navigationItems: readonly NavigationItem[] = [
  { key: 'dashboard', label: '总览', icon: DataBoard, enabled: true, routeName: 'dashboard' },
  { key: 'systems', label: '系统', icon: Grid, enabled: true, routeName: 'systems-list' },
  {
    key: 'business-functions',
    label: '业务功能',
    icon: Files,
    enabled: true,
    routeName: 'business-functions-list',
  },
  {
    key: 'database',
    label: '数据库',
    icon: Coin,
    enabled: true,
    routeName: 'database-objects-list',
  },
  {
    key: 'database-discovery',
    label: '数据库发现',
    icon: Search,
    enabled: true,
    routeName: 'database-discovery-runs',
  },
  {
    key: 'knowledge-documents',
    label: '知识内容',
    icon: Document,
    enabled: true,
    routeName: 'knowledge-documents-list',
  },
  {
    key: 'unknown-items',
    label: '待确认事项',
    icon: QuestionFilled,
    enabled: true,
    routeName: 'unknown-items-list',
  },
  {
    key: 'users',
    label: '用户管理',
    icon: UserFilled,
    enabled: true,
    groupLabel: '管理',
    routeName: 'users-management',
    minimumAccessLevel: 'Administrator',
  },
  {
    key: 'attachments',
    label: '附件管理',
    icon: Paperclip,
    enabled: true,
    routeName: 'attachment-administration',
    minimumAccessLevel: 'Administrator',
  },
]
