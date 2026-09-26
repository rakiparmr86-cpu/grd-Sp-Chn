import type { KeyboardEvent } from 'react'

export interface ScreenTab<T extends string> {
  id: T
  label: string
  count?: number
}

interface ScreenTabsProps<T extends string> {
  tabs: ScreenTab<T>[]
  active: T
  onChange: (id: T) => void
  // Prefix for element ids; the caller renders the panel as `${idPrefix}-panel`.
  idPrefix: string
  label: string
}

// One section of a screen at a time; used wherever a screen has several stacked lists.
export function ScreenTabs<T extends string>({ tabs, active, onChange, idPrefix, label }: ScreenTabsProps<T>) {
  function moveWithArrows(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return
    const index = tabs.findIndex((tab) => tab.id === active)
    const next = tabs[(index + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length]
    onChange(next.id)
    document.getElementById(`${idPrefix}-tab-${next.id}`)?.focus()
  }

  return (
    <div className="screen-tabs" role="tablist" aria-label={label} onKeyDown={moveWithArrows}>
      {tabs.map((tab) => (
        <button
          key={tab.id}
          id={`${idPrefix}-tab-${tab.id}`}
          type="button"
          role="tab"
          aria-selected={tab.id === active}
          aria-controls={`${idPrefix}-panel`}
          tabIndex={tab.id === active ? 0 : -1}
          className={tab.id === active ? 'is-active' : ''}
          onClick={() => onChange(tab.id)}
        >
          {tab.label}
          {tab.count !== undefined && <span className="screen-tabs__count">{tab.count}</span>}
        </button>
      ))}
    </div>
  )
}
