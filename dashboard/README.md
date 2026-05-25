## ProxyMap Monitor Dashboard

A web-based control and monitoring dashboard for ProxyMapService. It provides a real-time stream of system request logs via a secure WebSocket connection (SignalR) and handles user JWT authentication.

## 🚀 Features

* Real-Time Log Streaming: Connects to the backend via a SignalR WebSocket connection.
* Secure Access: Protects the dashboard view using JWT (JSON Web Tokens).
* Built-in Mock Server: Vite automatically intercepts authentication requests for local development and standalone testing.
* Log Filtering: Easily sort incoming logs by severity levels (Debug, Information, Warning, Error).

------------------------------
## 🛠️ Installation and Setup

This project requires Node.js (LTS version recommended) installed on your machine.

## 1. Install Dependencies

Clone the repository, navigate to the project root folder, and install the required npm packages:

```bash
npm install
```

## 2. Configure Environment Variables

The project uses environment variables to configure API endpoints and security options. Copy the example environment file to create your local development configuration:

```bash
cp .env.example .env.development
```

Open the newly created .env.development file and define the following parameters:

```ini
# The base URL where ProxyMapService backend is running (no trailing slash)
VITE_API_BASE_URL=http://localhost:5014

# The endpoint used for JWT authentication requests
VITE_LOGIN_URL=/api/login

# Credentials required to access the dashboard in dev/mock mode
LOGIN_USERNAME=test
LOGIN_PASSWORD=test

# JWT settings (Must strictly match the Authentication:Jwt section in the ProxyMapService appsettings.json!)
JWT_ISS=your_issuer
JWT_AUD=your_audience
JWT_SECRET=super_secret_key_that_is_at_least_32_characters_long_minimum!!
```

⚠️ Critical Requirement: The JWT_SECRET value must be a string containing at least 32 characters (256 bits). If it is shorter, the ASP.NET Core backend will reject the token signature validation for security reasons.

## 3. Run the Development Server

Start the local Vite development environment:

```bash
npm run dev
```

Once started, the terminal will display the local application address (typically http://localhost:5173/).

------------------------------

## 🔒 How Authentication Works in Dev Mode

   1. When you submit the login form, a request is sent to the path defined in VITE_LOGIN_URL (defaults to /api/login). A custom middleware plugin inside vite.config.ts intercepts this POST request.
   2. The middleware verifies the credentials against the configured LOGIN_USERNAME and LOGIN_PASSWORD variables.
   3. If valid, the plugin signs a new JWT using the HS256 algorithm, embedding the specified JWT_ISS, JWT_AUD, and JWT_SECRET, and sends it back to the client.
   4. The frontend stores this token in browser storage as TOKEN_ID and automatically appends it as a query string parameter (access_token=...) during the WebSocket handshake to satisfy the [Authorize] requirement on the ProxyMapService backend.

