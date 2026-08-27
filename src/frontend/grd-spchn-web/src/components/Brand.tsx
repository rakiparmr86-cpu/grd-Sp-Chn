interface BrandProps {
  compact?: boolean
  inverse?: boolean
}

export function Brand({ compact = false, inverse = false }: BrandProps) {
  return (
    <div className={`brand ${compact ? 'brand--compact' : ''} ${inverse ? 'brand--inverse' : ''}`}>
      <div className="brand__mark" aria-hidden="true">
        <span>G</span>
        <span>R</span>
        <span>D</span>
      </div>
      {!compact && (
        <div className="brand__copy">
          <strong>Supply Chain ERP</strong>
          <span>Move business forward</span>
        </div>
      )}
    </div>
  )
}

export function GridIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="3" width="6" height="6" rx="1" />
      <rect x="15" y="3" width="6" height="6" rx="1" />
      <rect x="3" y="15" width="6" height="6" rx="1" />
      <rect x="15" y="15" width="6" height="6" rx="1" />
    </svg>
  )
}
