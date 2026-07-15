<script setup lang="ts">
import { ref, watch, computed, reactive, onMounted, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import LoginForm from './components/LoginForm.vue'
import ProxyStats from './components/ProxyStats.vue'
import LogViewer from './components/LogViewer.vue'
import HttpTrafficViewer from './components/HttpTrafficViewer.vue'
import { isTokenExpired, getTokenExpiration } from './utils/jwt'
import type { LogEntry, EventLogPayload } from './types/log'
import type { 
  HttpRequestEntry, HttpResponseEntry, HttpCompletionEntry, HttpBodyEntry, 
  HttpRequestPayload, HttpResponsePayload, HttpCompletionPayload, HttpBodyPayload,
  HttpHistoryDto } from './types/http'
import type { ProxyStatsData } from './types/stats'
import { type ColumnKey, type ColumnFilters, allColumns } from './types/column'

const stats = ref<ProxyStatsData | null>(null)
const statsError = ref<string>('')
const logs = ref<LogEntry[]>([])
const requests = ref<HttpRequestEntry[]>([])
const responses = ref<HttpResponseEntry[]>([])
const completions = ref<HttpCompletionEntry[]>([])
const requestBodies = ref<HttpBodyEntry[]>([])
const responseBodies = ref<HttpBodyEntry[]>([])
const isConnected = ref<boolean>(false)
const isInitializing = ref(true) 
const loginErrorMessage = ref<string>('')

const currentToken = ref<string>('')
const currentRefreshToken = ref<string>('')
let expiryCheckTimer: ReturnType<typeof setInterval> | null = null

const activeTab = ref<'stats' | 'logs' | 'network'>('stats')

let connection: signalR.HubConnection | null = null

const isLogCapturing = computed(() => stats.value?.logCapture ?? false)
const isHttpCapturing = computed(() => stats.value?.httpCapture ?? false)

const fetchLogHistory = async () => {
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';    
    const response = await fetch(`${baseUrl}/EventLog/recent`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${currentToken.value}`
      }
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch logs history. Error: ${response.status}`);
    }
    
    const history: LogEntry[] = await response.json();    
    logs.value = history;
  } catch (err) {
    console.error('Error loading log history:', err);
  }
}

const fetchTrafficHistory = async () => {
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';    
    const response = await fetch(`${baseUrl}/TrafficHistory/recent`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${currentToken.value}`
      }
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch traffic history. Error: ${response.status}`);
    }
    
    const history: HttpHistoryDto = await response.json();    
    requests.value = history.requests;
    responses.value = history.responses;
    completions.value = history.completions;
    requestBodies.value = history.requestBodies;
    responseBodies.value = history.responseBodies;
  } catch (err) {
    console.error('Error loading log history:', err);
  }
}

