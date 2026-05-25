<script setup lang="ts">
import { ref } from 'vue'
import { isTokenExpired } from '../utils/jwt'

const emit = defineEmits<{
  (e: 'login-success', token: string): void
  (e: 'logout'): void
}>()

const username = ref<string>('')
const password = ref<string>('')
const errorMessage = ref<string>('')
const isLoading = ref<boolean>(false)

const savedToken = localStorage.getItem('TOKEN_ID')
const isAuthenticated = ref<boolean>(!!savedToken && !isTokenExpired(savedToken))

const handleLogin = async (): Promise<void> => {
  errorMessage.value = ''
  isLoading.value = true

  const loginUrl = import.meta.env.VITE_LOGIN_URL || '/api/login';

  try {
    const response = await fetch(loginUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        username: username.value,
        password: password.value,
      }),
    })

    const data = await response.json()

    if (response.ok && data.success) {
      localStorage.setItem('TOKEN_ID', data.token)
      isAuthenticated.value = true
      
      username.value = ''
      password.value = ''
      
      emit('login-success', data.token)
    } else {
      errorMessage.value = data.message || 'Authorization error'
    }
  } catch (error) {
    errorMessage.value = 'Failed to contact the server'
    console.error(error)
  } finally {
    isLoading.value = false
  }
}

const handleLogout = (): void => {
  localStorage.removeItem('TOKEN_ID')
  isAuthenticated.value = false
  emit('logout')
}
</script>

<template>
  <div class="auth-container">
    <form v-if="!isAuthenticated" @submit.prevent="handleLogin" class="auth-form">
      <h2>Login to the monitoring system</h2>
      
      <div class="form-group">
        <label for="username">Username:</label>
        <input 
          id="username"
          v-model="username" 
          type="text" 
          required 
          placeholder="test"
          :disabled="isLoading"
        />
      </div>

      <div class="form-group">
        <label for="password">Password:</label>
        <input 
          id="password"
          v-model="password" 
          type="password" 
          required 
          placeholder="test"
          :disabled="isLoading"
        />
      </div>

      <div v-if="errorMessage" class="error-alert">
        {{ errorMessage }}
      </div>

      <button type="submit" :disabled="isLoading" class="btn-submit">
        {{ isLoading ? 'Logging in...' : 'Login' }}
      </button>
    </form>

    <div v-else class="welcome-box">
      <p>You have successfully logged into the system.</p>
      <button @click="handleLogout" class="btn-logout">Log out of your account</button>
    </div>
  </div>
</template>

<style scoped>
.auth-container {
  max-width: 400px;
  margin: 40px auto;
  padding: 20px;
  background-color: #2d2d2d;
  border-radius: 8px;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.3);
  color: #ffffff;
  font-family: Arial, sans-serif;
}

h2 {
  margin-top: 0;
  margin-bottom: 20px;
  font-size: 1.3rem;
  color: #4caf50;
  text-align: center;
}

.form-group {
  margin-bottom: 15px;
}

.form-group label {
  display: block;
  margin-bottom: 5px;
  font-size: 14px;
  color: #b3b3b3;
}

.form-group input {
  width: 100%;
  padding: 10px;
  box-sizing: border-box;
  background-color: #1e1e1e;
  border: 1px solid #444;
  border-radius: 4px;
  color: #fff;
}

.form-group input:focus {
  outline: none;
  border-color: #4caf50;
}

.error-alert {
  color: #f44336;
  background-color: rgba(244, 67, 54, 0.1);
  padding: 10px;
  border-radius: 4px;
  margin-bottom: 15px;
  font-size: 14px;
  border: 1px solid rgba(244, 67, 54, 0.2);
}

.btn-submit {
  width: 100%;
  padding: 11px;
  background-color: #4caf50;
  color: white;
  border: none;
  border-radius: 4px;
  font-size: 16px;
  cursor: pointer;
  font-weight: bold;
}

.btn-submit:hover:not(:disabled) {
  background-color: #45a049;
}

.btn-submit:disabled {
  background-color: #555;
  cursor: not-allowed;
}

.welcome-box {
  text-align: center;
}

.welcome-box p {
  margin-bottom: 15px;
  color: #e0e0e0;
}

.btn-logout {
  padding: 8px 16px;
  background-color: #f44336;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.btn-logout:hover {
  background-color: #d32f2f;
}
</style>