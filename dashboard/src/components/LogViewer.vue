<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { LogEntry } from '../types/log'

const props = defineProps<{
  token: string
}>()

const logs = ref<LogEntry[]>([])
const isConnected = ref<boolean>(false)
const filterLevel = ref<string>('All')
const logsContainer = ref<HTMLDivElement | null>(null)

let connection: signalR.HubConnection | null = null

const getLogClass = (level: LogEntry['level']): string => {
  switch (level) {
    case 'Debug': return 'text-gray-400'
    case 'Information': return 'text-green-400'
    case 'Warning': return 'text-yellow-400'
    case 'Error': return 'text-red-500 font-bold'
    case 'Critical': return 'text-red-100 bg-red-800 px-1 font-bold'
    default: return 'text-white'
  }
}

const formatTime = (timestamp: string): string => {
  return new Date(timestamp).toLocaleTimeString()
}

const scrollToBottom = async (): Promise<void> => {
  await nextTick()
  if (logsContainer.value) {
    logsContainer.value.scrollTop = logsContainer.value.scrollHeight
  }
}

const startSignalR = () => {
  if (connection) {
    connection.stop()
    isConnected.value = false
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/EventLog`, {
      accessTokenFactory: () => props.token
    }) 
    .withAutomaticReconnect()                
    .configureLogging(signalR.LogLevel.Information)
    .build()

  connection.on('EventLog', (logEntry: LogEntry) => {
    logs.value.push(logEntry)    
    if (logs.value.length > 500) {
      logs.value.shift()
    }    
    scrollToBottom()
  })

  connection.start()
    .then(() => {
      isConnected.value = true
      console.log('SignalR Connected.')
    })
    .catch(err => console.error('SignalR Connection Error: ', err))
}

watch(() => props.token, (newToken) => {
  if (newToken) {
    startSignalR()
  } else if (connection) {
    connection.stop()
    isConnected.value = false
  }
})

onMounted(() => {
  if (props.token) {
    startSignalR()
  }
})

onUnmounted(() => {
  if (connection) {
    connection.stop()
  }
})

const clearLogs = (): void => {
  logs.value = []
}
</script>

<template>
  <div class="log-viewer">
    <div class="toolbar">
      <div class="status-indicator">
        <span :class="['dot', isConnected ? 'online' : 'offline']"></span>
        {{ isConnected ? 'Connected' : 'Disconnected' }}
      </div>

      <div class="controls">
        <select v-model="filterLevel">
          <option value="All">All levels</option>
          <option value="Debug">Debug</option>
          <option value="Information">Information</option>
          <option value="Warning">Warning</option>
          <option value="Error">Error</option>
        </select>
        <button @click="clearLogs">Clear</button>
      </div>
    </div>

    <div class="console" ref="logsContainer">
      <div v-if="logs.length === 0" class="empty-msg">
        Waiting for logs...
      </div>
      
      <div 
        v-for="(log, index) in logs" 
        :key="index"
        v-show="filterLevel === 'All' || log.level === filterLevel"
        class="log-line"
      >
        <span class="time">[{{ formatTime(log.timestamp) }}]</span>
        <span :class="['level', getLogClass(log.level)]">[{{ log.level.toUpperCase() }}]</span>
        <!-- span class="category">{{ log.category }}:</span -->
        <span class="message">{{ log.message }}</span>
        
        <div v-if="log.exception" class="exception">
          {{ log.exception }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.log-viewer {
  display: flex;
  flex-direction: column;
  height: 85vh;
  font-family: 'Courier New', Courier, monospace;
  background-color: #1e1e1e;
  color: #d4d4d4;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background-color: #2d2d2d;
  padding: 10px 15px;
  border-bottom: 1px solid #3c3c3c;
}
.status-indicator { display: flex; align-items: center; font-size: 14px; }
.dot { width: 10px; height: 10px; border-radius: 50%; margin-right: 8px; }
.dot.online { background-color: #4caf50; }
.dot.offline { background-color: #f44336; }
.controls select, .controls button {
  background: #3c3c3c;
  color: #fff;
  border: 1px solid #555;
  padding: 5px 10px;
  margin-left: 10px;
  border-radius: 4px;
  cursor: pointer;
}
.console { flex: 1; padding: 15px; overflow-y: auto; font-size: 13px; line-height: 1.5; }
.empty-msg { color: #666; text-align: center; margin-top: 20px; }
.log-line { margin-bottom: 4px; white-space: pre-wrap; word-break: break-all; }
.time { color: #858585; margin-right: 5px; }
.level { margin-right: 5px; }
.category { color: #569cd6; margin-right: 5px; }
.message { color: #e3e3e3; }
.exception { color: #f44336; padding-left: 20px; font-size: 12px; margin-top: 2px; }
.text-gray-400 { color: #9e9e9e; }
.text-green-400 { color: #4caf50; }
.text-yellow-400 { color: #ffeb3b; }
.text-red-500 { color: #f44336; font-weight: bold; }
.text-red-100 { color: #ffebee; }
.bg-red-800 { background-color: #c62828; border-radius: 2px; }
.px-1 { padding-left: 4px; padding-right: 4px; }
.font-bold { font-weight: bold; }
</style>