const fetchStats = async () => {
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014'
    const response = await fetch(`${baseUrl}/ProxyStats`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${currentToken.value}`,
        'Content-Type': 'application/json'
      }
    })

    if (response.status === 401) {
      const errorMessage = `Server error: ${response.status} ${response.statusText}`
      loginErrorMessage.value = errorMessage
      onLogout()
      throw new Error(errorMessage)
    }

    if (!response.ok) {
      throw new Error(`Server error: ${response.status}`)
    }

    stats.value = await response.json()
    statsError.value = ''
  } catch (err: any) {
    statsError.value = 'Failed to update service statistics'
    console.error('Error loading stats:', err)
  }
}

const handleToggleLogCapture = async () => {
  if (!stats.value) return

  const targetState = !stats.value.logCapture
  stats.value.logCapture = targetState

  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014'
    const response = await fetch(`${baseUrl}/EventLog/toggle`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${currentToken.value}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ capture: targetState })
    })

    if (!response.ok) {
      throw new Error(`Server error: ${response.status}`)
    }
  } catch (err) {
    stats.value.logCapture = !targetState
    console.error('Failed to toggle event log capture on server:', err)
  }
}

const handleToggleHttpCapture = async () => {
  if (!stats.value) return

  const targetState = !stats.value.httpCapture
  stats.value.httpCapture = targetState

  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014'
    const response = await fetch(`${baseUrl}/TrafficHistory/toggle`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${currentToken.value}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ capture: targetState })
    })

    if (!response.ok) {
      throw new Error(`Server error: ${response.status}`)
    }
  } catch (err) {
    stats.value.httpCapture = !targetState
    console.error('Failed to toggle event log capture on server:', err)
  }
}

const startSignalR = () => {
  if (connection) {
    connection.stop()
    isConnected.value = false
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/updates`, {
      accessTokenFactory: () => currentToken.value
    }) 
    .withAutomaticReconnect()                
    .configureLogging(signalR.LogLevel.Information)
    .build()

  connection.on('EventLog', (payload: EventLogPayload) => {
    logs.value.push(payload.entry)    
    while (payload.maxEntries && logs.value.length > payload.maxEntries) {
      logs.value.shift()
    }    
    // scrollToBottom()
  })

  connection.on('HttpRequest', (payload: HttpRequestPayload) => {
    requests.value.push(payload.entry)
    while (payload.maxEntries && requests.value.length > payload.maxEntries) {    
      requests.value.shift()
    }
  })  

  connection.on('HttpResponse', (payload: HttpResponsePayload) => {
    responses.value.push(payload.entry)
    while (payload.maxEntries && responses.value.length > payload.maxEntries) {
      responses.value.shift()
    }
  })

  connection.on('HttpCompletion', (payload: HttpCompletionPayload) => {
    completions.value.push(payload.entry)
    while (payload.maxEntries && responses.value.length > payload.maxEntries) {
      responses.value.shift()
    }
  })

  connection.on('HttpRequestBody', (payload: HttpBodyPayload) => {
    requestBodies.value.push(payload.entry)
    while (payload.maxEntries && requestBodies.value.length > payload.maxEntries) {
      requestBodies.value.shift()
    }
  })

  connection.on('HttpResponseBody', (payload: HttpBodyPayload) => {
    responseBodies.value.push(payload.entry)
    while (payload.maxEntries && requestBodies.value.length > payload.maxEntries) {
      responseBodies.value.shift()
    }
  })

  connection.on('Stats', (data: ProxyStatsData) => {
    stats.value = data
  })

  connection.onreconnected((connectionId) => {
    console.log('Reconnected! Fetching missed entries...')
    Promise.all([
      fetchLogHistory(),
      fetchTrafficHistory(),
      fetchStats(),
    ])
      .then(() => {
        console.log('All missed entries successfully reloaded.');
      })
      .catch(err => {
        console.error('Failed to reload entries after reconnect:', err);
      });    
  })

  Promise.all([
    fetchLogHistory(),
    fetchTrafficHistory(),
    fetchStats(),
  ])
    .then(() => {
      if (connection) {
        return connection.start();
      }
    })
    .then(() => {
      isConnected.value = true;
      console.log('SignalR Connected.');
    })
    .catch(err => {
      console.error('SignalR Connection Error: ', err);
    })
}

watch(currentToken, (newToken) => {
  if (newToken) {
    startSignalR()
  } else if (connection) {
    connection.stop()
    isConnected.value = false
  }
}, { immediate: true })

const onLogout = (): void => {
  localStorage.removeItem('URL_TOKEN')
  localStorage.removeItem('TOKEN_ID')
  localStorage.removeItem('REFRESH_TOKEN')
  currentToken.value = ''
  currentRefreshToken.value = ''
  if (expiryCheckTimer) {
    clearInterval(expiryCheckTimer)
    expiryCheckTimer = null
  }  
}

const onLoginSuccess = (token: string, refreshToken?: string): void => {
  currentToken.value = token
  currentRefreshToken.value = refreshToken || ''
  startExpiryCheck()
}

const startExpiryCheck = () => {
  if (expiryCheckTimer) clearInterval(expiryCheckTimer)

  const thresholdSeconds = Number(import.meta.env.VITE_TOKEN_EXPIRY_THRESHOLD_SEC) || 300
  const checkIntervalMs = Number(import.meta.env.VITE_TOKEN_CHECK_INTERVAL_MS) || 30000

  console.log(`thresholdSeconds: ${thresholdSeconds}`)
  console.log(`checkIntervalMs: ${checkIntervalMs}`)

  expiryCheckTimer = setInterval(async () => {    
    if (isTokenExpired(currentToken.value)) {
      onLogout()
    }
    else {
      const expiresIn = getTokenExpiration(currentToken.value)
      if (expiresIn < thresholdSeconds) {
        await doRefreshToken(currentToken.value, currentRefreshToken.value)
      }
    }
  }, checkIntervalMs)
}

