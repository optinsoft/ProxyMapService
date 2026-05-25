import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv  } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import * as jose from 'jose'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const jwt_secret: string = env.JWT_SECRET || 'your_secret_key';
  const jwt_iss: string = env.JWT_ISS || 'your_issuer';
  const jwt_aud: string = env.JWT_AUD || 'your_audience';

  const login_username: string = env.LOGIN_USERNAME || 'test';
  const login_password: string = env.LOGIN_PASSWORD || 'test';

  return {
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
                    const cryptoKey = new TextEncoder().encode(jwt_secret)
                     const token = await new jose.SignJWT({ name: username, role: 'user' })
                      .setProtectedHeader({ alg: 'HS256' })
                      .setIssuedAt()
                      .setIssuer(jwt_iss)
                      .setAudience(jwt_aud)
                      .setExpirationTime('1h')
                      .sign(cryptoKey)
                    res.writeHead(200)
                    res.end(JSON.stringify({
                      success: true,
                      token
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
