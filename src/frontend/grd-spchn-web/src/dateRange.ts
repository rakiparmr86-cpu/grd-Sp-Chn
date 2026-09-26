import type { DateRangeFilter } from './api'

// Date inputs work in the viewer's local calendar; the APIs work in UTC instants.
export function toInputDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function startOfLocalDay(value: string, addDays = 0): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day + addDays)
}

// Both dates are inclusive calendar days; an empty input leaves that side open.
export function toDateRangeFilter(fromDate: string, toDate: string): DateRangeFilter {
  return {
    fromUtc: fromDate ? startOfLocalDay(fromDate).toISOString() : undefined,
    toUtc: toDate ? startOfLocalDay(toDate, 1).toISOString() : undefined,
  }
}

export function dateRangeError(fromDate: string, toDate: string): string {
  return fromDate && toDate && fromDate > toDate
    ? 'The From date must be on or before the To date.'
    : ''
}