const doRefreshToken = async (token: string | null, refreshToken: string | null) => {
  const refreshUrl = import.meta.env.VITE_REFRESH_URL;

  if (!refreshUrl || !token || !refreshToken)  {
    return false
  }

  try {
    const response = await fetch(refreshUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        token,
        refreshToken
      }),
    })

    const data = await response.json()

    if (response.ok && data.success) {
      localStorage.setItem('TOKEN_ID', data.token)
      localStorage.setItem('REFRESH_TOKEN', data.refreshToken)

      currentToken.value = data.token
      currentRefreshToken.value = data.refreshToken || ''

      return true
    }

  } catch (error) {
    console.error(error)
  }

  localStorage.removeItem('REFRESH_TOKEN')
  currentRefreshToken.value = ''

  return false
}

const clearLogs = (): void => {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';

  fetch(`${baseUrl}/EventLog/clear`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${currentToken.value}` 
    }
  })
  .then(response => {
    if (!response.ok) {
      throw new Error('Failed to clear logs on server');
    }
    return response.json()
  })
  .then((data: { success: boolean; message: string }) => {
    if (data.success) {
      logs.value = []
      console.log(data.message)
    }
  })
  .catch(err => {
    console.error('Error clearing logs:', err);
  });
}

const clearNetworkData = () => {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5014';

  fetch(`${baseUrl}/TrafficHistory/clear`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${currentToken.value}` 
    }
  })
  .then(response => {
    if (!response.ok) {
      throw new Error('Failed to clear traffic history on server');
    }
    return response.json()
  })
  .then((data: { success: boolean; message: string }) => {
    if (data.success) {
      requests.value = []
      responses.value = [] 
      console.log(data.message)
    }
  })
  .catch(err => {
    console.error('Error clearing traffic history:', err);
  });
}

onMounted(async () => {
  const urlParams = new URLSearchParams(window.location.search);
  let urlToken = urlParams.get('token');

  if (urlToken) {
    localStorage.setItem('URL_TOKEN', urlToken);
    const baseUrl = import.meta.env.BASE_URL;
    window.history.replaceState({}, document.title, baseUrl);     
  }
  else {
    urlToken = localStorage.getItem('URL_TOKEN')
  }

  const savedToken = localStorage.getItem('TOKEN_ID')
  const savedRefreshToken = localStorage.getItem('REFRESH_TOKEN')
  
  if (urlToken) {
    currentToken.value = urlToken;
    currentRefreshToken.value = '';
  }
  else if (savedToken && !isTokenExpired(savedToken)) {
    currentToken.value = savedToken
    currentRefreshToken.value = savedRefreshToken || ''
    startExpiryCheck() 
  } else {
    const ok = await doRefreshToken(savedToken, savedRefreshToken)
    if (!ok)
      onLogout()
  }

  isInitializing.value = false 
})

onUnmounted(() => {
  if (expiryCheckTimer) clearInterval(expiryCheckTimer)
  if (connection) connection.stop()
})
/*
const visibleColumns = ref<ColumnKey[]>(
    allColumns.map(c => c.key)
)

const columnFilters = reactive<ColumnFilters>(
  Object.fromEntries(
    allColumns.map(c => [c.key, ''])
  ) as Record<ColumnKey, string>
)
*/
</script>

<template>
  <header class="app-header">
    <div class="header-container">
      <h1 class="logo">ProxyMap Dashboard</h1>
      <div v-if="currentToken" class="user-menu">
        <button @click="onLogout" class="btn-logout">Log out</button>
      </div>    
    </div>
  </header>

  <main class="app-content">    
    <LoginForm 
      v-if="!isInitializing && !currentToken" 
      @login-success="onLoginSuccess" 
      v-model:error-message="loginErrorMessage"
    />

    <div v-if="currentToken" class="dashboard-layout">
      <div class="tab-navigation">
        <button 
          :class="['tab-btn', { active: activeTab === 'stats' }]" 
          @click="activeTab = 'stats'"
        >
          📊 Stats
        </button>  
        <button 
          :class="['tab-btn', { active: activeTab === 'logs' }]" 
          @click="activeTab = 'logs'"
        >
          📄 Event Log
        </button>  
        <button 
          :class="['tab-btn', { active: activeTab === 'network' }]" 
          @click="activeTab = 'network'"
        >
          🌐 HTTP Traffic
        </button>              
      </div>
      <div class="tab-view-content">
        <div class="stats-div" v-if="activeTab === 'stats'">
          <div class="stats-title">ProxyMapService Performance Metrics</div>
          <ProxyStats             
            :stats="stats"
            :stats-error="statsError" />
        </div>
        <LogViewer 
          v-show="activeTab === 'logs'" 
          :logs="logs" 
          :isConnected="isConnected" 
          :is-capturing="isLogCapturing" 
          @clear-logs="clearLogs"
          @toggle-capture="handleToggleLogCapture" 
        />
        <HttpTrafficViewer 
          v-show="activeTab === 'network'" 
          :requests="requests" 
          :responses="responses"
          :completions="completions"
          :request-bodies="requestBodies"
          :response-bodies="responseBodies"
          :isConnected="isConnected"
          :is-capturing="isHttpCapturing"
          @clear-network="clearNetworkData"
          @toggle-capture="handleToggleHttpCapture"
        />          
      </div>
    </div>

    <div v-else class="unauthorized-placeholder">
      <div class="placeholder-icon">🔒</div>
      <h4>Authentication Required</h4>
      <div v-if="isInitializing" class="app-loader">
        <div class="spinner"></div>
        <p>Checking authorization...</p>
      </div>
      <div v-else>
        <p>Please log in above to access the real-time telemetry metrics and event log.</p>
      </div>
    </div>    
  </main>
