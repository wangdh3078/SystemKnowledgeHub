import 'vue-router'
import type { NavigationKey } from '../app/router/navigation'

export {}

declare module 'vue-router' {
  interface RouteMeta {
    readonly title: string
    readonly layout: 'app-shell' | 'plain' | 'portal'
    readonly navigationKey: NavigationKey | null
    readonly hasContextRail?: boolean
    readonly minimumAccessLevel?: 'Administrator'
  }
}
