import type { ComponentProps, ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { useTable, type OnChangeFn, type RowData, type SortingState } from '@tanstack/react-table'
import { LuArrowDown, LuArrowUp, LuArrowUpDown } from 'react-icons/lu'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { dataTableFeatures, type DataTableColumn } from '@/lib/table'

type DataTableProps<TData extends RowData> = {
  data: TData[]
  columns: DataTableColumn<TData>[]
  getRowId: (row: TData) => string
  caption: string
  emptyState?: ReactNode
  sorting: SortingState
  onSortingChange: OnChangeFn<SortingState>
  density?: 'default' | 'comfortable'
  tableClassName?: string
  containerProps?: Omit<ComponentProps<'div'>, 'children'>
}

/** Keep data, columns and getRowId stable; the caller owns the sorting state. */
export function DataTable<TData extends RowData>({
  data, columns, getRowId, caption, emptyState = 'No items to display.', sorting, onSortingChange,
  density = 'default', tableClassName, containerProps,
}: DataTableProps<TData>) {
  const table = useTable({
    features: dataTableFeatures,
    data,
    columns,
    getRowId,
    state: { sorting },
    onSortingChange,
    enableMultiSort: false,
    sortDescFirst: false,
  })
  const rows = table.getRowModel().rows

  return (
    <Table className={tableClassName} containerProps={containerProps} data-density={density}>
      <TableCaption>{caption}</TableCaption>
      <TableHeader>
        {table.getHeaderGroups().map(group => (
          <TableRow key={group.id}>
            {group.headers.map(header => {
              const direction = header.column.getIsSorted()
              const Icon = direction === 'asc' ? LuArrowUp : direction === 'desc' ? LuArrowDown : LuArrowUpDown
              const next = header.column.getNextSortingOrder()
              return (
                <TableHead key={header.id} colSpan={header.colSpan} scope={header.subHeaders.length ? 'colgroup' : 'col'}
                  className={cn(header.column.columnDef.meta?.align === 'right' && 'text-right tabular-nums', header.column.columnDef.meta?.wrap && 'whitespace-normal')}
                  aria-sort={direction === 'asc' ? 'ascending' : direction === 'desc' ? 'descending' : undefined}>
                  {header.isPlaceholder ? null : header.column.getCanSort() ? (
                    <Button type="button" variant="ghost" size={density === 'comfortable' ? 'touch' : 'sm'} className={header.column.columnDef.meta?.align === 'right' ? 'justify-end text-right' : undefined} onClick={header.column.getToggleSortingHandler()}>
                      <table.FlexRender header={header} />{' '}
                      <Icon aria-hidden="true" focusable="false" data-icon="inline-end" />
                      <span className="sr-only">{next === 'asc' ? 'Sort ascending' : next === 'desc' ? 'Sort descending' : 'Clear sorting'}</span>
                    </Button>
                  ) : <table.FlexRender header={header} />}
                </TableHead>
              )
            })}
          </TableRow>
        ))}
      </TableHeader>
      <TableBody>
        {rows.length ? rows.map(row => (
          <TableRow key={row.id}>
            {row.getAllCells().map(cell => {
              const meta = cell.column.columnDef.meta
              const Cell = meta?.rowHeader ? TableHead : TableCell
              return <Cell key={cell.id} scope={meta?.rowHeader ? 'row' : undefined}
                className={cn(meta?.align === 'right' && 'text-right tabular-nums', meta?.wrap && 'whitespace-normal')}>
                <table.FlexRender cell={cell} />
              </Cell>
            })}
          </TableRow>
        )) : (
          <TableRow><TableCell colSpan={Math.max(table.getAllLeafColumns().length, 1)} className="h-24 text-center text-muted-foreground">{emptyState}</TableCell></TableRow>
        )}
      </TableBody>
    </Table>
  )
}
