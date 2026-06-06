<script setup lang="ts">
import { ref, computed } from 'vue'
import type { HttpRequestEntry, HttpResponseEntry, MergedTrafficEntry } from '../types/log'

const props = defineProps<{
  requests: HttpRequestEntry[],
  responses: HttpResponseEntry[],
  isConnected: boolean
}>()

const emit = defineEmits<{
  (e: 'clear-network'): void
}>()

const selectedId = ref<string | null>(null)

// Match requests and responses by unique ID
const mergedTransactions = computed<MergedTrafficEntry[]>(() => {
  return props.requests.map(req => {
    const res = props.responses.find(r => r.id === req.id)
    
    // Parse timestamp calculations
    const reqTime = new Date(req.timestamp).getTime()
    const resTime = res ? new Date(res.timestamp).getTime() : null
    const duration = (resTime && reqTime) ? (resTime - reqTime) : null

    const routeDisplay = res?.route || req.route || '-'

    return {
      id: req.id,
      timestamp: new Date(req.timestamp).toLocaleTimeString(),
      inbound: req.inbound,
      requestURI: req.requestURI,
      requestMethod: req.requestMethod.toUpperCase(),
      route: routeDisplay,
      statusCode: res ? parseInt(res.statusCode, 10) : null,
      statusText: res ? res.statusText : '',
      durationMs: duration,
      requestHeaders: req.headers || {},
      responseHeaders: res?.headers || {}
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
</script>

<template>
  <div class="http-traffic-viewer">
    <div class="toolbar">
      <div class="status-indicator">
        <span class="dot online"></span> HTTP Traffic Analyzer
      </div>
      <button class="btn-clear" @click="emit('clear-network'); selectedId = null">Clear</button>
    </div>

    <!-- Master-Detail Layout Container -->
    <div class="workspace">
      <!-- Left side: The Main log table grid -->
      <div class="table-container">
        <table class="traffic-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>Inbound</th>
              <th>Route</th>
              <th>Request URI</th>
              <th>Method</th>
              <th>Status</th>
              <th>Duration</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="mergedTransactions.length === 0">
              <td colspan="6" class="empty-row">No proxy transactions captured yet...</td>
            </tr>
            <tr 
              v-for="tx in mergedTransactions" 
              :key="tx.id"
              :class="{ 'active-row': selectedId === tx.id }"
              @click="selectRow(tx.id)"
            >
              <td class="cell-time">{{ tx.timestamp }}</td>
              <td class="cell-inbound">{{ tx.inbound }}</td>
              <td>
                <span :class="['route-badge', {'direct': 'route-direct', 'file': 'route-file'}[tx.route.toLowerCase()] || 'route-proxy']">
                  {{ tx.route }}
                </span>
              </td>
              <td class="cell-target" :title="tx.requestURI">{{ tx.requestURI }}</td>
              <td class="cell-method">{{ tx.requestMethod }}</td>
              <td>
                <span :class="['status-tag', getStatusClass(tx.statusCode)]">
                  {{ tx.statusCode ? `${tx.statusCode} ${tx.statusText}` : 'Pending...' }}
                </span>
              </td>
              <td class="cell-duration">
                {{ tx.durationMs !== null ? tx.durationMs + ' ms' : '-' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Right side: Headers inspector sidebar (Appears only when a row is clicked) -->
      <div v-if="selectedTransaction" class="details-sidebar">
        <div class="sidebar-header">
          <h4>Transaction Inspector</h4>
          <button class="btn-close" @click="selectedId = null">✕</button>
        </div>
        
        <div class="sidebar-content">
          <div class="info-block">
            <p><strong>Inbound:</strong> <span class="mono">{{ selectedTransaction.inbound }}</span></p>
            <p><strong>Route:</strong> <span class="mono">{{ selectedTransaction.route }}</span></p>
            <p><strong>Request URI:</strong> <span class="mono">{{ selectedTransaction.requestURI }}</span></p>
            <p><strong>Request Method:</strong> <span class="mono">{{ selectedTransaction.requestMethod }}</span></p>
            <p><strong>Status Code:</strong> <span class="mono">{{ selectedTransaction.statusCode }}</span></p>
            <p><strong>Status Text:</strong> <span class="mono">{{ selectedTransaction.statusText }}</span></p>
            <p><strong>Duration (ms):</strong> <span class="mono">{{ selectedTransaction.durationMs }}</span></p>
          </div>

          <!-- Section A: Request Headers -->
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

          <!-- Section B: Response Headers -->
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
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.http-traffic-viewer {
  display: flex;
  flex-direction: column;
  height: 62vh;
  background-color: #1e1e1e;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background-color: #252526;
  padding: 8px 15px;
  border-bottom: 1px solid #2d2d2d;
}
.status-indicator { display: flex; align-items: center; font-size: 13px; color: #ccc; }
.dot { width: 8px; height: 8px; border-radius: 50%; margin-right: 8px; background-color: #4caf50; }
.btn-clear {
  background: #3c3c3c; color: #fff; border: 1px solid #555;
  padding: 4px 12px; border-radius: 4px; cursor: pointer; font-size: 13px;
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
.traffic-table th:nth-child(1) { width: 60px; }
.traffic-table th:nth-child(2) { width: 180px; }
.traffic-table th:nth-child(3) { width: 20%; }
.traffic-table th:nth-child(4) { width: 20%; }
.traffic-table th:nth-child(5) { width: 70px; }
.traffic-table th:nth-child(6) { width: 20%; }
.traffic-table th:nth-child(7) { width: 60px; }

.traffic-table td { padding: 8px 10px; border-bottom: 1px solid #2d2d2d; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; cursor: pointer; }
.traffic-table tbody tr:hover { background-color: #2a2a2a; }
.traffic-table tbody tr.active-row { background-color: #094771 !important; color: #fff; }

.empty-row { text-align: center; color: #666; padding: 40px !important; font-style: italic; }
.cell-time { color: #858585; font-family: monospace; }
.cell-inbound { color: #e3e3e3; font-family: monospace; }
.cell-method { color: #e3e3e3; font-family: monospace; }
.cell-target { color: #e3e3e3; font-family: monospace; }
.cell-duration { font-family: monospace; text-align: right; padding-right: 15px !important; }

/* Side panel layout styles */
.details-sidebar {
  width: 420px;
  background-color: #252526;
  border-left: 1px solid #2d2d2d;
  display: flex;
  flex-direction: column;
  z-index: 5;
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
.info-block { margin-bottom: 15px; border-bottom: 1px solid #333; padding-bottom: 10px; }
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
</style>