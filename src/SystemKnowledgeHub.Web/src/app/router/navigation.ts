import type { Component } from 'vue'
import { Coin, DataBoard, Files, Grid, QuestionFilled } from '@element-plus/icons-vue'

export type NavigationKey =
  'dashboard' | 'systems' | 'business-functions' | 'database' | 'unknown-items'

export interface NavigationItem {
  readonly key: NavigationKey
  readonly label: string
  readonly icon: Component
  readonly enabled: boolean
  readonly routeName?: 'foundation' | 'systems-list' | 'business-functions-list' | 'database-object-detail' | 'unknown-items-list'
}

export const navigationItems: readonly NavigationItem[] = [
  { key: 'dashboard', label: '总览', icon: DataBoard, enabled: true, routeName: 'foundation' },
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
    routeName: 'database-object-detail',
  },
  {
    key: 'unknown-items',
    label: '待确认事项',
    icon: QuestionFilled,
    enabled: true,
    routeName: 'unknown-items-list',
  },
]
