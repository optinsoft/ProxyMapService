<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import type { ProxyStatsData } from '../types/stats'

const props = defineProps<{
  token: string
}>()

const stats = ref<ProxyStatsData | null>(null)
const error = ref<string>('')
let timer: ReturnType<typeof setInterval> | null = null

const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0\u00A0B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + '\u00A0' + sizes[i]
}

const fetchStats = async () => {
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014'
    const response = await fetch(`${baseUrl}/ProxyStats`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${props.token}`,
        'Content-Type': 'application/json'
      }
    })

    if (!response.ok) {
      throw new Error(`Server error: ${response.status}`)
    }

    stats.value = await response.json()
    error.value = ''
  } catch (err: any) {
    error.value = 'Failed to update service statistics'
    console.error(err)
  }
}

onMounted(() => {
  fetchStats()
  timer = setInterval(fetchStats, 5000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <div class="stats-wrapper">
    <div v-if="error" class="stats-error">{{ error }}</div>
    
    <div v-if="stats" class="stats-container">
      <!-- Service Status Card -->
      <div class="stats-card system-info">
        <h4>Service Status</h4>
        <p><span class="status">{{ stats.serviceInfo }}</span></p>
        <p><strong>Running:</strong> <span :class="stats.started ? 'status status-up' : 'status status-down'">{{ stats.started ? 'YES' : 'NO' }}</span></p>
        <p><strong>Started At:</strong> {{ stats.startTime }}</p>
        <p><strong>Current Time:</strong> {{ stats.currentTime }}</p>
      </div>

      <!-- Traffic Metrics Card -->
      <div class="stats-card">
        <h4>Traffic Metrics (Total)</h4>
        <div class="grid-2x2">
          <div>📥 Received: <span>{{ formatBytes(stats.totalBytesRead) }}</span></div>
          <div>📤 Sent: <span>{{ formatBytes(stats.totalBytesSent) }}</span></div>
        </div>
        <h5 class="sub-title">Proxy Share:</h5>
        <div class="grid-2x2 sub-grid">
          <div>📥 Received: {{ formatBytes(stats.proxyBytesRead) }}</div>
          <div>📤 Sent: {{ formatBytes(stats.proxyBytesSent) }}</div>
        </div>
        <div class="connection-status sub-grid">
          <p>🔌Connections: <span class="text-green">✔ {{ stats.proxyConnected }}</span> / <span class="text-red">❌ {{ stats.proxyFailed }}</span></p>
        </div>        
        <h5 class="sub-title">Bypass Share:</h5>
        <div class="grid-2x2 sub-grid">
          <div>📥 Received: {{ formatBytes(stats.bypassBytesRead) }}</div>
          <div>📤 Sent: {{ formatBytes(stats.bypassBytesSent) }}</div>
        </div>
        <div class="connection-status sub-grid">
          <p>🔌Connections: <span class="text-green">✔ {{ stats.bypassConnected }}</span> / <span class="text-red">❌ {{ stats.bypassFailed }}</span></p>
        </div>        
      </div>

      <!-- Authentication & Sessions Card -->
      <div class="stats-card">
        <h4>Auth & Sessions</h4>
        <p>Total Sessions: <span class="badge">{{ stats.sessionsCount }}</span></p>
        <p>Authenticated: <span class="text-green">{{ stats.authenticated }}</span></p>
        <p>Invalid Auth Attempts: <span class="text-red">{{ stats.authenticationInvalid }}</span></p>
        <p>Auth Required: <span>{{ stats.authenticationRequired }}</span></p>
        <p>Auth Not Required: <span>{{ stats.authenticationNotRequired }}</span></p>
      </div>

      <!-- Routing & Connection Handling Card -->
      <div class="stats-card">
        <h4>Host Handling</h4>
        <p>Proxified: <span class="text-blue">{{ stats.hostProxified }}</span></p>
        <p>Bypassed: <span class="text-purple">{{ stats.hostBypassed }}</span></p>
        <p>Rejected Hosts: <span class="text-red">{{ stats.hostRejected }}</span></p>
        <p>Header Failures: <span>{{ stats.headerFailed }}</span></p>
        <p>Cache Hits: <span>{{ stats.cacheResponses }} ({{ formatBytes(stats.cacheBytesSent) }})</span></p>
      </div>
    </div>
    
    <div v-else-if="!error" class="stats-loading">Loading service metrics...</div>
  </div>
</template>

<style scoped>
.stats-wrapper { margin-bottom: 20px; font-family: sans-serif; }
.stats-container { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 15px; }
.stats-card { background-color: #1e1e1e; border: 1px solid #2d2d2d; border-radius: 6px; padding: 15px; box-shadow: 0 4px 6px rgba(0,0,0,0.2); }
.stats-card h4 { margin: 0 0 12px 0; color: #4caf50; font-size: 0.95rem; border-bottom: 1px solid #2d2d2d; padding-bottom: 6px; text-transform: uppercase; }
.stats-card p { margin: 6px 0; font-size: 13px; color: #ccc; display: flex; justify-content: space-between; }
.system-info p { justify-content: flex-start; gap: 10px; }
.grid-2x2 { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; font-size: 13px; }
.grid-2x2 span { font-weight: bold; color: #fff; }
.sub-title { margin: 10px 0 4px 0; font-size: 11px; color: #777; text-transform: uppercase; }
.sub-grid { color: #aaa; font-size: 12px; padding-left: 5px; border-left: 2px solid #333; }
.status-up { color: #4caf50; font-weight: bold; }
.status-down { color: #f44336; font-weight: bold; }
.text-green { color: #4caf50; font-weight: bold; }
.text-red { color: #f44336; font-weight: bold; }
.text-blue { color: #2196f3; font-weight: bold; }
.text-purple { color: #9c27b0; font-weight: bold; }
.badge { background: #333; padding: 2px 6px; border-radius: 4px; color: #fff; font-weight: bold; }
.stats-error { background: rgba(244,67,54,0.1); border: 1px solid #f44336; color: #f44336; padding: 10px; border-radius: 4px; margin-bottom: 10px; font-size: 13px; }
.stats-loading { text-align: center; color: #666; font-size: 13px; padding: 20px; }
.connection-status {
  margin-top: 4px;
  font-size: 12px;
}
.connection-status p {
  margin: 0;
  color: #888;
  display: flex;
  justify-content: flex-start;
  gap: 8px;
}
.status { margin-bottom: 1rem; }
</style>