import { screenLabel, screenLabelMode, type ScreenId } from '../config/screens'

// Renders a screen heading as configured (code, title, or both).
// The hover text repeats the visible label only, so code mode never reveals the title.
export function ScreenName({ id }: { id: ScreenId }) {
  const label = screenLabel(id)
  return (
    <span className={`screen-name screen-name--${screenLabelMode}`} title={label}>
      {label}
    </span>
  )
}
