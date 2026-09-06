import { createSortedRowModel, rowSortingFeature, sortFns, tableFeatures } from '@tanstack/react-table'
import type { ColumnDef, RowData } from '@tanstack/react-table'

export const dataTableFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns,
  columnMeta: {} as { align?: 'left' | 'right'; wrap?: boolean; rowHeader?: boolean },
})

export type DataTableColumn<TData extends RowData> = ColumnDef<typeof dataTableFeatures, TData>
