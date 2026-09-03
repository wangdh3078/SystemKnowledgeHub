/* eslint-disable vue/one-component-per-file -- Shared lightweight stubs keep page tests focused. */
import {
  computed,
  defineComponent,
  h,
  inject,
  provide,
  type ComputedRef,
  type InjectionKey,
  type PropType,
} from 'vue'

type TableRow = Record<string, unknown>

interface RadioGroupContext {
  readonly modelValue: ComputedRef<unknown>
  readonly select: (value: unknown) => void
}

interface SelectContext {
  readonly select: (value: unknown) => void
}

interface DropdownContext {
  readonly command: (value: string) => void
}

const tableRowsKey: InjectionKey<ComputedRef<readonly TableRow[]>> = Symbol('discovery-table-rows')
const radioGroupKey: InjectionKey<RadioGroupContext> = Symbol('discovery-radio-group')
const selectKey: InjectionKey<SelectContext> = Symbol('discovery-select')
const dropdownKey: InjectionKey<DropdownContext> = Symbol('discovery-dropdown')

const ElTable = defineComponent({
  name: 'ElTable',
  props: {
    data: {
      type: Array as PropType<readonly TableRow[]>,
      default: () => [],
    },
  },
  setup(props, { slots }) {
    provide(
      tableRowsKey,
      computed(() => props.data),
    )
    return () => h('div', { class: 'el-table-stub' }, slots.default?.())
  },
})

const ElTableColumn = defineComponent({
  name: 'ElTableColumn',
  props: {
    prop: { type: String, default: '' },
    label: { type: String, default: '' },
  },
  setup(props, { slots }) {
    const rows = inject(
      tableRowsKey,
      computed<readonly TableRow[]>(() => []),
    )
    return () =>
      h(
        'div',
        { class: 'el-table-column-stub', 'data-label': props.label },
        rows.value.map((row) =>
          slots.default
            ? slots.default({ row })
            : h('span', String(props.prop ? (row[props.prop] ?? '') : '')),
        ),
      )
  },
})

const ElButton = defineComponent({
  name: 'ElButton',
  props: { disabled: Boolean },
  emits: ['click'],
  setup(props, { emit, slots }) {
    return () =>
      h(
        'button',
        { type: 'button', disabled: props.disabled, onClick: () => emit('click') },
        slots.default?.(),
      )
  },
})

const ElDrawer = defineComponent({
  name: 'ElDrawer',
  props: { modelValue: Boolean, title: { type: String, default: '' } },
  emits: ['update:modelValue'],
  setup(props, { slots }) {
    return () =>
      props.modelValue
        ? h('section', { class: 'el-drawer-stub' }, [h('h2', props.title), slots.default?.()])
        : null
  },
})

const passthrough = (name: string, tag = 'div') =>
  defineComponent({
    name,
    setup(_, { slots }) {
      return () => h(tag, slots.default?.())
    },
  })

const ElAlert = defineComponent({
  name: 'ElAlert',
  props: { title: { type: String, default: '' } },
  setup(props, { slots }) {
    return () => h('aside', [h('strong', props.title), slots.default?.()])
  },
})

const ElFormItem = defineComponent({
  name: 'ElFormItem',
  props: {
    label: { type: String, default: '' },
    error: { type: String, default: '' },
  },
  setup(props, { slots }) {
    return () =>
      h('label', { class: 'el-form-item-stub', 'data-label': props.label }, [
        h('span', props.label),
        slots.default?.(),
        props.error ? h('span', { role: 'alert' }, props.error) : null,
      ])
  },
})

const ElInput = defineComponent({
  name: 'ElInput',
  inheritAttrs: false,
  props: {
    modelValue: { type: [String, Number], default: '' },
    type: { type: String, default: 'text' },
  },
  emits: ['update:modelValue', 'input'],
  setup(props, { attrs, emit }) {
    return () => {
      const tag = props.type === 'textarea' ? 'textarea' : 'input'
      return h(tag, {
        ...attrs,
        type: tag === 'input' ? props.type : undefined,
        value: props.modelValue,
        onInput: (event: Event) => {
          const value = (event.target as HTMLInputElement).value
          emit('update:modelValue', value)
          emit('input', value)
        },
      })
    }
  },
})

const ElInputNumber = defineComponent({
  name: 'ElInputNumber',
  props: { modelValue: { type: Number, default: 0 } },
  emits: ['update:modelValue', 'change'],
  setup(props, { emit }) {
    return () =>
      h('input', {
        type: 'number',
        value: props.modelValue,
        onInput: (event: Event) => {
          const value = Number((event.target as HTMLInputElement).value)
          emit('update:modelValue', value)
          emit('change', value)
        },
      })
  },
})

const ElSwitch = defineComponent({
  name: 'ElSwitch',
  props: { modelValue: Boolean, disabled: Boolean },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () =>
      h('input', {
        type: 'checkbox',
        checked: props.modelValue,
        disabled: props.disabled,
        onChange: (event: Event) =>
          emit('update:modelValue', (event.target as HTMLInputElement).checked),
      })
  },
})

