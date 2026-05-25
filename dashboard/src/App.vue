<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import LoginForm from './components/LoginForm.vue'
import ProxyStats from './components/ProxyStats.vue'
import LogViewer from './components/LogViewer.vue'
import { isTokenExpired } from './utils/jwt'

const currentToken = ref<string>('')
let expiryCheckTimer: ReturnType<typeof setInterval> | null = null

const onLogout = (): void => {
  localStorage.removeItem('TOKEN_ID')
  currentToken.value = ''
  if (expiryCheckTimer) {
    clearInterval(expiryCheckTimer)
    expiryCheckTimer = null
  }  
}

const onLoginSuccess = (token: string): void => {
  currentToken.value = token
  startExpiryCheck()
}

const startExpiryCheck = () => {
  if (expiryCheckTimer) clearInterval(expiryCheckTimer)

  expiryCheckTimer = setInterval(() => {
    if (isTokenExpired(currentToken.value)) {
      console.warn('JWT Token has expired. Logging out automatically.')
      onLogout()
    }
  }, 30000)
}

onMounted(() => {
  const savedToken = localStorage.getItem('TOKEN_ID')
  
  if (savedToken && !isTokenExpired(savedToken)) {
    currentToken.value = savedToken
    startExpiryCheck()
  } else {
    onLogout()
  }
})

onUnmounted(() => {
  if (expiryCheckTimer) clearInterval(expiryCheckTimer)
})
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
    <LoginForm v-if="!currentToken" @login-success="onLoginSuccess" />

    <div v-if="currentToken" class="dashboard-layout">
      <section class="dashboard-section">
        <div class="section-title">ProxyMapService Performance Metrics</div>
        <ProxyStats :token="currentToken" />
      </section>
      <section class="dashboard-section">
        <LogViewer :token="currentToken" />
      </section>
    </div>

    <div v-else class="unauthorized-placeholder">
      <div class="placeholder-icon">🔒</div>
      <h4>Authentication Required</h4>
      <p>Please log in above to access the real-time telemetry metrics and event log.</p>
    </div>    
  </main>
</template>

<style>
body {
  margin: 0; background-color: #121212; color: #ffffff;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
}
.app-header {
  background-color: #1a1a1a; border-bottom: 1px solid #2d2d2d; padding: 12px 20px;
}
.header-container {
  max-width: 1200px; margin: 0 auto;
  display: flex; justify-content: space-between; align-items: center;
}
.logo { margin: 0; font-size: 1.3rem; color: #4caf50; font-weight: 700; }

/* Стили панели пользователя в шапке */
.user-menu { display: flex; align-items: center; gap: 15px; }
.user-badge { font-size: 13px; color: #4caf50; background: rgba(76, 175, 80, 0.1); padding: 4px 10px; border-radius: 12px; }
.btn-logout {
  background-color: #dc3545; color: white; border: none; padding: 6px 14px;
  border-radius: 4px; font-size: 13px; cursor: pointer; font-weight: 600; transition: background 0.2s;
}
.btn-logout:hover { background-color: #bd2130; }

.app-content { max-width: 1200px; margin: 25px auto; padding: 0 20px; }
.log-section { margin-top: 25px; }
.section-title { color: #666; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px; font-weight: bold; }

.unauthorized-placeholder {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 45vh; background-color: #1a1a1a; border: 1px dashed #333; border-radius: 6px; padding: 30px; text-align: center;
}
.placeholder-icon { font-size: 2.5rem; margin-bottom: 15px; }
.unauthorized-placeholder h4 { margin: 0 0 8px 0; color: #e53935; font-size: 1.1rem; }
.unauthorized-placeholder p { margin: 0; max-width: 400px; color: #777; font-size: 13px; line-height: 1.5; }
</style>
