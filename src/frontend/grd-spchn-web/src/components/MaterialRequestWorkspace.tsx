import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  api,
  ApiError,
  type LoginResponse,
  type MaterialRequest,
  type OrganizationUnit,
} from '../api'

interface MaterialRequestWorkspaceProps {
  session: LoginResponse
  onBack: () => void
}

interface MaterialLineForm {
  key: string
  productId: string
  quantity: string
  unitOfMeasure: string
}

const materialCatalog = [
  {
    id: '30000000-0000-0000-0000-000000000001',
    name: 'Packing Bag · 70 kg capacity',
    unitOfMeasure: 'BAG',
  },
  {
    id: '30000000-0000-0000-0000-000000000002',
    name: 'Production Coal',
    unitOfMeasure: 'MT',
  },
  {
    id: '30000000-0000-0000-0000-000000000003',
    name: 'Furnace Oil',
    unitOfMeasure: 'LTR',
  },
] as const

function createLine(catalogIndex = 0): MaterialLineForm {
  const material = materialCatalog[catalogIndex] ?? materialCatalog[0]
  return {
    key: crypto.randomUUID(),
    productId: material.id,
    quantity: '',
    unitOfMeasure: material.unitOfMeasure,
  }
}

export function MaterialRequestWorkspace({
  session,
  onBack,
}: MaterialRequestWorkspaceProps) {
  const [organizationUnits, setOrganizationUnits] = useState<OrganizationUnit[]>([])
  const [purpose, setPurpose] = useState('')
  const [lines, setLines] = useState<MaterialLineForm[]>([createLine()])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState<MaterialRequest | null>(null)

  useEffect(() => {
    let active = true
    api.getOrganizationUnits(session.accessToken)
      .then((units) => {
        if (active) setOrganizationUnits(units)
      })
      .catch(() => {
        // The authenticated organization id remains authoritative if names cannot be loaded.
      })

    return () => {
      active = false
    }
  }, [session.accessToken])

  const requestingUnit = useMemo(
    () => organizationUnits.find((unit) => unit.id === session.organizationUnitId),
    [organizationUnits, session.organizationUnitId],
  )

  function updateLine(key: string, updates: Partial<MaterialLineForm>) {
    setLines((current) => current.map((line) => (
      line.key === key ? { ...line, ...updates } : line
    )))
    setCreated(null)
  }

  function selectMaterial(key: string, productId: string) {
    const material = materialCatalog.find((entry) => entry.id === productId)
    updateLine(key, {
      productId,
      unitOfMeasure: material?.unitOfMeasure ?? 'EA',
    })
  }

  function addLine() {
    const unusedIndex = materialCatalog.findIndex(
      (material) => !lines.some((line) => line.productId === material.id),
    )
    setLines((current) => [...current, createLine(unusedIndex >= 0 ? unusedIndex : 0)])
    setCreated(null)
  }

  function removeLine(key: string) {
    setLines((current) => current.filter((line) => line.key !== key))
    setCreated(null)
  }

  function resetForm() {
    setPurpose('')
    setLines([createLine()])
    setError('')
    setCreated(null)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setCreated(null)

    const duplicateProducts = new Set(lines.map((line) => line.productId)).size !== lines.length
    if (duplicateProducts) {
      setError('Each material can appear only once in a requisition.')
      return
    }

    if (lines.some((line) => Number(line.quantity) <= 0)) {
      setError('Enter a quantity greater than zero for every material.')
      return
    }

    setSubmitting(true)
    try {
      const result = await api.createMaterialRequest(session.accessToken, {
        purpose: purpose.trim(),
        items: lines.map((line) => ({
          productId: line.productId,
          quantity: Number(line.quantity),
          unitOfMeasure: line.unitOfMeasure,
        })),
      })
      setCreated(result)
    } catch (reason) {
      setError(reason instanceof ApiError
        ? reason.message
        : 'The requisition could not be sent to the Purchase Department.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="requisition-workspace" aria-labelledby="material-request-title">
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1 id="material-request-title">GRD M. Requisition</h1>
          <p>Raise a plant requirement for review by the Purchase Department.</p>
        </div>
        <span className="workspace-status"><i /> New request</span>
      </header>

      <form className="requisition-card" onSubmit={handleSubmit}>
        <div className="requisition-card__heading">
          <div>
            <span className="eyebrow">Request details</span>
            <h2>Plant requirement</h2>
          </div>
          <span>Required fields are marked *</span>
        </div>

        <div className="requisition-routing-grid">
          <div className="read-only-field">
            <span>Requesting location</span>
            <strong>{requestingUnit?.name ?? 'Delhi Manufacturing Plant'}</strong>
            <small>{requestingUnit?.code ?? session.organizationUnitId}</small>
          </div>
          <div className="read-only-field">
            <span>Send to</span>
            <strong>Purchase Department</strong>
            <small>Approval and supplier sourcing queue</small>
          </div>
          <div className="read-only-field">
            <span>Requested by</span>
            <strong>{session.userName}</strong>
            <small>{session.role} · {session.accessProfile}</small>
          </div>
        </div>

        <label className="field requisition-purpose">
          <span>Business purpose *</span>
          <input
            value={purpose}
            onChange={(event) => {
              setPurpose(event.target.value)
              setCreated(null)
            }}
            maxLength={500}
            placeholder="Example: Packing bags required for September production"
            required
          />
        </label>

        <div className="request-lines-heading">
          <div>
            <h3>Requested materials</h3>
            <span>Add the item, required quantity, and unit of measure.</span>
          </div>
          <button
            className="add-line-button"
            type="button"
            onClick={addLine}
            disabled={lines.length >= materialCatalog.length}
          >
            + Add material
          </button>
        </div>

        <div className="request-lines" role="group" aria-label="Requested material lines">
          <div className="request-line request-line--header" aria-hidden="true">
            <span>Material</span>
            <span>Quantity</span>
            <span>UOM</span>
            <span />
          </div>
          {lines.map((line, index) => (
            <div className="request-line" key={line.key}>
              <label>
                <span className="mobile-field-label">Material</span>
                <select
                  value={line.productId}
                  onChange={(event) => selectMaterial(line.key, event.target.value)}
                  aria-label={`Material ${index + 1}`}
                  required
                >
                  {materialCatalog.map((material) => (
                    <option key={material.id} value={material.id}>{material.name}</option>
                  ))}
                </select>
              </label>
              <label>
                <span className="mobile-field-label">Quantity</span>
                <input
                  type="number"
                  min="0.001"
                  step="0.001"
                  value={line.quantity}
                  onChange={(event) => updateLine(line.key, { quantity: event.target.value })}
                  placeholder="0.000"
                  aria-label={`Quantity ${index + 1}`}
                  required
                />
              </label>
              <label>
                <span className="mobile-field-label">UOM</span>
                <select
                  value={line.unitOfMeasure}
                  onChange={(event) => updateLine(line.key, { unitOfMeasure: event.target.value })}
                  aria-label={`Unit of measure ${index + 1}`}
                  required
                >
                  <option value="BAG">BAG</option>
                  <option value="EA">EA</option>
                  <option value="KG">KG</option>
                  <option value="MT">MT</option>
                  <option value="LTR">LTR</option>
                </select>
              </label>
              <button
                className="remove-line-button"
                type="button"
                onClick={() => removeLine(line.key)}
                disabled={lines.length === 1}
                aria-label={`Remove material ${index + 1}`}
              >
                ×
              </button>
            </div>
          ))}
        </div>

        {error && <div className="form-alert requisition-message" role="alert">{error}</div>}
        {created && (
          <div className="success-alert requisition-message" role="status">
            <strong>{created.requestNumber}</strong> was submitted to the Purchase Department.
            Current status: <strong>{created.status}</strong>.
          </div>
        )}

        <div className="requisition-actions">
          <button className="secondary-button" type="button" onClick={resetForm}>Clear</button>
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? <><span className="spinner" /> Sending…</> : 'Send requisition'}
          </button>
        </div>
      </form>

      <section className="requisition-snapshot" aria-label="Requisition workflow">
        <div className="snapshot-heading">
          <strong>Requisition workflow</strong>
          <span>The Purchase Department receives the request after submission.</span>
        </div>
        <div className="snapshot-grid">
          <article>
            <span className="snapshot-number">1</span>
            <div><small>Raised by</small><strong>Plant Supervisor</strong></div>
          </article>
          <article>
            <span className="snapshot-number">2</span>
            <div><small>Next owner</small><strong>Purchase Department</strong></div>
          </article>
          <article>
            <span className={created ? 'snapshot-number is-complete' : 'snapshot-number'}>3</span>
            <div><small>Current status</small><strong>{created?.status ?? 'Draft'}</strong></div>
          </article>
        </div>
      </section>
    </section>
  )
}
