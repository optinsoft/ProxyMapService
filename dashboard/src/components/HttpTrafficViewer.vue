<script setup lang="ts">
import { ref, computed } from 'vue'
import type { HttpRequestEntry, HttpResponseEntry, HttpBodyEntry, MergedTrafficEntry, HttpCompletionEntry } from '../types/log'
import HttpBodyViewer from './HttpBodyViewer.vue'

const props = defineProps<{
  requests: HttpRequestEntry[],
  responses: HttpResponseEntry[],
  completions: HttpCompletionEntry[],  
  requestBodies: HttpBodyEntry[],
  responseBodies: HttpBodyEntry[],
  isConnected: boolean,
  isCapturing: boolean
}>()

const emit = defineEmits<{
  (e: 'clear-network'): void,
  (e: 'toggle-capture'): void
}>()

const selectedId = ref<string | null>(null)

const inspectorWidth = ref(420)

const isResizing = ref(false)

const activeTab = ref<'request' | 'response'>('request')

// Match requests and responses by unique ID
const mergedTransactions = computed<MergedTrafficEntry[]>(() => {
  const responsesMap = new Map(props.responses.map(r => [r.id, r]))
  const completionsMap = new Map(props.completions.map(r => [r.id, r]))
  const reqBodiesMap = new Map(props.requestBodies.map(b => [b.id, b]))
  const resBodiesMap = new Map(props.responseBodies.map(b => [b.id, b]))
  
  return props.requests.map(req => {    
    const res = responsesMap.get(req.id)
    const completion = completionsMap.get(req.id) || null
    const reqBody = reqBodiesMap.get(req.id) || null
    const resBody = resBodiesMap.get(req.id) || null
    
    // Parse timestamp calculations
    const reqTime = new Date(req.timestamp).getTime()
    const compTime = 
      res?.completed 
      ? new Date(res.timestamp).getTime() 
      : (
        resBody?.completed
        ? new Date(resBody.timestamp).getTime()
        : (
          completion 
          ? new Date(completion.timestamp).getTime() 
          : null
        )
      )
    const duration = (compTime && reqTime) ? (compTime - reqTime) : null

    const routeDisplay = res?.route || req.route || '-'
    const targetHostDisplay = res?.targetHost || req.targetHost || '-'

    return {
      id: req.id,
      timestamp: new Date(req.timestamp).toLocaleTimeString(),
      inbound: req.inbound,
      requestURI: req.requestURI,
      requestMethod: req.requestMethod.toUpperCase(),
      route: routeDisplay,
      targetHost: targetHostDisplay,
      statusCode: res ? parseInt(res.statusCode, 10) : null,
      statusText: res ? res.statusText : '',
      type: res ? res.type : '',
      size: typeof res?.size === 'number' ? res.size : resBody?.length || null,
      durationMs: duration,
      completed: !!completion,
      requestHeaders: req.headers || {},
      responseHeaders: res?.headers || {},
      requestBody: reqBody,
      responseBody: resBody
    }
  })
})

// Retrieve the currently active transaction for header inspections
const selectedTransaction = computed(() => {
  return mergedTransactions.value.find(tx => tx.id === selectedId.value) || null
})

const getStatusClass = (status: number | null): string => {
  if (!status) return 'status-pending'
  if (status >= 200 && status < 300) return 'status-success'
  if (status >= 300 && status < 400) return 'status-redirect'
  return 'status-error'
}

const selectRow = (id: string) => {
  // Toggle selection closed if clicked again, otherwise open details
  selectedId.value = selectedId.value === id ? null : id
}
const startResize = (e: MouseEvent) => {
  isResizing.value = true

  const startX = e.clientX
  const startWidth = inspectorWidth.value

  const onMove = (ev: MouseEvent) => {
    const delta = startX - ev.clientX

    inspectorWidth.value = Math.min(
      1200,
      Math.max(250, startWidth + delta)
    )
  }

  const onUp = () => {
    isResizing.value = false
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onUp)
  }

  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return '0 B'
  }

  const labels = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))

  if (i >= labels.length) {
    return `${(bytes / Math.pow(1024, labels.length - 1)).toFixed(2)} ${labels[labels.length - 1]}`
  }

  const value = bytes / Math.pow(1024, i)

  let decimals = 0;
  if (i === 1 || i === 2) {
    decimals = 1;
  } else if (i > 2) {
    decimals = 2
  }

  const formattedValue = value.toFixed(decimals).replace(/\.0+$/, '')

  return `${formattedValue} ${labels[i]}`
}

