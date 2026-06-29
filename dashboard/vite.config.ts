import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv  } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import * as jose from 'jose'

import crypto from 'node:crypto'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const jwt_secret: string = env.JWT_SECRET || 'your_secret_key';
  const jwt_iss: string = env.JWT_ISS || 'your_issuer';
  const jwt_aud: string = env.JWT_AUD || 'your_audience';

  const login_username: string = env.LOGIN_USERNAME || 'test';
  const login_password: string = env.LOGIN_PASSWORD || 'test';

  const createAccessToken = async function (username: string) {
    const cryptoKey = new TextEncoder().encode(jwt_secret)
    return await new jose.SignJWT({
      name: username,
      role: 'admin'
    })
      .setProtectedHeader({ alg: 'HS256' })
      .setIssuedAt()
      .setIssuer(jwt_iss)
      .setAudience(jwt_aud)
      .setExpirationTime('10m')
      .sign(cryptoKey)
  }  

  const refreshTokens = new Map<string, {
    username: string
    expires: Date
  }>()

  const REFRESH_TOKEN_LIFETIME_DAYS = 30

  const createRefreshToken = function (username: string) {
    const token = crypto.randomBytes(32).toString('base64url')
    refreshTokens.set(token, {
      username,
      expires: new Date(
        Date.now() + REFRESH_TOKEN_LIFETIME_DAYS * 24 * 60 * 60 * 1000)
    })
    return token
  }

  return {
    base: '/ProxyMapDashboard/', 
    plugins: [
      vue(),
      vueDevTools(),
      {
        name: 'mock-auth',
        configureServer(server) {
          server.middlewares.use((req, res, next) => {
            if (req.url === '/api/login' && req.method === 'POST') {
              let body = ''            
              req.on('data', chunk => { body += chunk })            
              req.on('end', async () => {
                res.setHeader('Content-Type', 'application/json')              
                try {
                  const { username, password } = JSON.parse(body)
                  if (username === login_username && password === login_password) {
                    const accessToken = await createAccessToken(username)
                    const refreshToken = createRefreshToken(username)
                    res.writeHead(200)
                    res.end(JSON.stringify({
                      success: true,
                      token: accessToken,
                      refreshToken
                    }))
                  } else {
                    res.writeHead(401)
                    res.end(JSON.stringify({ message: 'Incorrect username or password' }))
                  }
                } catch (e) {
                  res.writeHead(400)
                  res.end(JSON.stringify({ message: 'Data processing error' }))
                }
              })
            }
            else if (req.url === '/api/refresh' && req.method === 'POST') {
              let body = ''
              req.on('data', chunk => body += chunk)
              req.on('end', async () => {
                res.setHeader('Content-Type', 'application/json')
                try {
                  const { refreshToken } = JSON.parse(body)
                  const stored = refreshTokens.get(refreshToken)
                  if (!stored) {
                    res.writeHead(401)
                    return res.end(JSON.stringify({
                      message: 'Invalid refresh token'
                    }))
                  }
                  if (stored.expires < new Date()) {
                    refreshTokens.delete(refreshToken)
                    res.writeHead(401)
                    return res.end(JSON.stringify({
                      message: 'Refresh token expired'
                    }))
                  }
                  refreshTokens.delete(refreshToken)
                  const newRefreshToken = createRefreshToken(stored.username)
                  const accessToken = await createAccessToken(stored.username)
                  res.writeHead(200)
                  res.end(JSON.stringify({
                    success: true,
                    token: accessToken,
                    refreshToken: newRefreshToken
                  }))
                }
                catch {
                  res.writeHead(400)
                  res.end(JSON.stringify({
                    message: 'Data processing error'
                  }))
                }
              })
            } else {
              next()
            }
          })
        }
      }
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url))
      },
    },
  }
})
