import {
  ElAlert,
  ElButton,
  ElCheckbox,
  ElCollapse,
  ElCollapseItem,
  ElConfigProvider,
  ElDatePicker,
  ElDialog,
  ElDivider,
  ElDrawer,
  ElForm,
  ElFormItem,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElSwitch,
  ElTag,
  ElTooltip,
} from 'element-plus'
import 'element-plus/es/components/button/style/css'
import 'element-plus/es/components/alert/style/css'
import 'element-plus/es/components/checkbox/style/css'
import 'element-plus/es/components/collapse/style/css'
import 'element-plus/es/components/collapse-item/style/css'
import 'element-plus/es/components/config-provider/style/css'
import 'element-plus/es/components/date-picker/style/css'
import 'element-plus/es/components/drawer/style/css'
import 'element-plus/es/components/dialog/style/css'
import 'element-plus/es/components/divider/style/css'
import 'element-plus/es/components/form/style/css'
import 'element-plus/es/components/form-item/style/css'
import 'element-plus/es/components/icon/style/css'
import 'element-plus/es/components/input/style/css'
import 'element-plus/es/components/input-number/style/css'
import 'element-plus/es/components/message/style/css'
import 'element-plus/es/components/message-box/style/css'
import 'element-plus/es/components/option/style/css'
import 'element-plus/es/components/pagination/style/css'
import 'element-plus/es/components/radio-button/style/css'
import 'element-plus/es/components/radio-group/style/css'
import 'element-plus/es/components/select/style/css'
import 'element-plus/es/components/table/style/css'
import 'element-plus/es/components/table-column/style/css'
import 'element-plus/es/components/switch/style/css'
import 'element-plus/es/components/tag/style/css'
import 'element-plus/es/components/tooltip/style/css'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import App from '../../App.vue'
import router from '../router'
import '../../styles/tokens.css'
import '../../styles/typography.css'
import '../../styles/element-plus-overrides.css'
import '../../styles/app.css'
import { setApiAntiforgeryTokenProvider, setApiSecurityErrorHandler } from '../../api/client/apiClient'
import { useActorStore } from '../stores/actor'

export async function bootstrapApp(): Promise<void> {
  const app = createApp(App)

  app.config.errorHandler = (error, instance, info) => {
    console.error('应用发生未处理错误。', { error, instance, info })
  }

  const pinia = createPinia()
  app.use(pinia)
  const actorStore = useActorStore(pinia)
  setApiAntiforgeryTokenProvider(() => actorStore.antiforgeryToken)
  setApiSecurityErrorHandler((error, path) => {
    if (path !== '/current-user') actorStore.handleSecurityError(error)
  })
  await actorStore.initialize()
  app.use(router)
  app.use(ElAlert)
  app.use(ElButton)
  app.use(ElCheckbox)
  app.use(ElCollapse)
  app.use(ElCollapseItem)
  app.use(ElConfigProvider)
  app.use(ElDatePicker)
  app.use(ElDrawer)
  app.use(ElDialog)
  app.use(ElDivider)
  app.use(ElForm)
  app.use(ElFormItem)
  app.use(ElIcon)
  app.use(ElInput)
  app.use(ElInputNumber)
  app.use(ElOption)
  app.use(ElPagination)
  app.use(ElRadioButton)
  app.use(ElRadioGroup)
  app.use(ElSelect)
  app.use(ElTable)
  app.use(ElTableColumn)
  app.use(ElSwitch)
  app.use(ElTag)
  app.use(ElTooltip)
  await router.isReady()
  app.mount('#app')
}
