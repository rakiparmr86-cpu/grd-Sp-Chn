import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, ApiError, type AccountBalance, type AccountType, type LoginResponse } from '../api'
import { SCREEN, type ScreenId } from '../config/screens'
import { startOfLocalDay, toInputDate } from '../dateRange'
import { DateRangeSearch } from './DateRangeSearch'
import { ScreenName } from './ScreenName'

interface StatementProps {
  session: LoginResponse
  onBack: () => void
}

const moneyFormat = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const money = (value: number) => `₹ ${moneyFormat.format(Math.abs(value))}`
const drCr = (value: number) => (value === 0 ? '₹ 0.00' : `${money(value)} ${value > 0 ? 'Dr' : 'Cr'}`)
const typeOrder: AccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense', 'Unclassified']

// Indian financial year: 1 April to 31 March.
function financialYearStart(date: Date): Date {
  const year = date.getMonth() >= 3 ? date.getFullYear() : date.getFullYear() - 1
  return new Date(year, 3, 1)
}

function useAccountBalances(session: LoginResponse) {
  const today = useMemo(() => new Date(), [])
  const [fromDate, setFromDate] = useState(() => toInputDate(financialYearStart(today)))
  const [toDate, setToDate] = useState(() => toInputDate(today))
  const [accounts, setAccounts] = useState<AccountBalance[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const result = await api.getAccountBalances(
        session.accessToken,
        startOfLocalDay(fromDate).toISOString(),
        startOfLocalDay(toDate, 1).toISOString(),
      )
      setAccounts(result.accounts)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not load account balances.')
    } finally {
      setLoading(false)
    }
  }, [fromDate, session.accessToken, toDate])

  useEffect(() => {
    void load()
  }, [load])

  function applyDates(nextFrom: string, nextTo: string) {
    if (nextFrom === fromDate && nextTo === toDate) {
      void load()
      return
    }
    setFromDate(nextFrom)
    setToDate(nextTo)
  }

  return { fromDate, toDate, accounts, loading, error, applyDates }
}

function StatementShell({
  screen,
  description,
  data,
  onBack,
  fromLabel,
  toLabel,
  children,
}: {
  screen: ScreenId
  description: string
  data: ReturnType<typeof useAccountBalances>
  onBack: () => void
  fromLabel?: string
  toLabel?: string
  children: ReactNode
}) {
  return (
    <>
      <header className="workspace-title">
        <div>
          <button className="workspace-back" type="button" onClick={onBack}>← Dashboard</button>
          <h1><ScreenName id={screen} /></h1>
          <p>{description}</p>
        </div>
        <span className="workspace-status"><i /> Accounts</span>
      </header>
      <div className="list-filters" role="search" aria-label="Report period">
        <DateRangeSearch
          fromDate={data.fromDate}
          toDate={data.toDate}
          onSearch={data.applyDates}
          busy={data.loading}
          required
          fromLabel={fromLabel}
          toLabel={toLabel}
        />
      </div>
      {data.error && <div className="form-alert requisition-list-alert" role="alert">{data.error}</div>}
      {data.loading && data.accounts.length === 0 ? (
        <section className="requisition-list-card">
          <div className="requisition-list-empty"><span className="spinner spinner--dark" /> Loading…</div>
        </section>
      ) : children}
    </>
  )
}

function BalancedNote({ left, right, leftLabel, rightLabel }: { left: number; right: number; leftLabel: string; rightLabel: string }) {
  const balanced = Math.abs(left - right) < 0.005
  return (
    <span className={`tracking-status ${balanced ? 'tracking-status--received' : 'tracking-status--submitted'}`}>
      {balanced ? `✓ Balanced · ${leftLabel} = ${rightLabel} = ${money(left)}` : `⚠ Not balanced · ${leftLabel} ${money(left)} vs ${rightLabel} ${money(right)}`}
    </span>
  )
}


const STOCK_ACCOUNT = 'INVENTORY'
// Signed amount on its own side of a statement; a negative figure is shown with a minus.
const signedMoney = (value: number) => (value < 0 ? `- ${money(value)}` : money(value))
const opening = (account: AccountBalance) => account.openingDebit - account.openingCredit

