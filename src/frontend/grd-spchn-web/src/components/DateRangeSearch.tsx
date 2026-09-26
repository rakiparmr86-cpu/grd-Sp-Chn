import { useEffect, useState, type KeyboardEvent } from 'react'
import { dateRangeError } from '../dateRange'

interface DateRangeSearchProps {
  // The range currently applied to the list; edits stay local until Search is pressed.
  fromDate: string
  toDate: string
  onSearch: (fromDate: string, toDate: string) => void
  required?: boolean
  busy?: boolean
  fromLabel?: string
  toLabel?: string
}

// From / To date inputs plus a Search button, rendered inside a .list-filters bar.
export function DateRangeSearch({
  fromDate,
  toDate,
  onSearch,
  required = false,
  busy = false,
  fromLabel = 'From',
  toLabel = 'To',
}: DateRangeSearchProps) {
  const [draftFrom, setDraftFrom] = useState(fromDate)
  const [draftTo, setDraftTo] = useState(toDate)

  // Follow the applied range when the screen changes it (for example Clear filters).
  useEffect(() => setDraftFrom(fromDate), [fromDate])
  useEffect(() => setDraftTo(toDate), [toDate])

  const error = required && (!draftFrom || !draftTo)
    ? 'Select both From and To dates.'
    : dateRangeError(draftFrom, draftTo)

  function search() {
    if (!error) onSearch(draftFrom, draftTo)
  }

  function searchOnEnter(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') search()
  }

  return (
    <>
      <label>
        <span>{fromLabel}</span>
        <input
          type="date"
          value={draftFrom}
          max={draftTo || undefined}
          required={required}
          onChange={(event) => setDraftFrom(event.target.value)}
          onKeyDown={searchOnEnter}
        />
      </label>
      <label>
        <span>{toLabel}</span>
        <input
          type="date"
          value={draftTo}
          min={draftFrom || undefined}
          required={required}
          onChange={(event) => setDraftTo(event.target.value)}
          onKeyDown={searchOnEnter}
        />
      </label>
      <button
        type="button"
        className="primary-button list-filters__search-button"
        onClick={search}
        disabled={!!error || busy}
      >
        {busy ? 'Searching…' : 'Search'}
      </button>
      {error && <span className="list-filters__error" role="alert">{error}</span>}
    </>
  )
}