</template>

<style>
html, body {
  height: 100%;
}
body {
  margin: 0; 
  display: flex;
  flex-direction: column;  
  background-color: #121212; 
  color: #ffffff;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
}
.app-header {
  background-color: #1a1a1a; 
  border-bottom: 1px solid #2d2d2d; 
  padding: 12px 20px;
}
.header-container {
  max-width: 100%; /* 1200px; */
  margin: 0 auto;
  display: flex; 
  justify-content: space-between; 
  align-items: center;
}
.logo { margin: 0; font-size: 1.3rem; color: #4caf50; font-weight: 700; }

.user-menu { display: flex; align-items: center; gap: 15px; }
.user-badge { font-size: 13px; color: #4caf50; background: rgba(76, 175, 80, 0.1); padding: 4px 10px; border-radius: 12px; }
.btn-logout {
  background-color: #dc3545; color: white; border: none; padding: 6px 14px;
  border-radius: 4px; font-size: 13px; cursor: pointer; font-weight: 600; transition: background 0.2s;
}
.btn-logout:hover { background-color: #bd2130; }

.app-content { 
  flex-grow: 1;
  max-width: 100%; /* 1200px; */
  margin: 25px auto; 
  padding: 0 20px; 
}

.log-section { margin-top: 25px; }
.section-title { color: #666; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px; font-weight: bold; }

.unauthorized-placeholder {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 45vh; background-color: #1a1a1a; border: 1px dashed #333; border-radius: 6px; padding: 30px; text-align: center;
}
.placeholder-icon { font-size: 2.5rem; margin-bottom: 15px; }
.unauthorized-placeholder h4 { margin: 0 0 8px 0; color: #e53935; font-size: 1.1rem; }
.unauthorized-placeholder p { margin: 0; max-width: 400px; color: #777; font-size: 13px; line-height: 1.5; }

.tab-navigation {
  display: flex;
  gap: 4px;
  /* border-bottom: 2px solid #2d2d2d; */
  margin-top: 10px;  
}
.tab-btn {
  background: #1e1e1e;
  color: #858585;
  border: 1px solid #2d2d2d;
  border-bottom: none;
  padding: 10px 20px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  border-top-left-radius: 6px;
  border-top-right-radius: 6px;
  transition: all 0.2s ease;
}
.tab-btn:hover {
  color: #fff;
  background: #252526;
}
.tab-btn.active {
  background: #2d2d2d;
  color: #4caf50;
  border-color: #4caf50;
  border-bottom: 0px solid #2d2d2d;
  margin-bottom: -1px; /* Pull overlap forward over global line */
}
.tab-view-content {
  margin-top: 0px;
}
.tab-view-content > div {
  border-top-left-radius: 0px !important;
  /* border-top-right-radius: 0px !important; */
  border: 1px solid #4caf50;
}
.stats-title { color: #666; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px; font-weight: bold; }
.stats-div {
  padding-left: 1rem;
  padding-top: 1rem;
  padding-right: 1rem;
}
.app-loader {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  /* min-height: 100vh; */
  font-family: sans-serif;
  margin-top: 3rem;
}
.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid #f3f3f3;
  border-top: 4px solid #3498db;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 10px;
}
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>
