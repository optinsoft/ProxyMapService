export type ColumnKey =
  | 'time'
  | 'inbound'
  | 'route'
  | 'clientProto'
  | 'targetProto'
  | 'targetHost'
  | 'requestURI'
  | 'method'
  | 'status'
  | 'type'
  | 'size'
  | 'duration'

export type ColumnFilters = Record<ColumnKey, string>

export interface Column {
  key: ColumnKey
  label: string
}

export const allColumns: Column[] = [
  { key: 'time', label: 'Time' },
  { key: 'inbound', label: 'Inbound' },
  { key: 'route', label: 'Route' },
  { key: 'clientProto', label: 'Client Proto' },
  { key: 'targetProto', label: 'Target Proto' },
  { key: 'targetHost', label: 'Target Host' },
  { key: 'requestURI', label: 'Request URI' },
  { key: 'method', label: 'Method' },
  { key: 'status', label: 'Status' },
  { key: 'type', label: 'Type' },
  { key: 'size', label: 'Size' },
  { key: 'duration', label: 'Duration' },
]
