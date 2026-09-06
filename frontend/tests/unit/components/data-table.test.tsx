import { useState } from 'react'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { functionalUpdate, type SortingState } from '@tanstack/react-table'
import { expect, test, vi } from 'vitest'
import { DataTable } from '@/components/data-table'
import type { DataTableColumn } from '@/lib/table'

type Note = { id: string; label: string; category: string }
const notes: Note[] = [
  { id: 'note-b', label: 'Bravo', category: 'Draft' },
  { id: 'note-a', label: 'Alpha', category: 'Reviewed' },
]
const columns: DataTableColumn<Note>[] = [
  { accessorKey: 'label', header: 'Label' },
  { accessorKey: 'category', header: 'Category', enableSorting: false, cell: ({ row }) => <em>{row.original.category}</em> },
]
const getRowId = (note: Note) => note.id
const noSorting: SortingState = []

test('optional presentation metadata supplies row headers and a labelled scroll container', () => {
  const presentation: DataTableColumn<Note>[] = [{ accessorKey: 'label', header: 'Label', enableSorting: false, meta: { rowHeader: true, wrap: true, align: 'right' } }]
  render(<DataTable data={notes} columns={presentation} getRowId={getRowId} caption="Comfortable notes" sorting={noSorting} onSortingChange={vi.fn()} density="comfortable" tableClassName="min-w-[38rem]" containerProps={{ role: 'region', 'aria-label': 'Notes scroll area', tabIndex: 0 }} />)
  expect(screen.getByRole('region', { name: 'Notes scroll area' })).toHaveAttribute('tabindex', '0')
  expect(screen.getByRole('rowheader', { name: 'Bravo' })).toHaveAttribute('scope', 'row')
  expect(screen.getByRole('table')).toHaveAttribute('data-density', 'comfortable')
})

test('renders a named semantic table with typed cells and decorative icons', () => {
  render(<DataTable data={notes} columns={columns} getRowId={getRowId} caption="Reference notes" sorting={noSorting} onSortingChange={vi.fn()} />)
  const table = screen.getByRole('table', { name: 'Reference notes' })
  expect(within(table).getAllByRole('columnheader')).toHaveLength(2)
  expect(within(table).getByText('Reviewed').tagName).toBe('EM')
  expect(within(table).getAllByRole('row')).toHaveLength(3)
  const button = within(table).getByRole('button', { name: 'Label Sort ascending' })
  expect(button).toHaveAttribute('type', 'button')
  expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  expect(within(table).queryByRole('button', { name: /Category/ })).not.toBeInTheDocument()
})

test('empty states span all columns and retain the caption', () => {
  const view = render(<DataTable data={[]} columns={columns} getRowId={getRowId} caption="Reference notes" sorting={noSorting} onSortingChange={vi.fn()} />)
  expect(screen.getByRole('cell', { name: 'No items to display.' })).toHaveAttribute('colspan', '2')
  view.rerender(<DataTable data={[]} columns={columns} getRowId={getRowId} caption="Reference notes" emptyState="No matching notes." sorting={noSorting} onSortingChange={vi.fn()} />)
  expect(screen.getByRole('cell', { name: 'No matching notes.' })).toBeInTheDocument()
  expect(screen.getByRole('table', { name: 'Reference notes' })).toBeInTheDocument()
})

test('sorting requests an external update before reordering and accepts external reset', async () => {
  const onSortingChange = vi.fn()
  const view = render(<DataTable data={notes} columns={columns} getRowId={getRowId} caption="Reference notes" sorting={noSorting} onSortingChange={onSortingChange} />)
  fireEvent.click(screen.getByRole('button', { name: 'Label Sort ascending' }))
  expect(onSortingChange).toHaveBeenCalledTimes(1)
  expect(screen.getAllByRole('row')[1]).toHaveTextContent('Bravo')
  const next = functionalUpdate(onSortingChange.mock.calls[0]![0], noSorting)
  expect(next).toEqual([{ id: 'label', desc: false }])
  view.rerender(<DataTable data={notes} columns={columns} getRowId={getRowId} caption="Reference notes" sorting={next} onSortingChange={onSortingChange} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('Alpha'))
  expect(screen.getByRole('columnheader', { name: /Label/ })).toHaveAttribute('aria-sort', 'ascending')
  view.rerender(<DataTable data={notes} columns={columns} getRowId={getRowId} caption="Reference notes" sorting={noSorting} onSortingChange={onSortingChange} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('Bravo'))
  expect(screen.getByRole('columnheader', { name: /Label/ })).not.toHaveAttribute('aria-sort')
})

function EditableLabel({ note }: { note: Note }) {
  const [label, setLabel] = useState(note.label)
  return <input aria-label={`Edit ${note.id}`} value={label} onChange={event => setLabel(event.target.value)} />
}
const editableColumns: DataTableColumn<Note>[] = [
  { accessorKey: 'label', header: 'Label', cell: ({ row }) => <EditableLabel note={row.original} /> },
]

test('canonical row identity preserves cell state across data replacement and sorting', async () => {
  function Harness({ data }: { data: Note[] }) {
    const [sorting, setSorting] = useState<SortingState>([])
    return <DataTable data={data} columns={editableColumns} getRowId={getRowId} caption="Editable notes" sorting={sorting} onSortingChange={setSorting} />
  }
  const view = render(<Harness data={notes} />)
  fireEvent.change(screen.getByRole('textbox', { name: 'Edit note-b' }), { target: { value: 'Unsaved note' } })
  view.rerender(<Harness data={[{ ...notes[1]! }, { ...notes[0]! }]} />)
  expect(screen.getByRole('textbox', { name: 'Edit note-b' })).toHaveValue('Unsaved note')
  expect(screen.getByRole('textbox', { name: 'Edit note-a' })).toHaveValue('Alpha')
  fireEvent.click(screen.getByRole('button', { name: 'Label Sort ascending' }))
  await screen.findByRole('button', { name: 'Label Sort descending' })
  fireEvent.click(screen.getByRole('button', { name: 'Label Sort descending' }))
  await waitFor(() => expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'descending'))
  expect(within(screen.getAllByRole('row')[1]!).getByRole('textbox')).toHaveValue('Unsaved note')
  fireEvent.click(screen.getByRole('button', { name: 'Label Clear sorting' }))
  await waitFor(() => expect(screen.getByRole('columnheader')).not.toHaveAttribute('aria-sort'))
})