const ElCheckbox = defineComponent({
  name: 'ElCheckbox',
  inheritAttrs: false,
  props: { modelValue: Boolean, disabled: Boolean, indeterminate: Boolean },
  emits: ['update:modelValue', 'change'],
  setup(props, { attrs, emit, slots }) {
    return () =>
      h('label', [
        h('input', {
          ...attrs,
          type: 'checkbox',
          checked: props.modelValue,
          disabled: props.disabled,
          'data-indeterminate': String(props.indeterminate),
          onChange: (event: Event) => {
            const checked = (event.target as HTMLInputElement).checked
            emit('update:modelValue', checked)
            emit('change', checked)
          },
        }),
        slots.default?.(),
      ])
  },
})

const ElSelect = defineComponent({
  name: 'ElSelect',
  props: { modelValue: { type: [String, Number], default: '' } },
  emits: ['update:modelValue', 'change'],
  setup(_, { emit, slots }) {
    provide(selectKey, {
      select: (value) => {
        emit('update:modelValue', value)
        emit('change', value)
      },
    })
    return () => h('div', { class: 'el-select-stub' }, slots.default?.())
  },
})

const ElOption = defineComponent({
  name: 'ElOption',
  props: {
    value: { type: [String, Number], required: true },
    label: { type: String, default: '' },
  },
  setup(props, { slots }) {
    const select = inject(selectKey)
    return () =>
      h(
        'button',
        {
          type: 'button',
          'data-option-value': String(props.value),
          onClick: () => select?.select(props.value),
        },
        slots.default?.() ?? props.label,
      )
  },
})

const ElRadioGroup = defineComponent({
  name: 'ElRadioGroup',
  props: { modelValue: { type: [String, Number], default: '' } },
  emits: ['update:modelValue', 'change'],
  setup(props, { emit, slots }) {
    provide(radioGroupKey, {
      modelValue: computed(() => props.modelValue),
      select: (value) => {
        emit('update:modelValue', value)
        emit('change', value)
      },
    })
    return () => h('div', { role: 'radiogroup' }, slots.default?.())
  },
})

const ElRadioButton = defineComponent({
  name: 'ElRadioButton',
  props: { value: { type: [String, Number], required: true } },
  setup(props, { slots }) {
    const group = inject(radioGroupKey)
    return () =>
      h(
        'button',
        {
          type: 'button',
          'data-radio-value': String(props.value),
          'aria-pressed': group?.modelValue.value === props.value,
          onClick: () => group?.select(props.value),
        },
        slots.default?.(),
      )
  },
})

const ElDropdown = defineComponent({
  name: 'ElDropdown',
  emits: ['command'],
  setup(_, { emit, slots }) {
    provide(dropdownKey, { command: (value) => emit('command', value) })
    return () =>
      h('div', { class: 'el-dropdown-stub' }, [
        h('div', { class: 'el-dropdown-trigger-stub' }, slots.default?.()),
        h('div', { class: 'el-dropdown-menu-stub' }, slots.dropdown?.()),
      ])
  },
})

const ElDropdownItem = defineComponent({
  name: 'ElDropdownItem',
  props: { command: { type: String, required: true } },
  setup(props, { slots }) {
    const dropdown = inject(dropdownKey)
    return () =>
      h(
        'button',
        {
          type: 'button',
          'data-dropdown-command': props.command,
          onClick: () => dropdown?.command(props.command),
        },
        slots.default?.(),
      )
  },
})

const ElPagination = defineComponent({
  name: 'ElPagination',
  props: {
    currentPage: { type: Number, default: 1 },
    total: { type: Number, default: 0 },
    pageSize: { type: Number, default: 20 },
    pageSizes: { type: Array as PropType<number[]>, default: () => [] },
    layout: { type: String, default: '' },
  },
  emits: ['update:currentPage', 'update:pageSize', 'current-change', 'size-change'],
  setup(props, { emit }) {
    return () =>
      h('div', { 'data-pagination-layout': props.layout }, [
        ...props.pageSizes.map((size) =>
          h(
            'button',
            {
              type: 'button',
              'data-page-size': String(size),
              onClick: () => {
                emit('update:pageSize', size)
                emit('size-change', size)
              },
            },
            `每页 ${size}`,
          ),
        ),
        h(
          'button',
          {
            type: 'button',
            'data-pagination-next': '',
            disabled: props.currentPage * props.pageSize >= props.total,
            onClick: () => {
              const next = props.currentPage + 1
              emit('update:currentPage', next)
              emit('current-change', next)
            },
          },
          '下一页',
        ),
      ])
  },
})

export const discoveryPageStubs = {
  DiscoverySectionNav: passthrough('DiscoverySectionNav', 'nav'),
  RouterLink: passthrough('RouterLink', 'a'),
  ElAlert,
  ElButton,
  ElCheckbox,
  ElCollapse: passthrough('ElCollapse'),
  ElCollapseItem: passthrough('ElCollapseItem'),
  ElDrawer,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu: passthrough('ElDropdownMenu'),
  ElForm: passthrough('ElForm', 'form'),
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElIcon: passthrough('ElIcon', 'span'),
  ElOption,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElResult: passthrough('ElResult'),
  ElSelect,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag: passthrough('ElTag', 'span'),
  ElTooltip: passthrough('ElTooltip', 'span'),
}