interface StatementRow {
  key: string
  label: string
  detail?: string
  amount: number
  opening?: number
}

export function TrialBalanceWorkspace({ session, onBack }: StatementProps) {
  const data = useAccountBalances(session)
  // Every account in the chart is listed, including Cash, Bank and Stock at zero.
  const rows = data.accounts
  const sum = (pick: (account: AccountBalance) => number) => rows.reduce((total, account) => total + pick(account), 0)
  const openingDebit = sum((account) => Math.max(opening(account), 0))
  const openingCredit = sum((account) => Math.max(-opening(account), 0))
  const closingDebit = sum((account) => Math.max(account.closing, 0))
  const closingCredit = sum((account) => Math.max(-account.closing, 0))

  return (
    <section className="requisition-workspace" aria-label="Trial balance">
      <StatementShell
        screen={SCREEN.trialBalance}
        description="Opening balance, period debits and credits, and closing balance of every account."
        data={data}
        onBack={onBack}
      >
        <section className="requisition-list-card">
          <div className="requisition-list-heading">
            <div>
              <strong>Trial balance</strong>
              <span>{data.fromDate} to {data.toDate}</span>
            </div>
            <BalancedNote left={closingDebit} right={closingCredit} leftLabel="Total Dr" rightLabel="Total Cr" />
          </div>
          <div className="requisition-table-scroll">
            <table className="requisition-table stock-ledger-table financial-table">
              <thead>
                <tr>
                  <th>Account</th>
                  <th className="is-number">Opening Dr</th>
                  <th className="is-number">Opening Cr</th>
                  <th className="is-number">Period Dr</th>
                  <th className="is-number">Period Cr</th>
                  <th className="is-number">Closing Dr</th>
                  <th className="is-number">Closing Cr</th>
                </tr>
              </thead>
              {typeOrder.map((type) => {
                const typeRows = rows.filter((account) => account.type === type)
                if (typeRows.length === 0) return null
                return (
                  <tbody key={type}>
                    <tr className="stock-ledger-group"><td colSpan={7}><strong>{type === 'Unclassified' ? 'Unclassified (check chart of accounts)' : `${type} accounts`}</strong></td></tr>
                    {typeRows.map((account) => (
                      <tr key={account.code}>
                        <td><strong>{account.name}</strong><small>{account.group} · {account.code}</small></td>
                        <td className="is-number">{opening(account) > 0 ? money(opening(account)) : ''}</td>
                        <td className="is-number">{opening(account) < 0 ? money(opening(account)) : ''}</td>
                        <td className="is-number">{account.periodDebit ? money(account.periodDebit) : ''}</td>
                        <td className="is-number">{account.periodCredit ? money(account.periodCredit) : ''}</td>
                        <td className="is-number"><strong>{account.closing > 0 ? money(account.closing) : ''}</strong></td>
                        <td className="is-number"><strong>{account.closing < 0 ? money(account.closing) : ''}</strong></td>
                      </tr>
                    ))}
                  </tbody>
                )
              })}
              <tfoot>
                <tr className="stock-ledger-balance-row">
                  <td>Total</td>
                  <td className="is-number">{money(openingDebit)}</td>
                  <td className="is-number">{money(openingCredit)}</td>
                  <td className="is-number">{money(sum((account) => account.periodDebit))}</td>
                  <td className="is-number">{money(sum((account) => account.periodCredit))}</td>
                  <td className="is-number">{money(closingDebit)}</td>
                  <td className="is-number">{money(closingCredit)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </section>
      </StatementShell>
    </section>
  )
}

function StatementTable({ title, rows, total, totalLabel, showOpening = false, openingTotal = 0 }: {
  title: string
  rows: StatementRow[]
  total: number
  totalLabel: string
  showOpening?: boolean
  openingTotal?: number
}) {
  return (
    <section className="requisition-list-card financial-side">
      <div className="requisition-list-heading"><div><strong>{title}</strong></div></div>
      <table className="requisition-table stock-ledger-table">
        {showOpening && (
          <thead>
            <tr>
              <th>Particulars</th>
              <th className="is-number">Opening</th>
              <th className="is-number">Closing</th>
            </tr>
          </thead>
        )}
        <tbody>
          {rows.length === 0 ? (
            <tr><td className="requisition-list-empty" colSpan={showOpening ? 3 : 2}>Nothing posted.</td></tr>
          ) : rows.map((row) => (
            <tr key={row.key}>
              <td><strong>{row.label}</strong>{row.detail && <small>{row.detail}</small>}</td>
              {showOpening && <td className="is-number">{signedMoney(row.opening ?? 0)}</td>}
              <td className="is-number">{signedMoney(row.amount)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="stock-ledger-balance-row">
            <td>{totalLabel}</td>
            {showOpening && <td className="is-number">{signedMoney(openingTotal)}</td>}
            <td className="is-number">{signedMoney(total)}</td>
          </tr>
        </tfoot>
      </table>
    </section>
  )
}

const totalOf = (rows: StatementRow[]) => rows.reduce((total, row) => total + row.amount, 0)

export function ProfitAndLossWorkspace({ session, onBack }: StatementProps) {
  const data = useAccountBalances(session)
  const stock = data.accounts.find((account) => account.code === STOCK_ACCOUNT)
  // Trading account from the stock ledger: opening stock + purchases - closing stock is the
  // cost of goods consumed. Purchases are the stock debits of the period (accepted GRNs).
  // A future consumption posting must credit Stock against a non-expense account (or be
  // excluded here) so that cost is not counted twice.
  const openingStock = stock ? opening(stock) : 0
  const purchases = stock?.periodDebit ?? 0
  const closingStock = stock?.closing ?? 0
  const income = data.accounts
    .filter((account) => account.type === 'Income')
    .map((account) => ({ key: account.code, label: account.name, detail: account.group, amount: account.periodCredit - account.periodDebit }))
  const expenses = data.accounts
    .filter((account) => account.type === 'Expense')
    .map((account) => ({ key: account.code, label: account.name, detail: account.group, amount: account.periodDebit - account.periodCredit }))
  const sales = totalOf(income)
  const grossProfit = sales + closingStock - openingStock - purchases
  const netProfit = grossProfit - totalOf(expenses)

  const tradingDebit: StatementRow[] = [
    { key: 'opening-stock', label: 'To Opening stock', detail: `Stock at ${data.fromDate}`, amount: openingStock },
    { key: 'purchases', label: 'To Purchases', detail: 'Material accepted into stock (GRN)', amount: purchases },
    ...(grossProfit > 0 ? [{ key: 'gp', label: 'To Gross profit c/d', amount: grossProfit }] : []),
  ]
  const tradingCredit: StatementRow[] = [
    ...(income.length > 0
      ? income.map((row) => ({ ...row, label: `By ${row.label}` }))
      : [{ key: 'sales', label: 'By Sales', detail: 'No sales posted', amount: 0 }]),
    { key: 'closing-stock', label: 'By Closing stock', detail: `Stock at ${data.toDate}`, amount: closingStock },
    ...(grossProfit < 0 ? [{ key: 'gl', label: 'By Gross loss c/d', amount: -grossProfit }] : []),
  ]
  const plDebit: StatementRow[] = [
    ...(grossProfit < 0 ? [{ key: 'gl-bd', label: 'To Gross loss b/d', amount: -grossProfit }] : []),
    ...expenses.map((row) => ({ ...row, label: `To ${row.label}` })),
    ...(netProfit > 0 ? [{ key: 'np', label: 'To Net profit', detail: 'Transferred to capital', amount: netProfit }] : []),
  ]
  const plCredit: StatementRow[] = [
    { key: 'gp-bd', label: 'By Gross profit b/d', amount: Math.max(grossProfit, 0) },
    ...(netProfit < 0 ? [{ key: 'nl', label: 'By Net loss', detail: 'Transferred to capital', amount: -netProfit }] : []),
  ]

  return (
    <section className="requisition-workspace" aria-label="Profit and loss">
      <StatementShell
        screen={SCREEN.profitAndLoss}
        description="Trading account (opening stock, purchases, sales, closing stock) and profit and loss account for the period."
        data={data}
        onBack={onBack}
      >
        <h2 className="financial-section-title">Trading account</h2>
        <div className="financial-columns">
          <StatementTable title="Dr · Particulars" rows={tradingDebit} total={totalOf(tradingDebit)} totalLabel="Total" />
          <StatementTable title="Cr · Particulars" rows={tradingCredit} total={totalOf(tradingCredit)} totalLabel="Total" />
        </div>
        <h2 className="financial-section-title">Profit and loss account</h2>
        <div className="financial-columns">
          <StatementTable title="Dr · Particulars" rows={plDebit} total={totalOf(plDebit)} totalLabel="Total" />
          <StatementTable title="Cr · Particulars" rows={plCredit} total={totalOf(plCredit)} totalLabel="Total" />
        </div>
        <section className="requisition-list-card financial-result">
          <div className="requisition-list-heading">
            <div>
              <strong>{netProfit >= 0 ? 'Net profit' : 'Net loss'}</strong>
              <span>{data.fromDate} to {data.toDate} · Gross {grossProfit >= 0 ? 'profit' : 'loss'} {money(grossProfit)}</span>
            </div>
            <strong className={netProfit >= 0 ? 'financial-result__profit' : 'financial-result__loss'}>{money(netProfit)}</strong>
          </div>
        </section>
      </StatementShell>
    </section>
  )
}

export function BalanceSheetWorkspace({ session, onBack }: StatementProps) {
  const data = useAccountBalances(session)
  const assets: StatementRow[] = []
  const liabilities: StatementRow[] = []
  let profitAndLossOpening = 0
  let profitAndLossClosing = 0

  for (const account of data.accounts) {
    if (account.type === 'Income' || account.type === 'Expense') {
      // Income and expense close into the Profit and Loss account (credit = profit).
      profitAndLossOpening -= opening(account)
      profitAndLossClosing -= account.closing
      continue
    }
    // Side follows the closing balance (debit = asset, credit = liability/capital); a zero
    // balance stays on the account's normal side so Cash, Bank and Stock always appear.
    const onAssetSide = account.closing !== 0 ? account.closing > 0 : account.type === 'Asset' || account.type === 'Unclassified'
    const unusual = (account.type === 'Asset' && !onAssetSide) || (account.type === 'Liability' && onAssetSide)
    const label = account.type === 'Asset' && !onAssetSide ? `${account.name} (overdraft)` : account.name
    const sign = onAssetSide ? 1 : -1
    const row = {
      key: account.code,
      label,
      detail: `${account.group}${unusual ? ' · opposite to normal side' : ''}${account.type === 'Unclassified' ? ' · unclassified' : ''}`,
      opening: sign * opening(account),
      amount: sign * account.closing,
    }
    if (onAssetSide) assets.push(row)
    else liabilities.push(row)
  }
  liabilities.push({
    key: 'PL',
    label: 'Profit and loss account',
    detail: profitAndLossClosing >= 0 ? 'Accumulated profit' : 'Accumulated loss (shown negative)',
    opening: profitAndLossOpening,
    amount: profitAndLossClosing,
  })
  const totalAssets = totalOf(assets)
  const totalLiabilities = totalOf(liabilities)
  const openingOf = (rows: StatementRow[]) => rows.reduce((total, row) => total + (row.opening ?? 0), 0)

  return (
    <section className="requisition-workspace" aria-label="Balance sheet">
      <StatementShell
        screen={SCREEN.balanceSheet}
        description="Assets against liabilities and capital, at the start of the year and as at the chosen date."
        data={data}
        onBack={onBack}
        fromLabel="Year from"
        toLabel="As at"
      >
        <div className="financial-summary">
          <span>Opening = {startOfLocalDay(data.fromDate).toLocaleDateString('en-IN')} · Closing = as at {startOfLocalDay(data.toDate).toLocaleDateString('en-IN')}</span>
          <BalancedNote left={totalLiabilities} right={totalAssets} leftLabel="Liabilities" rightLabel="Assets" />
        </div>
        <div className="financial-columns">
          <StatementTable title="Liabilities and capital" rows={liabilities} total={totalLiabilities} totalLabel="Total" showOpening openingTotal={openingOf(liabilities)} />
          <StatementTable title="Assets" rows={assets} total={totalAssets} totalLabel="Total" showOpening openingTotal={openingOf(assets)} />
        </div>
      </StatementShell>
    </section>
  )
}