function formatDuration(ms: number): string {
  if (!Number.isFinite(ms) || ms <= 0) {
    return '0 ms'
  }

  const i = Math.floor(Math.log(ms) / Math.log(1000))
  if ( i > 0) {
    return `${(ms / 1000).toFixed(2)} s`
  } 
  
  return `${ms} ms`
}

const allColumns = [
  { key: 'time', label: 'Time' },
  { key: 'inbound', label: 'Inbound' },
  { key: 'route', label: 'Route' },
  { key: 'targetHost', label: 'Target Host' },
  { key: 'requestURI', label: 'Request URI' },
  { key: 'method', label: 'Method' },
  { key: 'status', label: 'Status' },
  { key: 'type', label: 'Type' },
  { key: 'size', label: 'Size' },
  { key: 'duration', label: 'Duration' },
]

const visibleColumns = ref(allColumns.map(c => c.key))

const displayedColumns = computed(() =>
    allColumns.filter(c => visibleColumns.value.includes(c.key))
)
</script>

<template>
  <div class="http-traffic-viewer">
    <div class="toolbar">
      <div class="status-indicator">
        <span :class="['dot', isConnected ? 'online' : 'offline']"></span>
        {{ isConnected ? 'Connected' : 'Disconnected' }}
        
        <button 
          @click="emit('toggle-capture');" 
          :class="['capture-btn', isCapturing ? 'active' : 'paused']"
        >
          {{ isCapturing ? '⏸️ Pause Capture' : '▶️ Resume Capture' }}
        </button>
      </div>
      <div class="controls">
        <div class="columns-menu">
          <button class="btn-columns">
            Columns ▼
          </button>
          <div class="columns-popup">
            <label
              v-for="column in allColumns"
              :key="column.key"
            >
              <input
                type="checkbox"
                v-model="visibleColumns"
                :value="column.key"
              >
              {{ column.label }}
            </label>
          </div>
        </div>
        <button class="btn-clear" @click="emit('clear-network'); selectedId = null">Clear</button>
      </div>
    </div>

    <!-- Master-Detail Layout Container -->
    <div class="workspace">
      <!-- Left side: The Main log table grid -->
      <div class="table-container">
        <table class="traffic-table">
          <thead>
            <tr>
              <th
                v-for="column in displayedColumns"
                :key="column.key"
                :class="`col-${column.key}`"
              >
                {{ column.label }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="mergedTransactions.length === 0">
              <td :colspan="displayedColumns.length" class="empty-row">No transactions captured yet...</td>
            </tr>
            <tr 
              v-for="(tx, index) in mergedTransactions" 
              :key="tx.id"
              :data-row-number="index + 1"
              :class="{ 'active-row': selectedId === tx.id }"
              @click="selectRow(tx.id)"
            >
              <td
                v-for="column in displayedColumns"
                :key="column.key"
                :class="`cell-${column.key}`"
              >
                <template v-if="column.key === 'time'">
                  {{ tx.timestamp }}
                </template>
                <template v-else-if="column.key === 'inbound'">
                  {{ tx.inbound }}
                </template>
                <template v-else-if="column.key === 'route'">
                  <span
                    :class="[
                      'route-badge',
                      {
                        direct: 'route-direct',
                        file: 'route-file'
                      }[tx.route.toLowerCase()] || 'route-proxy'
                    ]"
                  >
                    {{ tx.route }}
                  </span>
                </template>
                <template v-else-if="column.key === 'targetHost'">
                  {{ tx.targetHost }}
                </template>
                <template v-else-if="column.key === 'requestURI'">
                  <span :title="tx.requestURI">
                    {{ tx.requestURI }}
                  </span>
                </template>
                <template v-else-if="column.key === 'method'">
                  {{ tx.requestMethod }}
                </template>
                <template v-else-if="column.key === 'status'">
                  <span :class="['status-tag', getStatusClass(tx.statusCode)]">
                    {{
                      tx.statusCode
                        ? `${tx.statusCode} ${tx.statusText}`
                        : (tx.completed ? 'Interrupted' : 'Pending...')
                    }}
                  </span>
                </template>
                <template v-else-if="column.key === 'type'">
                  {{ tx.type || '-' }}
                </template>
                <template v-else-if="column.key === 'size'">
                  {{ typeof tx.size === 'number' ? formatBytes(tx.size) : '-' }}
                </template>
                <template v-else-if="column.key === 'duration'">
                  {{ tx.durationMs !== null ? formatDuration(tx.durationMs) : '-' }}
                </template>
              </td>              
            </tr>
          </tbody>
        </table>
      </div>

      <!-- splitter between the Main log table grid and the headers inspector sidebar -->
      <div
        v-if="selectedTransaction"
        class="splitter"
        @mousedown="startResize"
      ></div>

      <!-- Right side: Headers inspector sidebar (Appears only when a row is clicked) -->
      <div 
        v-if="selectedTransaction" 
        class="details-sidebar"
        :style="{ width: `${inspectorWidth}px` }"
      >
        <div class="sidebar-header">
          <h4>Transaction Inspector</h4>
          <button class="btn-close" @click="selectedId = null">✕</button>
        </div>
        
        <div class="sidebar-content">          
          <div class="info-block">
            <p><strong>Time:</strong> <span class="mono">{{ selectedTransaction.timestamp }}</span></p>            
            <p><strong>Inbound:</strong> <span class="mono">{{ selectedTransaction.inbound }}</span></p>
            <p><strong>Route:</strong> <span class="mono">{{ selectedTransaction.route }}</span></p>
            <p><strong>Target Host:</strong> <span class="mono">{{ selectedTransaction.targetHost }}</span></p>
            <p><strong>Request URI:</strong> <span class="mono">{{ selectedTransaction.requestURI }}</span></p>
            <p><strong>Request Method:</strong> <span class="mono">{{ selectedTransaction.requestMethod }}</span></p>
            <p><strong>Status Code:</strong> <span class="mono">{{ selectedTransaction.statusCode || '-' }}</span></p>
            <p><strong>Status Text:</strong> <span class="mono">{{ selectedTransaction.statusCode ? selectedTransaction.statusText : (selectedTransaction.completed ? 'Interrupted' : 'Pending...') }}</span></p>
            <p><strong>Type:</strong> <span class="mono">{{ selectedTransaction.type || '-' }}</span></p>
            <p><strong>Size:</strong> <span class="mono">{{ typeof selectedTransaction.size === 'number' ? selectedTransaction.size : '-' }}</span></p>
            <p><strong>Duration (ms):</strong> <span class="mono">{{ typeof selectedTransaction.durationMs === 'number' ? selectedTransaction.durationMs : '-' }}</span></p>
          </div>

          <div class="tabs">
            <button
              :class="{ active: activeTab === 'request' }"
              @click="activeTab = 'request'"
            >
              Request
            </button>

            <button
              :class="{ active: activeTab === 'response' }"
              @click="activeTab = 'response'"
            >
              Response
            </button>
          </div>
          
          <template v-if="activeTab === 'request'">
            <!-- Section A: Request Headers & Body -->
            <h5 class="headers-title">Request Headers</h5>
            <div class="headers-grid">
              <div v-if="Object.keys(selectedTransaction.requestHeaders).length === 0" class="no-headers">
                No request headers found
              </div>
              <div 
                v-for="(value, key) in selectedTransaction.requestHeaders" 
                :key="key" 
                class="header-row"
              >
                <span class="header-key">{{ key }}:</span>
                <span class="header-val" :title="value">{{ value }}</span>
              </div>
            </div>
            <h5 class="headers-title">Request Body</h5>
            <div v-if="selectedTransaction.requestBody">
              <div class="headers-grid">
                <div v-if="typeof selectedTransaction.requestBody.compressedLength === 'number'" class="header-row">
                  <span class="header-key">Compressed Length:</span>
                  <span class="header-val" :title="selectedTransaction.requestBody.length.toString()">{{ selectedTransaction.requestBody.compressedLength }}</span>
                </div>
                <div class="header-row">
                  <span class="header-key">Length:</span>
                  <span class="header-val" :title="selectedTransaction.requestBody.length.toString()">{{ selectedTransaction.requestBody.length }}</span>
                </div>
              </div>
              <HttpBodyViewer
                :body="selectedTransaction.requestBody"
              />
            </div>
            <div
              v-else
              class="no-headers"
            >
              No request body
            </div>            
          </template>

          <template v-else>
            <!-- Section B: Response Headers & Body -->
            <h5 class="headers-title">Response Headers</h5>
            <div class="headers-grid">
              <div v-if="Object.keys(selectedTransaction.responseHeaders).length === 0" class="no-headers">
                No response headers found
              </div>
              <div 
                v-for="(value, key) in selectedTransaction.responseHeaders" 
                :key="key" 
                class="header-row"
              >
                <span class="header-key">{{ key }}:</span>
                <span class="header-val" :title="value">{{ value }}</span>
              </div>
            </div>
            <h5 class="headers-title">Response Body</h5>
            <div v-if="selectedTransaction.responseBody">
              <div class="headers-grid">                
                <div v-if="typeof selectedTransaction.responseBody.compressedLength === 'number'" class="header-row">
                  <span class="header-key">Compressed Length:</span>
                  <span class="header-val" :title="selectedTransaction.responseBody.length.toString()">{{ selectedTransaction.responseBody.compressedLength }}</span>
                </div>
                <div class="header-row">
                  <span class="header-key">Length:</span>
                  <span class="header-val" :title="selectedTransaction.responseBody.length.toString()">{{ selectedTransaction.responseBody.length }}</span>
                </div>
              </div>
              <HttpBodyViewer              
                :body="selectedTransaction.responseBody"
              />
            </div>
            <div
              v-else
              class="no-headers"
            >
              No response body
            </div>            
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.http-traffic-viewer {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 140px);
  background-color: #1e1e1e;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
  color: #d4d4d4;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background-color: #2d2d2d;
  padding: 10px 15px;
  height: 36px;
  border-bottom: 1px solid #3c3c3c;
}
.status-indicator { display: flex; align-items: center; font-size: 14px; }
.dot { width: 10px; height: 10px; border-radius: 50%; margin-right: 8px; background-color: #4caf50; }
.btn-clear {
  background: #3c3c3c; color: #fff; border: 1px solid #555;
  padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 13px;
}
.btn-clear:hover { background: #444; }

/* Workspace Splitter Layout */
.workspace { display: flex; flex: 1; overflow: hidden; }
.table-container { flex: 1; overflow-y: auto; background-color: #1e1e1e; }

.traffic-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; color: #d4d4d4; table-layout: fixed; }
.traffic-table th {
  background-color: #252526; color: #858585; font-weight: 600;
  padding: 10px; position: sticky; top: 0; border-bottom: 1px solid #2d2d2d; z-index: 2;
}
/* Width distribution metrics */
.col-time { width: 5%; }
.col-inbound { width: 12%; }
.col-route { width: 12%; }
.col-targetHost { width: 18%; }
.col-requestURI { width: 18%; }
.col-method { width: 5%; }
.col-status { width: 15%; }
.col-type { width: 5%; }
.col-size { width: 5%; }
.col-duration { width: 5%; }

.traffic-table td { padding: 8px 10px; border-bottom: 1px solid #2d2d2d; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; cursor: pointer; }
.traffic-table tbody tr:hover { background-color: #2a2a2a; }
.traffic-table tbody tr.active-row { background-color: #094771 !important; color: #fff; }

.empty-row { text-align: center; color: #666; padding: 40px !important; font-style: italic; }

.cell-time { color: #858585; font-family: monospace; }
.cell-inbound { color: #e3e3e3; font-family: monospace; }
.cell-route { color: #e3e3e3; font-family: monospace; }
.cell-targetHost { color: #e3e3e3; font-family: monospace; }
.cell-requestURI { color: #e3e3e3; font-family: monospace; }
.cell-method { color: #e3e3e3; font-family: monospace; }
.cell-type { color: #e3e3e3; font-family: monospace; }
.cell-size { color: #e3e3e3; font-family: monospace; }
.cell-duration { font-family: monospace; text-align: right; padding-right: 15px !important; }

.splitter {
  width: 5px;
  cursor: col-resize;
  background: #2d2d2d;
  flex-shrink: 0;
}

.splitter:hover {
  background: #007acc;
}

/* Side panel layout styles */
.details-sidebar {
  min-width: 250px;
  max-width: 1200px;
  flex-shrink: 0;  
  overflow: scroll;
}
.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 15px;
  background-color: #2d2d2d;
  border-bottom: 1px solid #3c3c3c;
}
.sidebar-header h4 { margin: 0; font-size: 13px; color: #4caf50; text-transform: uppercase; }
.btn-close { background: none; border: none; color: #aaa; cursor: pointer; font-size: 14px; }
.btn-close:hover { color: #fff; }

.sidebar-content { flex: 1; overflow-y: auto; padding: 15px; font-size: 12px; }
.info-block { padding-bottom: 10px; }
.info-block p { margin: 4px 0; color: #aaa; }
.mono { font-family: monospace; color: #fff; }

.headers-title { margin: 15px 0 6px 0; color: #858585; text-transform: uppercase; font-size: 11px; letter-spacing: 0.5px; }
.headers-grid { background-color: #1e1e1e; border: 1px solid #2d2d2d; border-radius: 4px; padding: 8px; margin-bottom: 15px; }
.no-headers { color: #555; padding: 5px; font-style: italic; }
.header-row { display: flex; padding: 3px 0; border-bottom: 1px solid #252526; gap: 6px; }
.header-row:last-child { border-bottom: none; }
.header-key { color: #569cd6; font-family: monospace; font-weight: 600; white-space: nowrap; }
.header-val { color: #ce9178; font-family: monospace; word-break: break-all; overflow: hidden; text-overflow: ellipsis; }

/* Badges */
.method-badge { padding: 2px 6px; border-radius: 3px; font-weight: bold; font-size: 10px; color: #fff; font-family: monospace; }

.tabs {
  display: flex;
  margin-bottom: 12px;
  gap: 4px;
}

.tabs button {
  flex: 1;
  border: 1px solid #3c3c3c;
  background: #252526;
  color: #ccc;
  padding: 8px;
  cursor: pointer;
  max-width: 200px;
}

.tabs button.active {
  background: #094771;
  color: white;
}

.status-indicator .capture-btn {
  background: #3c3c3c;
  color: #fff;
  border: 1px solid #555;
  padding: 5px 12px;
  margin-left: 15px;
  border-radius: 4px;
  cursor: pointer;
  font-family: inherit;
  font-size: 13px;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.status-indicator .capture-btn:hover {
  filter: brightness(1.2);
}
.status-indicator .capture-btn.active {
  background-color: rgba(76, 175, 80, 0.15);
  border-color: #4caf50;
  color: #4caf50;
}
.status-indicator .capture-btn.paused {
  background-color: rgba(244, 67, 54, 0.1);
  border-color: #f44336;
  color: #ef5350;
}
.columns-menu {
  position: relative;
  display: inline-block;
}
.btn-columns {
  background: #3c3c3c;
  color: #fff;
  border: 1px solid #555;
  padding: 4px 10px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
}
.btn-columns:hover {
  background: #444;
}
.columns-menu:hover .columns-popup {
  display: block;
}
.columns-popup {
  display: none;
  position: absolute;
  top: calc(100% + 4px);
  right: 0;
  z-index: 100;

  min-width: 180px;
  max-height: 350px;
  overflow-y: auto;

  background: #252526;
  border: 1px solid #3c3c3c;
  border-radius: 6px;
  box-shadow: 0 6px 16px rgba(0,0,0,.5);

  padding: 6px 0;
}
.columns-popup label {
  display: flex;
  align-items: center;
  gap: 8px;

  padding: 6px 12px;
  cursor: pointer;
  user-select: none;
  font-size: 13px;
  color: #ddd;
}
.columns-popup label:hover {
  background: #2d2d2d;
}
.columns-popup input {
  margin: 0;
}
.controls div, .controls button {
  margin-left: 10px;
}
</style>